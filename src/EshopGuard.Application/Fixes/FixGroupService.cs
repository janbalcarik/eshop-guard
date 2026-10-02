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

/// <summary>
/// Bulk fixes (change 11, AD 7, design E): one decision for the same sentence on many pages. The groups are built by another
/// change (K rozhodnutí 1); this one changes only the facts, the mode, the excluded pages and the state. The pages of a group are
/// the pages of its findings (<c>segment_hash</c>); the pages of <c>fit.individual</c> and those the merchant excluded are fixed
/// one by one. Approval is one transaction: an accepted proposal (<c>group_id</c>) for every included page, the findings
/// through the state machine, the decision remembered once for the group, the group locked. A change of the wording queues
/// <c>fix.recheck</c> (202); approval needs every fact and an <c>ok</c> recheck (except removal). Changes go over
/// <c>If-Match</c>; the audit carries codes and counts.
/// </summary>
public sealed class FixGroupService(
    EshopGuardDb db,
    ShopReader reader,
    ExtractContextReader extracts,
    FindingTransitions transitions,
    DecisionMemoryWriter memory,
    IJobQueue queue,
    SecurityAuditWriter audit,
    ITenantContext tenant,
    IOptions<FixesOptions> options,
    TimeProvider time)
{
    public const int PageLimit = 30;
    public const int SampleCount = 3;

    private static readonly string[] Modes = ["replace", "remove", "custom"];
    private static readonly FixGroupStatus[] Locked = [FixGroupStatus.Approved, FixGroupStatus.Published, FixGroupStatus.PartiallyPublished];

    public async Task<FixGroupListDto> ListAsync(Guid shopId, string? status, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var shop = await reader.ReadAsync(shopId, ct).ConfigureAwait(false);
            var query = db.FixGroups.AsNoTracking().Where(g => g.ShopId == shopId);
            if (status is not null)
            {
                var wanted = Enum.GetValues<FixGroupStatus>().Where(s => FindingMapper.Text(s) == status).ToArray();
                if (wanted.Length == 0)
                {
                    throw DomainException.Validation(new ValidationResult().Add("status", ProblemCodes.Fields.ValueNotAllowed));
                }

                query = query.Where(g => wanted.Contains(g.Status));
            }

            if (SampleOnlyGuard.IsSampleOnly(shop))
            {
                return new FixGroupListDto([], new FixGroupTotalsDto(0, 0));
            }

            var items = new List<FixGroupListItemDto>();
            foreach (var group in await query.OrderByDescending(g => g.PageCount).ThenBy(g => g.Id).ToListAsync(ct).ConfigureAwait(false))
            {
                var pages = await PagesAsync(group, ct).ConfigureAwait(false);
                var individual = PageReviewService.Individual(group).Select(i => i.PageId).ToHashSet();
                var findings = await FindingsAsync(group, ct).ConfigureAwait(false);
                items.Add(new FixGroupListItemDto(
                    group.Id, FindingMapper.Text(group.Kind), group.OriginalText, pages.Count, Included(group, pages).Count, pages.Count(p => individual.Contains(p.PageId)),
                    FindingMapper.Text(group.Status), FindingMapper.Text(group.Mode), GroupValues.Missing(group).Count, Recheck(group).Status,
                    findings.OrderBy(f => f.Rank).FirstOrDefault()?.RuleId));
            }

            return new FixGroupListDto(items, new FixGroupTotalsDto(items.Count, items.Sum(i => i.PageCount)));
        }, ct).ConfigureAwait(false);

    public async Task<FixGroupDto> DetailAsync(Guid shopId, Guid groupId, string? cursor, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var group = await RequireAsync(shopId, groupId, forChange: false, ct).ConfigureAwait(false);
            return await DtoAsync(group, cursor, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public Task<FixGroupDto> SetValuesAsync(Guid userId, Guid shopId, Guid groupId, IReadOnlyDictionary<string, string?>? values, uint? version, CancellationToken ct) =>
        ChangeAsync(userId, shopId, groupId, version, AuditActions.GroupValuesSet, group =>
        {
            var keys = GroupValues.Keys(group);
            var filled = new Dictionary<string, string>(GroupValues.Values(group), StringComparer.Ordinal);
            foreach (var (key, value) in values ?? new Dictionary<string, string?>())
            {
                if (!keys.Contains(key))
                {
                    throw new DomainException(ProblemCodes.GroupPlaceholderUnknown, 400, new Dictionary<string, object?> { ["key"] = key });
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    filled.Remove(key);
                }
                else
                {
                    // The fact goes in exactly as written (only the whitespace around it is dropped).
                    filled[key] = value.Trim();
                }
            }

            group.FilledValues = JsonDocument.Parse(GroupValues.ValuesJson(filled));
            return Task.FromResult(new JsonObject { ["keys"] = (values?.Count ?? 0) });
        }, ct);

    public Task<FixGroupDto> SetModeAsync(Guid userId, Guid shopId, Guid groupId, string? mode, string? customText, uint? version, CancellationToken ct) =>
        ChangeAsync(userId, shopId, groupId, version, AuditActions.GroupModeSet, group =>
        {
            if (mode is null || !Modes.Contains(mode))
            {
                throw DomainException.Validation(new ValidationResult().Add("mode", ProblemCodes.Fields.ValueNotAllowed));
            }

            var parsed = Enum.Parse<FixGroupMode>(mode, ignoreCase: true);
            if (parsed == FixGroupMode.Custom)
            {
                var text = customText?.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    throw new DomainException(ProblemCodes.GroupCustomTextRequired, 400);
                }

                if (text.Length > options.Value.MaxTextLength)
                {
                    throw new DomainException(ProblemCodes.ProposalTextTooLong, 400, new Dictionary<string, object?> { ["max"] = options.Value.MaxTextLength });
                }

                group.CustomText = text;
            }

            group.Mode = parsed;
            return Task.FromResult(new JsonObject { ["mode"] = mode });
        }, ct);

    public Task<FixGroupDto> ExcludePagesAsync(Guid userId, Guid shopId, Guid groupId, IReadOnlyList<Guid>? pageIds, uint? version, CancellationToken ct) =>
        ChangeAsync(userId, shopId, groupId, version, AuditActions.GroupPagesExcluded, async group =>
        {
            var pages = await PagesAsync(group, ct).ConfigureAwait(false);
            var ids = (pageIds ?? []).Distinct().ToArray();
            if (ids.FirstOrDefault(id => pages.All(p => p.PageId != id)) is var unknown && unknown != Guid.Empty)
            {
                throw new DomainException(ProblemCodes.GroupPageNotInGroup, 400, new Dictionary<string, object?> { ["pageId"] = unknown });
            }

            group.ExcludedPageIds = ids;
            if (Included(group, pages).Count == 0)
            {
                throw new DomainException(ProblemCodes.GroupNoPagesLeft, 409);
            }

            return new JsonObject { ["pages"] = ids.Length };
        }, ct);

    /// <summary>Approves the group: an accepted proposal for every included page, in one transaction.</summary>
    public async Task<FixGroupDto> ApproveAsync(Guid userId, Guid shopId, Guid groupId, uint? version, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var group = await RequireAsync(shopId, groupId, forChange: true, ct).ConfigureAwait(false);
            db.Entry(group).Property(g => g.Version).OriginalValue = Concurrency.Require(version);
            EnsureEditable(group);
            var replacement = EnsureApprovable(group);
            var pages = await PagesAsync(group, ct).ConfigureAwait(false);
            var included = Included(group, pages);
            var applied = new List<Guid>();
            foreach (var page in included)
            {
                if (await ApplyAsync(userId, group, page, replacement, ct).ConfigureAwait(false) is not null)
                {
                    applied.Add(page.PageId);
                }
            }

            await FollowFindingsAsync(userId, group, ct).ConfigureAwait(false);
            await memory.RecordAsync(shopId, group.SegmentHash, group.OriginalText, group.Mode == FixGroupMode.Remove ? Decision.Remove : Decision.Replace, userId, ct,
                replacementText: replacement).ConfigureAwait(false);
            var now = time.GetUtcNow();
            group.Status = FixGroupStatus.Approved;
            group.ApprovedBy = userId;
            group.ApprovedAt = now;
            group.LockedAt = now;
            group.UpdatedAt = now;
            await SaveAsync(ct).ConfigureAwait(false);
            await AuditAsync(AuditActions.GroupApproved, userId, group, new JsonObject
            {
                ["pages"] = applied.Count,
                ["individual"] = pages.Count(p => PageReviewService.Individual(group).Any(i => i.PageId == p.PageId)),
                ["excluded"] = group.ExcludedPageIds.Length,
                ["notLocated"] = included.Count - applied.Count,
            }, ct).ConfigureAwait(false);
            await db.Entry(group).ReloadAsync(ct).ConfigureAwait(false);
            return await DtoAsync(group, null, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    /// <summary>„Len na tejto stránke“: the fix accepted on one page of the group only.</summary>
    public async Task<ProposalDto> ApprovePageAsync(Guid userId, Guid shopId, Guid groupId, Guid? requestedPageId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var group = await RequireAsync(shopId, groupId, forChange: true, ct).ConfigureAwait(false);
            var pageId = requestedPageId ?? throw DomainException.Validation(new ValidationResult().Add("pageId", ProblemCodes.Fields.Required));
            var pages = await PagesAsync(group, ct).ConfigureAwait(false);
            var page = pages.FirstOrDefault(p => p.PageId == pageId)
                ?? throw new DomainException(ProblemCodes.GroupPageNotInGroup, 400, new Dictionary<string, object?> { ["pageId"] = pageId });
            if (PageReviewService.Individual(group).Any(i => i.PageId == pageId))
            {
                throw new DomainException(ProblemCodes.GroupPageNeedsIndividualFix, 409, new Dictionary<string, object?> { ["pageId"] = pageId });
            }

            var replacement = EnsureApprovable(group);
            var proposal = await ApplyAsync(userId, group, page, replacement, ct).ConfigureAwait(false)
                ?? throw new DomainException(ProblemCodes.GroupPageNeedsIndividualFix, 409, new Dictionary<string, object?> { ["pageId"] = pageId, ["reason"] = "text_not_found" });
            await FollowFindingsAsync(userId, group, ct).ConfigureAwait(false);
            await SaveAsync(ct).ConfigureAwait(false);
            await AuditAsync(AuditActions.GroupPageApproved, userId, group, new JsonObject { ["pageId"] = pageId.ToString("D") }, ct).ConfigureAwait(false);
            await db.Entry(proposal).ReloadAsync(ct).ConfigureAwait(false);
            return ProposalMapper.Dto(proposal);
        }, ct).ConfigureAwait(false);

    /// <summary>Takes the approval back while no proposal of the group is published.</summary>
    public async Task<FixGroupDto> UnapproveAsync(Guid userId, Guid shopId, Guid groupId, uint? version, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var group = await RequireAsync(shopId, groupId, forChange: true, ct).ConfigureAwait(false);
            db.Entry(group).Property(g => g.Version).OriginalValue = Concurrency.Require(version);
            var proposals = await db.FixProposals.Where(p => p.ShopId == shopId && p.GroupId == groupId).ToListAsync(ct).ConfigureAwait(false);
            if (group.Status is FixGroupStatus.Published or FixGroupStatus.PartiallyPublished || proposals.Any(p => p.Status == FixProposalStatus.Published))
            {
                throw new DomainException(ProblemCodes.GroupAlreadyPublished, 409);
            }

            foreach (var proposal in proposals.Where(p => p.Status == FixProposalStatus.Accepted))
            {
                proposal.Status = FixProposalStatus.Proposed;
                proposal.DecidedBy = null;
                proposal.DecidedAt = null;
                proposal.UpdatedAt = time.GetUtcNow();
            }

            foreach (var finding in await FindingsTrackedAsync(group, ct).ConfigureAwait(false))
            {
                if (finding.Status == FindingStatus.Approved)
                {
                    await transitions.ApplyAsync(finding, FindingStatus.Proposed, userId, ct, "group_unapproved").ConfigureAwait(false);
                }
            }

            await memory.SupersedeAsync(shopId, group.SegmentHash, ct).ConfigureAwait(false);
            group.Status = GroupValues.Missing(group).Count > 0 ? FixGroupStatus.NeedsValue : FixGroupStatus.Draft;
            group.ApprovedBy = null;
            group.ApprovedAt = null;
            group.LockedAt = null;
            group.UpdatedAt = time.GetUtcNow();
            await SaveAsync(ct).ConfigureAwait(false);
            await AuditAsync(AuditActions.GroupUnapproved, userId, group, new JsonObject { ["proposals"] = proposals.Count }, ct).ConfigureAwait(false);
            await db.Entry(group).ReloadAsync(ct).ConfigureAwait(false);
            return await DtoAsync(group, null, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    private sealed record GroupPage(Guid PageId, string? Title, Guid? VersionId);

    /// <summary>A change of the facts, the mode or the pages: the state and the recheck follow the wording (202 when it is checked again).</summary>
    private async Task<FixGroupDto> ChangeAsync(
        Guid userId, Guid shopId, Guid groupId, uint? version, string action, Func<FixGroup, Task<JsonObject>> change, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var group = await RequireAsync(shopId, groupId, forChange: true, ct).ConfigureAwait(false);
            db.Entry(group).Property(g => g.Version).OriginalValue = Concurrency.Require(version);
            EnsureEditable(group);
            var before = GroupValues.Replacement(group);
            var data = await change(group).ConfigureAwait(false);

            group.Status = GroupValues.Missing(group).Count > 0 ? FixGroupStatus.NeedsValue : FixGroupStatus.Draft;
            var after = GroupValues.Replacement(group);
            var queueRecheck = false;
            if (group.Mode == FixGroupMode.Remove)
            {
                // Removing the sentence needs no recheck: pages where another sentence follows it are in fit.individual.
                group.RecheckStatus = RecheckStatus.Ok;
                group.RecheckResult = null;
            }
            else if (after is null || GroupValues.Missing(group).Count > 0)
            {
                group.RecheckStatus = null;
                group.RecheckResult = null;
            }
            else if (after != before || group.RecheckStatus is null)
            {
                group.RecheckStatus = RecheckStatus.Pending;
                group.RecheckResult = null;
                queueRecheck = true;
            }

            group.UpdatedAt = time.GetUtcNow();
            await SaveAsync(ct).ConfigureAwait(false);
            if (queueRecheck)
            {
                var active = await FixRecheckHandler.ActiveJurisdictionsAsync(db, shopId, ct).ConfigureAwait(false);
                await queue.EnqueueAsync(FixJobs.Recheck(group.TenantId, shopId, FixJobs.TargetGroup, group.Id, ProposalText.Hash(after!), active), DbSql.Transaction(db), ct)
                    .ConfigureAwait(false);
            }

            await AuditAsync(action, userId, group, data, ct).ConfigureAwait(false);
            await db.Entry(group).ReloadAsync(ct).ConfigureAwait(false);
            return await DtoAsync(group, null, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    /// <summary>The e-shop (404), not only the sample when changing (409), then the group of it (404).</summary>
    private async Task<FixGroup> RequireAsync(Guid shopId, Guid groupId, bool forChange, CancellationToken ct)
    {
        var shop = await reader.ReadAsync(shopId, ct).ConfigureAwait(false);
        if (forChange)
        {
            SampleOnlyGuard.Ensure(shop);
        }

        var query = forChange ? db.FixGroups : db.FixGroups.AsNoTracking();
        return await query.FirstOrDefaultAsync(g => g.Id == groupId && g.ShopId == shopId, ct).ConfigureAwait(false)
            ?? throw new DomainException(ProblemCodes.GroupNotFound, 404);
    }

    private static void EnsureEditable(FixGroup group)
    {
        if (Locked.Contains(group.Status) || group.LockedAt is not null)
        {
            throw new DomainException(ProblemCodes.GroupLocked, 409, new Dictionary<string, object?> { ["status"] = FindingMapper.Text(group.Status) });
        }
    }

    /// <summary>The facts are filled and the wording passed its recheck (a removal needs none); returns the wording.</summary>
    private static string EnsureApprovable(FixGroup group)
    {
        var missing = GroupValues.Missing(group);
        if (missing.Count > 0)
        {
            throw new DomainException(ProblemCodes.GroupValueMissing, 409, new Dictionary<string, object?> { ["keys"] = missing });
        }

        var replacement = GroupValues.Replacement(group) ?? throw new DomainException(ProblemCodes.GroupCustomTextRequired, 409);
        if (group.Mode == FixGroupMode.Remove)
        {
            return replacement;
        }

        if (group.RecheckStatus == RecheckStatus.StillFinding)
        {
            var (jurisdictions, rules) = ProposalMapper.Failed(group.RecheckResult);
            throw new DomainException(ProblemCodes.GroupRecheckFailed, 409, new Dictionary<string, object?> { ["jurisdictions"] = jurisdictions, ["ruleIds"] = rules });
        }

        if (group.RecheckStatus != RecheckStatus.Ok)
        {
            throw new DomainException(ProblemCodes.GroupRecheckPending, 409);
        }

        return replacement;
    }

    /// <summary>
    /// The accepted proposal of the group on a page: the block of the current version with the sentence replaced (or removed);
    /// null when the sentence is not in the text of the page (fixed one by one then).
    /// </summary>
    private async Task<FixProposal?> ApplyAsync(Guid userId, FixGroup group, GroupPage page, string replacement, CancellationToken ct)
    {
        if (page.VersionId is not { } versionId || group.OriginalText is not { } sentence || tenant.TenantId is not { } tenantId)
        {
            return null;
        }

        var key = await db.PageVersions.AsNoTracking().Where(v => v.ShopId == group.ShopId && v.Id == versionId).Select(v => v.ExtractBlobKey).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        var text = await extracts.ReadAsync(tenantId, group.ShopId, key, ct).ConfigureAwait(false);
        var blockIndex = text?.Locate(sentence);
        if (text?.Block(blockIndex) is not { } block)
        {
            return null;
        }

        var normalized = PageText.Normalize(block);
        var target = PageText.Normalize(sentence);
        var proposed = normalized == target ? replacement : normalized.Replace(target, replacement, StringComparison.Ordinal);
        proposed = PageText.Normalize(proposed);
        var findingIds = await db.Findings.AsNoTracking().Where(f => f.ShopId == group.ShopId && f.SegmentHash == group.SegmentHash
                && db.FindingOccurrences.Any(o => o.FindingId == f.Id && o.PageId == page.PageId))
            .Select(f => f.Id).ToArrayAsync(ct).ConfigureAwait(false);
        var now = time.GetUtcNow();
        var proposal = await db.FixProposals.FirstOrDefaultAsync(p => p.ShopId == group.ShopId && p.GroupId == group.Id && p.PageId == page.PageId, ct).ConfigureAwait(false);
        if (proposal is null)
        {
            proposal = new FixProposal
            {
                ShopId = group.ShopId,
                PageId = page.PageId,
                PageVersionId = versionId,
                GroupId = group.Id,
                FindingIds = findingIds,
                Field = FixField.Block,
                OriginalText = block,
                ProposedText = proposed,
            };
            db.FixProposals.Add(proposal);
        }

        proposal.PageVersionId = versionId;
        proposal.BlockIndex = blockIndex;
        proposal.OriginalText = block;
        proposal.ProposedText = proposed;
        proposal.SelectedAlternative = null;
        proposal.EditedText = null;
        proposal.Placeholders = null;
        proposal.RecheckStatus = group.Mode == FixGroupMode.Remove ? RecheckStatus.Ok : group.RecheckStatus ?? RecheckStatus.Pending;
        proposal.RecheckResult = group.RecheckResult;
        proposal.FindingIds = findingIds.Length > 0 ? findingIds : proposal.FindingIds;
        proposal.Status = FixProposalStatus.Accepted;
        proposal.DecidedBy = userId;
        proposal.DecidedAt = now;
        proposal.UpdatedAt = now;
        return proposal;
    }

    /// <summary>
    /// The findings of the group follow the state machine: a finding whose every page has an accepted (or published) proposal
    /// becomes <c>approved</c>; one with a page still to fix one by one (or excluded) becomes <c>proposed</c> (from <c>open</c>
    /// or <c>needs_answer</c>) and waits (fail-closed: a sentence still on a page is not approved).
    /// </summary>
    private async Task FollowFindingsAsync(Guid userId, FixGroup group, CancellationToken ct)
    {
        await SaveAsync(ct).ConfigureAwait(false);
        foreach (var finding in await FindingsTrackedAsync(group, ct).ConfigureAwait(false))
        {
            var pages = await db.FindingOccurrences.AsNoTracking().Where(o => o.FindingId == finding.Id).Select(o => o.PageId).Distinct().ToListAsync(ct).ConfigureAwait(false);
            if (pages.Count == 0 && finding.PageId is { } own)
            {
                pages.Add(own);
            }

            var covered = await db.FixProposals.AsNoTracking()
                .Where(p => p.FindingIds.Contains(finding.Id) && (p.Status == FixProposalStatus.Accepted || p.Status == FixProposalStatus.Published))
                .Select(p => p.PageId).Distinct().ToListAsync(ct).ConfigureAwait(false);
            if (!pages.Any(covered.Contains))
            {
                // No page of this finding got the fix of the group (all excluded or fixed one by one): it stays as it is.
                continue;
            }

            var all = pages.All(covered.Contains);
            if (finding.Status is FindingStatus.Open or FindingStatus.NeedsAnswer)
            {
                await transitions.ApplyAsync(finding, FindingStatus.Proposed, userId, ct, "group_approved").ConfigureAwait(false);
            }

            if (all && finding.Status == FindingStatus.Proposed)
            {
                await transitions.ApplyAsync(finding, FindingStatus.Approved, userId, ct, "group_approved").ConfigureAwait(false);
            }
        }
    }

    private async Task<List<GroupPage>> PagesAsync(FixGroup group, CancellationToken ct)
    {
        List<Guid> ids;
        if (group.SegmentHash is { } hash)
        {
            var findingIds = db.Findings.Where(f => f.ShopId == group.ShopId && f.SegmentHash == hash).Select(f => f.Id);
            ids = await db.FindingOccurrences.AsNoTracking().Where(o => findingIds.Contains(o.FindingId)).Select(o => o.PageId).Distinct().ToListAsync(ct).ConfigureAwait(false);
            ids.AddRange(await db.Findings.AsNoTracking().Where(f => f.ShopId == group.ShopId && f.SegmentHash == hash && f.PageId != null).Select(f => f.PageId!.Value)
                .ToListAsync(ct).ConfigureAwait(false));
        }
        else
        {
            ids = await db.FixProposals.AsNoTracking().Where(p => p.ShopId == group.ShopId && p.GroupId == group.Id).Select(p => p.PageId).ToListAsync(ct).ConfigureAwait(false);
        }

        var distinct = ids.Distinct().ToArray();
        return await db.Pages.AsNoTracking().Where(p => p.ShopId == group.ShopId && distinct.Contains(p.Id))
            .OrderBy(p => p.Title).ThenBy(p => p.Id)
            .Select(p => new GroupPage(p.Id, p.Title, p.CurrentVersionId)).ToListAsync(ct).ConfigureAwait(false);
    }

    /// <summary>The pages the fix is written to: the pages of the group less the excluded and those of <c>fit.individual</c>.</summary>
    private static List<GroupPage> Included(FixGroup group, IReadOnlyList<GroupPage> pages)
    {
        var individual = PageReviewService.Individual(group).Select(i => i.PageId).ToHashSet();
        return pages.Where(p => !group.ExcludedPageIds.Contains(p.PageId) && !individual.Contains(p.PageId)).ToList();
    }

    private async Task<List<WorkFinding>> FindingsAsync(FixGroup group, CancellationToken ct) =>
        group.SegmentHash is not { } hash
            ? []
            : await db.Findings.AsNoTracking().Where(f => f.ShopId == group.ShopId && f.SegmentHash == hash)
                .Select(f => new WorkFinding(f.Id, f.RuleSetId, f.RuleId, f.Module, f.Scope, f.Status, f.SegmentHash, f.PageId, f.Text, f.Verdicts, f.Params,
                    f.Occurrences, EshopGuardDb.StrictnessRank(f.Verdicts)))
                .ToListAsync(ct).ConfigureAwait(false);

    private async Task<List<Finding>> FindingsTrackedAsync(FixGroup group, CancellationToken ct) =>
        group.SegmentHash is not { } hash
            ? []
            : await db.Findings.Where(f => f.ShopId == group.ShopId && f.SegmentHash == hash).ToListAsync(ct).ConfigureAwait(false);

    private static RecheckDto Recheck(FixGroup group)
    {
        if (group.RecheckStatus is not { } status)
        {
            return new RecheckDto("not_requested", null, null);
        }

        if (status != RecheckStatus.StillFinding)
        {
            return new RecheckDto(FindingMapper.Text(status), null, null);
        }

        var (jurisdictions, rules) = ProposalMapper.Failed(group.RecheckResult);
        return new RecheckDto(FindingMapper.Text(status), jurisdictions, rules);
    }

    private async Task<FixGroupDto> DtoAsync(FixGroup group, string? cursor, CancellationToken ct)
    {
        var pages = await PagesAsync(group, ct).ConfigureAwait(false);
        var individual = PageReviewService.Individual(group);
        var included = Included(group, pages);
        var findings = await FindingsAsync(group, ct).ConfigureAwait(false);
        var strictest = findings.OrderBy(f => f.Rank).FirstOrDefault();
        var values = GroupValues.Values(group);

        // The cursor carries the title and the id of the last page shown (the order of the list).
        Guid? after = Cursor.Decode(cursor) switch
        {
            null => null,
            [_, { ValueKind: JsonValueKind.String } id] when Guid.TryParse(id.GetString(), out var parsed) => parsed,
            _ => throw Cursor.Invalid(),
        };
        var rest = after is { } last
            ? pages.SkipWhile(p => p.PageId != last).Skip(1).ToList()
            : pages;
        var page = rest.Take(PageLimit).ToList();
        var next = rest.Count > PageLimit ? Cursor.Encode(page[^1].Title, page[^1].PageId) : null;

        return new FixGroupDto(
            group.Id, FindingMapper.Text(group.Kind), group.OriginalText, group.ReplacementTemplate, GroupValues.Replacement(group),
            GroupValues.Keys(group).Select(k => new PlaceholderDto(k, values.GetValueOrDefault(k))).ToList(),
            FindingMapper.Text(group.Mode), group.CustomText, strictest?.RuleId, strictest is null ? [] : VerdictStrictness.Verdicts(strictest.Verdicts),
            FindingMapper.Text(group.Status), Recheck(group),
            await SamplesAsync(group, included, ct).ConfigureAwait(false),
            new FixGroupFitDto(included.Count + group.ExcludedPageIds.Count(id => pages.Any(p => p.PageId == id) && individual.All(i => i.PageId != id)),
                individual.Select(i => new FixGroupIndividualDto(i.PageId, pages.FirstOrDefault(p => p.PageId == i.PageId)?.Title, i.ReasonCode, i.Params)).ToList()),
            new FixGroupPagesDto(pages.Count, group.ExcludedPageIds, page.Select(p => new FixGroupPageDto(p.PageId, p.Title)).ToList(), next),
            group.Version);
    }

    /// <summary>How the fix looks on a few included pages: the text right before and after the sentence.</summary>
    private async Task<List<FixGroupSampleDto>> SamplesAsync(FixGroup group, IReadOnlyList<GroupPage> included, CancellationToken ct)
    {
        var samples = new List<FixGroupSampleDto>();
        if (group.OriginalText is not { } sentence || tenant.TenantId is not { } tenantId)
        {
            return samples;
        }

        foreach (var page in included)
        {
            if (samples.Count == SampleCount)
            {
                break;
            }

            if (page.VersionId is not { } versionId)
            {
                continue;
            }

            var key = await db.PageVersions.AsNoTracking().Where(v => v.ShopId == group.ShopId && v.Id == versionId).Select(v => v.ExtractBlobKey).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            var text = await extracts.ReadAsync(tenantId, group.ShopId, key, ct).ConfigureAwait(false);
            var index = text?.Locate(sentence);
            if (text?.Block(index) is not { } block)
            {
                continue;
            }

            var normalized = PageText.Normalize(block);
            var at = normalized.IndexOf(PageText.Normalize(sentence), StringComparison.Ordinal);
            var (previous, following) = text.Around(index);
            var before = at > 0 ? normalized[..at].Trim() : previous;
            var after = at >= 0 && at + PageText.Normalize(sentence).Length < normalized.Length ? normalized[(at + PageText.Normalize(sentence).Length)..].Trim() : following;
            samples.Add(new FixGroupSampleDto(page.PageId, page.Title, before, after));
        }

        return samples;
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

    private Task AuditAsync(string action, Guid userId, FixGroup group, JsonObject data, CancellationToken ct)
    {
        data["shopId"] = group.ShopId.ToString("D");
        return audit.WriteAsync(new AuditEvent(action, group.TenantId, userId, "fix_group", group.Id.ToString("D"), data), ct);
    }
}
