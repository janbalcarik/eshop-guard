using EshopGuard.Jobs.Fixes;
using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Application.Fixes;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Shops;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Content;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Findings;

/// <summary>A finding as the lists need it, with the rank of its strictest verdict and its group (text, assess, verify).</summary>
public sealed record WorkFinding(
    Guid Id, Guid RuleSetId, string RuleId, string Module, FindingScope Scope, FindingStatus Status, long? SegmentHash, Guid? PageId,
    string? Text, JsonDocument Verdicts, JsonDocument? Params, int Occurrences, short Rank)
{
    /// <summary>The group of the strictest verdict, as returned by the API.</summary>
    public string Group => VerdictStrictness.Strictest(Verdicts)?.Checkability ?? "verify";
}

/// <summary>A page of the e-shop as the lists need it.</summary>
public sealed record WorkPage(
    Guid Id, string? Title, string? Path, string Url, string? Language, PageType? Type, PageSource Source, string? ExternalId, Guid? CurrentVersionId);

/// <summary>
/// What the lists of „Opravy“, „Nálezy“ and the overview read about one e-shop (change 11): its findings, the pages they occur
/// on, questions, proposals of fixes and groups. For an e-shop with only the free sample (<c>draft</c>, <c>sample</c>; K
/// rozhodnutí 4, AD 14) only the 5 findings of the summary of the sample are <see cref="Visible"/>; the rest count only.
/// </summary>
public sealed class ShopWork
{
    public required Shop Shop { get; init; }

    public required IReadOnlyList<WorkFinding> Findings { get; init; }

    /// <summary>Pages of every finding (its occurrences, or its page when it has none).</summary>
    public required IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> PagesOf { get; init; }

    public required IReadOnlyDictionary<Guid, WorkPage> Pages { get; init; }

    public required IReadOnlyList<Question> Questions { get; init; }

    public required IReadOnlyList<FixProposal> Proposals { get; init; }

    public required IReadOnlyList<FixGroup> Groups { get; init; }

    /// <summary>Ids of the findings the merchant may see in detail; null = all of them.</summary>
    public IReadOnlySet<Guid>? Visible { get; init; }

    /// <summary>True for an e-shop with only the free sample: decisions are <c>409 shop.sample_only</c>.</summary>
    public bool SampleOnly => Shop.Status is ShopStatus.Draft or ShopStatus.Sample;

    public bool IsVisible(Guid findingId) => Visible is null || Visible.Contains(findingId);

    /// <summary>Segment hashes of the template groups: their findings belong to „Celý e-shop: šablóna“, not to the pages.</summary>
    public IReadOnlySet<long> TemplateHashes => Groups.Where(g => g.Kind == FixGroupKind.Template && g.SegmentHash is not null)
        .Select(g => g.SegmentHash!.Value).ToHashSet();

    /// <summary>The proposals of the current version of their page, or accepted and published ones of any version.</summary>
    public IEnumerable<FixProposal> LiveProposals => Proposals.Where(p =>
        p.Status is FixProposalStatus.Accepted or FixProposalStatus.Published
        || (p.Status is FixProposalStatus.Proposed or FixProposalStatus.Edited && (!Pages.TryGetValue(p.PageId, out var page) || page.CurrentVersionId is null || page.CurrentVersionId == p.PageVersionId)));

    /// <summary>A proposal ready to be approved: proposed or edited and its current text passed the recheck.</summary>
    public static bool IsReady(FixProposal proposal) =>
        proposal.Status is FixProposalStatus.Proposed or FixProposalStatus.Edited && ProposalText.Recheck(proposal) == RecheckStatus.Ok;

    /// <summary>A group ready to be approved: not decided, every fact filled and, unless it removes the sentence, its recheck passed.</summary>
    public static bool IsReady(FixGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (group.Status is FixGroupStatus.Approved or FixGroupStatus.Published or FixGroupStatus.PartiallyPublished or FixGroupStatus.Rejected)
        {
            return false;
        }

        if (group.Mode == FixGroupMode.Remove)
        {
            return true;
        }

        return GroupValues.Missing(group).Count == 0 && group.RecheckStatus == RecheckStatus.Ok;
    }
}

