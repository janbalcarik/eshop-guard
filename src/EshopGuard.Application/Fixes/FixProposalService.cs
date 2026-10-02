using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Findings;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Shops;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Fixes;
using EshopGuard.Jobs.Queue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Fixes;

/// <summary>Settings of the fixes (<c>Fixes</c>).</summary>
public sealed class FixesOptions
{
    public const string SectionName = "Fixes";

    /// <summary>Longest own wording of a proposal (characters).</summary>
    public int MaxTextLength { get; set; } = 5000;

    /// <summary>Proposals generated after „Nie“ per tenant and day (K rozhodnutí 11, proposal 50).</summary>
    public int DailyGenerationsPerTenant { get; set; } = 50;
}

/// <summary>
/// The proposals of fixes (change 11, AD 5): choosing a variant, the own wording and the facts (each change of the text sets
/// the recheck to <c>pending</c> and queues <c>fix.recheck</c>), acceptance only when the current text passed the recheck in
/// every active country of the e-shop and no fact is missing, rejection and taking an acceptance back. Every change goes over
/// the version of the row (<c>If-Match</c>); the findings follow <see cref="FindingStatusMachine"/>, the decisions go to the
/// memory and the audit (codes only, never texts). An e-shop with only the sample is <c>409 shop.sample_only</c>.
/// </summary>
public sealed class FixProposalService(
    EshopGuardDb db,
    ShopReader reader,
    FindingTransitions transitions,
    DecisionMemoryWriter memory,
    IJobQueue queue,
    SecurityAuditWriter audit,
    IOptions<FixesOptions> options,
    TimeProvider time)
{
    /// <summary>The key of <c>PUT …/alternative</c> that goes back to the proposal of the rewrite.</summary>
    public const string ProposalKey = "proposal";

    private static readonly FixProposalStatus[] Open = [FixProposalStatus.Proposed, FixProposalStatus.Edited];

    public async Task<ProposalDto> GetAsync(Guid shopId, Guid proposalId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            await reader.ReadAsync(shopId, ct).ConfigureAwait(false);
            var proposal = await db.FixProposals.AsNoTracking().FirstOrDefaultAsync(p => p.Id == proposalId && p.ShopId == shopId, ct).ConfigureAwait(false)
                ?? throw new DomainException(ProblemCodes.ProposalNotFound, 404);
            return ProposalMapper.Dto(proposal);
        }, ct).ConfigureAwait(false);

    public Task<ProposalDto> SelectAlternativeAsync(Guid userId, Guid shopId, Guid proposalId, string? key, uint? version, CancellationToken ct) =>
        ChangeAsync(userId, shopId, proposalId, version, AuditActions.ProposalAlternativeSelected, async (proposal, _) =>
        {
            EnsureOpen(proposal);
            if (key == ProposalKey)
            {
                var edited = proposal.EditedText is not null;
                proposal.SelectedAlternative = null;
                proposal.EditedText = null;
                proposal.Status = FixProposalStatus.Proposed;
                // The own wording overwrote the recheck of the proposal: the proposal is checked again.
                return edited;
            }

            if (key is null || ProposalText.Alternatives(proposal.Alternatives).All(a => a.Key != key))
            {
                throw new DomainException(ProblemCodes.ProposalAlternativeUnknown, 400, new Dictionary<string, object?> { ["key"] = key });
            }

            proposal.SelectedAlternative = key;
            proposal.EditedText = null;
            proposal.Status = FixProposalStatus.Proposed;
            await Task.CompletedTask.ConfigureAwait(false);
            return ProposalText.Placeholders(proposal.Placeholders).Any(p => !string.IsNullOrWhiteSpace(p.Value));
        }, new JsonObject { ["key"] = key }, ct);

    public Task<ProposalDto> EditTextAsync(Guid userId, Guid shopId, Guid proposalId, string? text, uint? version, CancellationToken ct) =>
        ChangeAsync(userId, shopId, proposalId, version, AuditActions.ProposalEdited, (proposal, _) =>
        {
            EnsureOpen(proposal);
            var wording = text?.Trim();
            if (string.IsNullOrEmpty(wording))
            {
                throw new DomainException(ProblemCodes.ProposalTextEmpty, 400);
            }

            if (wording.Length > options.Value.MaxTextLength)
            {
                throw new DomainException(ProblemCodes.ProposalTextTooLong, 400, new Dictionary<string, object?> { ["max"] = options.Value.MaxTextLength });
            }

            if (wording == ProposalText.Text(proposal))
            {
                throw new DomainException(ProblemCodes.ProposalTextUnchanged, 400);
            }

            proposal.EditedText = wording;
            proposal.Status = FixProposalStatus.Edited;
            return Task.FromResult(true);
        }, new JsonObject(), ct);

    public Task<ProposalDto> FillPlaceholdersAsync(Guid userId, Guid shopId, Guid proposalId, IReadOnlyDictionary<string, string?>? values, uint? version, CancellationToken ct) =>
        ChangeAsync(userId, shopId, proposalId, version, AuditActions.ProposalPlaceholdersFilled, (proposal, _) =>
        {
            EnsureOpen(proposal);
            var placeholders = ProposalText.Placeholders(proposal.Placeholders).ToList();
            foreach (var (key, value) in values ?? new Dictionary<string, string?>())
            {
                var index = placeholders.FindIndex(p => p.Key == key);
                if (index < 0)
                {
                    throw new DomainException(ProblemCodes.ProposalPlaceholderUnknown, 400, new Dictionary<string, object?> { ["key"] = key });
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new DomainException(ProblemCodes.ProposalPlaceholderEmpty, 400, new Dictionary<string, object?> { ["key"] = key });
                }

                // The fact goes in exactly as written (only the whitespace around it is dropped).
                placeholders[index] = placeholders[index] with { Value = value.Trim() };
            }

            proposal.Placeholders = JsonDocument.Parse(ProposalText.PlaceholdersJson(placeholders));
            return Task.FromResult(true);
        }, new JsonObject { ["keys"] = (values?.Count ?? 0) }, ct);

    public async Task<ProposalDto> AcceptAsync(Guid userId, Guid shopId, Guid proposalId, uint? version, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var (shop, proposal) = await RequireAsync(shopId, proposalId, version, ct).ConfigureAwait(false);
            if (proposal.Status == FixProposalStatus.Accepted)
            {
                return ProposalMapper.Dto(proposal);
            }

            EnsureOpen(proposal);
            var missing = ProposalText.MissingPlaceholders(proposal);
            if (missing.Count > 0)
            {
                throw new DomainException(ProblemCodes.ProposalPlaceholderMissing, 409, new Dictionary<string, object?> { ["keys"] = missing });
            }

            var recheck = ProposalText.Recheck(proposal);
            if (recheck == RecheckStatus.StillFinding)
            {
                var (jurisdictions, rules) = ProposalMapper.Failed(proposal.RecheckResult);
                throw new DomainException(ProblemCodes.ProposalRecheckFailed, 409, new Dictionary<string, object?> { ["jurisdictions"] = jurisdictions, ["ruleIds"] = rules });
            }

            if (recheck == RecheckStatus.Pending)
            {
                throw new DomainException(ProblemCodes.ProposalRecheckPending, 409);
            }

            // A text checked by this change must cover every active country; a market added since checks it again.
            var active = await FixRecheckHandler.ActiveJurisdictionsAsync(db, shopId, ct).ConfigureAwait(false);
            if (proposal.RecheckResult is not null && ProposalText.Recheck(proposal) == proposal.RecheckStatus)
            {
                var covered = ProposalMapper.Covered(proposal.RecheckResult);
                var uncovered = active.Where(j => !covered.Contains(j)).ToList();
                if (uncovered.Count > 0)
                {
                    await queue.EnqueueAsync(FixJobs.Recheck(proposal.TenantId, shopId, FixJobs.TargetProposal, proposal.Id, ProposalText.Hash(ProposalText.Text(proposal)), active),
                        DbSql.Transaction(db), ct).ConfigureAwait(false);
                    throw new DomainException(ProblemCodes.ProposalRecheckPending, 409, new Dictionary<string, object?> { ["jurisdictions"] = uncovered });
                }
            }

            proposal.Status = FixProposalStatus.Accepted;
            proposal.DecidedBy = userId;
            proposal.DecidedAt = time.GetUtcNow();
            await SaveAsync(ct).ConfigureAwait(false);
            await FollowFindingsAsync(userId, proposal, ct).ConfigureAwait(false);
            var finding = await db.Findings.AsNoTracking().Where(f => proposal.FindingIds.Contains(f.Id)).OrderBy(f => f.Id).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            var text = ProposalText.Text(proposal);
            await memory.RecordAsync(shopId, finding?.SegmentHash, finding?.Text, text.Length == 0 ? Decision.Remove : Decision.Replace, userId, ct,
                replacementText: text, sourceProposalId: proposal.Id).ConfigureAwait(false);
            await AuditAsync(AuditActions.ProposalAccepted, shop.TenantId, userId, proposal, new JsonObject { ["findings"] = proposal.FindingIds.Length }, ct).ConfigureAwait(false);
            return await ReloadAsync(proposal, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public async Task<ProposalDto> RejectAsync(Guid userId, Guid shopId, Guid proposalId, uint? version, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var (shop, proposal) = await RequireAsync(shopId, proposalId, version, ct).ConfigureAwait(false);
            EnsureOpen(proposal);
            proposal.Status = FixProposalStatus.Rejected;
            proposal.DecidedBy = userId;
            proposal.DecidedAt = time.GetUtcNow();
            await SaveAsync(ct).ConfigureAwait(false);
            foreach (var finding in await db.Findings.Where(f => proposal.FindingIds.Contains(f.Id)).ToListAsync(ct).ConfigureAwait(false))
            {
                var others = await db.FixProposals.AnyAsync(p => p.Id != proposal.Id && p.FindingIds.Contains(finding.Id)
                    && (p.Status == FixProposalStatus.Proposed || p.Status == FixProposalStatus.Edited || p.Status == FixProposalStatus.Accepted), ct).ConfigureAwait(false);
                if (!others && finding.Status == FindingStatus.Proposed)
                {
                    await transitions.ApplyAsync(finding, FindingStatus.Open, userId, ct, "proposal_rejected").ConfigureAwait(false);
                }
            }

            await SaveAsync(ct).ConfigureAwait(false);
            await AuditAsync(AuditActions.ProposalRejected, shop.TenantId, userId, proposal, [], ct).ConfigureAwait(false);
            return await ReloadAsync(proposal, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public async Task<ProposalDto> UnacceptAsync(Guid userId, Guid shopId, Guid proposalId, uint? version, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var (shop, proposal) = await RequireAsync(shopId, proposalId, version, ct).ConfigureAwait(false);
            if (proposal.Status == FixProposalStatus.Published)
            {
                throw new DomainException(ProblemCodes.ProposalAlreadyPublished, 409);
            }

            if (proposal.Status != FixProposalStatus.Accepted)
            {
                throw new DomainException(ProblemCodes.ProposalLocked, 409, new Dictionary<string, object?> { ["status"] = FindingMapper.Text(proposal.Status) });
            }

            proposal.Status = proposal.EditedText is null ? FixProposalStatus.Proposed : FixProposalStatus.Edited;
            proposal.DecidedBy = null;
            proposal.DecidedAt = null;
            await SaveAsync(ct).ConfigureAwait(false);
            foreach (var finding in await db.Findings.Where(f => proposal.FindingIds.Contains(f.Id)).ToListAsync(ct).ConfigureAwait(false))
            {
                if (finding.Status == FindingStatus.Approved)
                {
                    await transitions.ApplyAsync(finding, FindingStatus.Proposed, userId, ct, "acceptance_taken_back").ConfigureAwait(false);
                }

                await memory.SupersedeAsync(shopId, finding.SegmentHash, ct, sourceProposalId: proposal.Id).ConfigureAwait(false);
            }

            await SaveAsync(ct).ConfigureAwait(false);
            await AuditAsync(AuditActions.ProposalUnaccepted, shop.TenantId, userId, proposal, [], ct).ConfigureAwait(false);
            return await ReloadAsync(proposal, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    /// <summary>
    /// After an acceptance: a finding whose every current proposal is accepted (or published) goes to <c>approved</c>
    /// (through <c>proposed</c> when it was still <c>open</c>).
    /// </summary>
    internal async Task FollowFindingsAsync(Guid userId, FixProposal proposal, CancellationToken ct)
    {
        foreach (var finding in await db.Findings.Where(f => proposal.FindingIds.Contains(f.Id)).ToListAsync(ct).ConfigureAwait(false))
        {
            var pending = await db.FixProposals.AnyAsync(p => p.FindingIds.Contains(finding.Id)
                && (p.Status == FixProposalStatus.Proposed || p.Status == FixProposalStatus.Edited), ct).ConfigureAwait(false);
            if (pending)
            {
                continue;
            }

            if (finding.Status is FindingStatus.Open or FindingStatus.NeedsAnswer)
            {
                await transitions.ApplyAsync(finding, FindingStatus.Proposed, userId, ct).ConfigureAwait(false);
            }

            if (finding.Status == FindingStatus.Proposed)
            {
                await transitions.ApplyAsync(finding, FindingStatus.Approved, userId, ct).ConfigureAwait(false);
            }
        }

        await SaveAsync(ct).ConfigureAwait(false);
    }

    /// <summary>A change of the text of a proposal: the recheck goes to <c>pending</c> and its job is queued (202).</summary>
    private async Task<ProposalDto> ChangeAsync(
        Guid userId, Guid shopId, Guid proposalId, uint? version, string action, Func<FixProposal, CancellationToken, Task<bool>> change, JsonObject data, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var (shop, proposal) = await RequireAsync(shopId, proposalId, version, ct).ConfigureAwait(false);
            var recheck = await change(proposal, ct).ConfigureAwait(false);
            if (recheck)
            {
                proposal.RecheckStatus = RecheckStatus.Pending;
                proposal.RecheckResult = null;
            }

            await SaveAsync(ct).ConfigureAwait(false);
            if (recheck)
            {
                var active = await FixRecheckHandler.ActiveJurisdictionsAsync(db, shopId, ct).ConfigureAwait(false);
                await queue.EnqueueAsync(FixJobs.Recheck(shop.TenantId, shopId, FixJobs.TargetProposal, proposal.Id, ProposalText.Hash(ProposalText.Text(proposal)), active),
                    DbSql.Transaction(db), ct).ConfigureAwait(false);
            }

            await AuditAsync(action, shop.TenantId, userId, proposal, data, ct).ConfigureAwait(false);
            return await ReloadAsync(proposal, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    /// <summary>The e-shop (404), not only the sample (409), then the proposal of it (404) at the version the client read.</summary>
    private async Task<(Data.Entities.Shops.Shop Shop, FixProposal Proposal)> RequireAsync(Guid shopId, Guid proposalId, uint? version, CancellationToken ct)
    {
        var shop = await reader.ReadAsync(shopId, ct).ConfigureAwait(false);
        SampleOnlyGuard.Ensure(shop);
        var proposal = await db.FixProposals.FirstOrDefaultAsync(p => p.Id == proposalId && p.ShopId == shopId, ct).ConfigureAwait(false)
            ?? throw new DomainException(ProblemCodes.ProposalNotFound, 404);
        db.Entry(proposal).Property(p => p.Version).OriginalValue = Concurrency.Require(version);
        return (shop, proposal);
    }

    private static void EnsureOpen(FixProposal proposal)
    {
        if (!Open.Contains(proposal.Status))
        {
            throw new DomainException(ProblemCodes.ProposalLocked, 409, new Dictionary<string, object?> { ["status"] = FindingMapper.Text(proposal.Status) });
        }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException(ProblemCodes.ConcurrencyConflict, 409);
        }
    }

    private async Task<ProposalDto> ReloadAsync(FixProposal proposal, CancellationToken ct)
    {
        await db.Entry(proposal).ReloadAsync(ct).ConfigureAwait(false);
        return ProposalMapper.Dto(proposal);
    }

    private Task AuditAsync(string action, Guid tenantId, Guid userId, FixProposal proposal, JsonObject data, CancellationToken ct)
    {
        data["pageId"] = proposal.PageId.ToString("D");
        return audit.WriteAsync(new AuditEvent(action, tenantId, userId, "proposal", proposal.Id.ToString("D"), data), ct);
    }
}
