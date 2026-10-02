using EshopGuard.Core;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Entities.Usage;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EshopGuard.Jobs.Fixes;

/// <summary>
/// The job <c>fix.recheck</c> (change 11, AD 5): the recheck of the own wording or the filled facts of a proposal, or of the
/// replacement of a group, by <see cref="TextRecheck"/> for the jurisdictions of the active markets of the e-shop and the
/// modules of its findings. The result is written only for the text it was computed for (the fingerprint in the payload must
/// match the current text); a text changed meanwhile keeps <c>pending</c> for its own job. The Jev calls are counted as
/// <c>recheck</c> in the usage of the tenant (no run). Logs carry ids and codes only.
/// </summary>
public sealed class FixRecheckHandler(IEshopGuard guard, UsageRecorder usage, TimeProvider time, ILogger<FixRecheckHandler> logger) : IJobHandler
{
    public string Kind => FixJobs.RecheckKind;

    public JobResourceClass ResourceClass => JobResourceClass.Jev;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var payload = context.Job.Payload.RootElement;
        var target = payload.GetProperty("target").GetString();
        var id = Guid.Parse(payload.GetProperty("id").GetString()!);
        var hash = payload.GetProperty("text_hash").GetString()!;
        var db = context.Services.GetRequiredService<EshopGuardDb>();
        var blobs = context.Services.GetRequiredService<ExtractContextReader>();
        var input = await db.ExecuteInTenantTransactionAsync(() => LoadAsync(db, blobs, target, id, ct), ct).ConfigureAwait(false);
        if (input is null || ProposalText.Hash(input.Text) != hash)
        {
            logger.LogInformation("fix.recheck {Target} {Id} stale", target, id);
            return JobResult.Done;
        }

        var scope = new RunAmbientScope(input.TenantId, input.ShopId, Guid.Empty, context.Job.Id) { JevOperation = UsageOperation.Recheck };
        RecheckOutcome outcome;
        using (RunAmbient.Enter(scope))
        {
            try
            {
                outcome = await TextRecheck.CheckAsync(guard, input.Text, input.Before, input.After, input.Jurisdictions, input.Modules, ct).ConfigureAwait(false);
            }
            finally
            {
                await usage.FlushAsync(scope, CancellationToken.None).ConfigureAwait(false);
            }
        }

        var now = time.GetUtcNow();
        var written = false;
        await context.CompleteAsync(async tx =>
        {
            if (target == FixJobs.TargetGroup)
            {
                var group = await tx.Db.FixGroups.FirstOrDefaultAsync(g => g.Id == id, ct).ConfigureAwait(false);
                if (group is not null && GroupValues.Replacement(group) is { } replacement && ProposalText.Hash(replacement) == hash)
                {
                    group.RecheckStatus = outcome.Status;
                    group.RecheckResult = System.Text.Json.JsonDocument.Parse(outcome.ToJson(hash, now));
                    group.UpdatedAt = now;
                    written = true;
                }
            }
            else
            {
                var proposal = await tx.Db.FixProposals.FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false);
                if (proposal is not null && ProposalText.Hash(ProposalText.Text(proposal)) == hash)
                {
                    proposal.RecheckStatus = outcome.Status;
                    proposal.RecheckResult = System.Text.Json.JsonDocument.Parse(outcome.ToJson(hash, now));
                    proposal.UpdatedAt = now;
                    written = true;
                }
            }

            await tx.Db.SaveChangesAsync(ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
        logger.LogInformation("fix.recheck {Target} {Id} {Status} {Written}", target, id, outcome.Status, written);
        return JobResult.Done;
    }

    private sealed record RecheckInput(
        Guid TenantId, Guid ShopId, string Text, string? Before, string? After, IReadOnlyList<string> Jurisdictions, IReadOnlyList<string> Modules);

    private static async Task<RecheckInput?> LoadAsync(EshopGuardDb db, ExtractContextReader blobs, string? target, Guid id, CancellationToken ct)
    {
        Guid tenantId, shopId;
        string text;
        long[] hashes;
        Guid[] findingIds;
        int? block = null;
        string? extractKey = null;
        if (target == FixJobs.TargetGroup)
        {
            var group = await db.FixGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, ct).ConfigureAwait(false);
            if (group is null || GroupValues.Replacement(group) is not { } replacement)
            {
                return null;
            }

            (tenantId, shopId, text) = (group.TenantId, group.ShopId, replacement);
            hashes = group.SegmentHash is { } h ? [h] : [];
            findingIds = await db.Findings.AsNoTracking().Where(f => f.ShopId == shopId && f.SegmentHash != null && hashes.Contains(f.SegmentHash.Value))
                .Select(f => f.Id).ToArrayAsync(ct).ConfigureAwait(false);
        }
        else
        {
            var proposal = await db.FixProposals.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false);
            if (proposal is null)
            {
                return null;
            }

            (tenantId, shopId, text) = (proposal.TenantId, proposal.ShopId, ProposalText.Text(proposal));
            findingIds = proposal.FindingIds;
            block = proposal.BlockIndex;
            extractKey = await db.PageVersions.AsNoTracking().Where(v => v.ShopId == proposal.ShopId && v.Id == proposal.PageVersionId)
                .Select(v => v.ExtractBlobKey).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        }

        var modules = await db.Findings.AsNoTracking().Where(f => findingIds.Contains(f.Id)).Select(f => f.Module).Distinct().ToListAsync(ct).ConfigureAwait(false);
        var jurisdictions = await ActiveJurisdictionsAsync(db, shopId, ct).ConfigureAwait(false);
        string? before = null, after = null;
        if (extractKey is not null && await blobs.ReadAsync(tenantId, shopId, extractKey, ct).ConfigureAwait(false) is { } page)
        {
            (before, after) = page.Around(block);
        }

        return new RecheckInput(tenantId, shopId, text, before, after, jurisdictions, modules.Count > 0 ? modules : ["eco", "legal"]);
    }

    /// <summary>The jurisdictions of the active markets of the e-shop (codes of the markets), otherwise its home country.</summary>
    public static async Task<IReadOnlyList<string>> ActiveJurisdictionsAsync(EshopGuardDb db, Guid shopId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var active = await db.ShopMarkets.AsNoTracking().Where(m => m.ShopId == shopId && m.Status == ShopMarketStatus.Active)
            .Select(m => m.CountryCode).ToListAsync(ct).ConfigureAwait(false);
        if (active.Count == 0)
        {
            var home = await db.Shops.AsNoTracking().Where(s => s.Id == shopId).Select(s => s.HomeCountry).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            active = home is null ? ["sk"] : [home];
        }

        return active.Select(c => c.ToLowerInvariant()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }
}