/// <summary>Loads <see cref="ShopWork"/> in the open transaction of the tenant.</summary>
public sealed class ShopWorkLoader(EshopGuardDb db, ShopReader reader)
{
    public async Task<ShopWork> LoadAsync(Guid shopId, CancellationToken ct)
    {
        var shop = await reader.ReadAsync(shopId, ct).ConfigureAwait(false);
        var findings = await db.Findings.AsNoTracking().Where(f => f.ShopId == shopId)
            .Select(f => new WorkFinding(f.Id, f.RuleSetId, f.RuleId, f.Module, f.Scope, f.Status, f.SegmentHash, f.PageId, f.Text, f.Verdicts, f.Params,
                f.Occurrences, EshopGuardDb.StrictnessRank(f.Verdicts)))
            .ToListAsync(ct).ConfigureAwait(false);
        var occurrences = await db.FindingOccurrences.AsNoTracking().Where(o => o.ShopId == shopId)
            .Select(o => new { o.FindingId, o.PageId }).ToListAsync(ct).ConfigureAwait(false);
        var pagesOf = occurrences.GroupBy(o => o.FindingId).ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(o => o.PageId).Distinct().ToList());
        foreach (var finding in findings.Where(f => f.PageId is not null && !pagesOf.ContainsKey(f.Id)))
        {
            pagesOf[finding.Id] = [finding.PageId!.Value];
        }

        var proposals = await db.FixProposals.AsNoTracking().Where(p => p.ShopId == shopId && p.Status != FixProposalStatus.Superseded)
            .ToListAsync(ct).ConfigureAwait(false);
        var pageIds = pagesOf.Values.SelectMany(p => p).Concat(proposals.Select(p => p.PageId)).Distinct().ToList();
        var pages = await db.Pages.AsNoTracking().Where(p => p.ShopId == shopId && pageIds.Contains(p.Id))
            .Select(p => new WorkPage(p.Id, p.Title, p.Path, p.Url, p.Language, p.PageType, p.Source, p.ExternalId, p.CurrentVersionId))
            .ToDictionaryAsync(p => p.Id, ct).ConfigureAwait(false);
        var questions = await db.Questions.AsNoTracking().Where(q => q.ShopId == shopId).ToListAsync(ct).ConfigureAwait(false);
        var groups = await db.FixGroups.AsNoTracking().Where(g => g.ShopId == shopId).ToListAsync(ct).ConfigureAwait(false);
        IReadOnlySet<Guid>? visible = null;
        if (shop.Status is ShopStatus.Draft or ShopStatus.Sample)
        {
            visible = await SampleTopFindingsAsync(shopId, ct).ConfigureAwait(false);
        }

        return new ShopWork
        {
            Shop = shop,
            Findings = findings,
            PagesOf = pagesOf,
            Pages = pages,
            Questions = questions,
            Proposals = proposals,
            Groups = groups,
            Visible = visible,
        };
    }

    /// <summary>
    /// The 5 findings of the summary of the newest free sample (<c>runs.stats.sample.top_finding_ids</c>, change 8): what an
    /// e-shop with only the sample shows in detail.
    /// </summary>
    private async Task<IReadOnlySet<Guid>> SampleTopFindingsAsync(Guid shopId, CancellationToken ct)
    {
        var stats = await db.Runs.AsNoTracking().Where(r => r.ShopId == shopId && r.Kind == RunKind.FreeSample)
            .OrderByDescending(r => r.CreatedAt).Select(r => r.Stats).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        var ids = new HashSet<Guid>();
        if (stats is not null && JsonNode.Parse(stats.RootElement.GetRawText())?["sample"]?["top_finding_ids"] is JsonArray top)
        {
            foreach (var node in top)
            {
                if (Guid.TryParse((string?)node, out var id))
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }
}

/// <summary>Refuses a decision for an e-shop with only the free sample (AD 14).</summary>
public static class SampleOnlyGuard
{
    public static void Ensure(Shop shop)
    {
        ArgumentNullException.ThrowIfNull(shop);
        if (IsSampleOnly(shop))
        {
            throw new DomainException(ProblemCodes.ShopSampleOnly, 409);
        }
    }

    public static bool IsSampleOnly(Shop shop) => shop.Status is ShopStatus.Draft or ShopStatus.Sample;
}
