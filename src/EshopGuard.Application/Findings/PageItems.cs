using EshopGuard.Application.Contracts;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Fixes;

namespace EshopGuard.Application.Findings;

/// <summary>
/// An item of „Opravy“ by pages (change 11, AD 3): a page with its findings, the whole e-shop's template
/// (<see cref="PageItems.SiteTemplate"/>: findings of template groups) or the whole e-shop's obligations
/// (<see cref="PageItems.SiteObligations"/>: findings and questions of the whole site).
/// </summary>
public sealed class WorkItem
{
    public required string Kind { get; init; }

    public WorkPage? Page { get; init; }

    public required IReadOnlyList<WorkFinding> Findings { get; init; }

    public required IReadOnlyList<Question> OpenQuestions { get; init; }

    public required IReadOnlyList<FixProposal> Proposals { get; init; }

    public required IReadOnlyList<FixGroup> Groups { get; init; }

    /// <summary>Stable key of the item for the cursor: the page id, or the kind of a whole-site item.</summary>
    public string Key => Page?.Id.ToString("N") ?? Kind;

    /// <summary>The item has a finding that is not resolved (it counts into „4 z 28“).</summary>
    public bool HasFinding => Findings.Any(f => f.Status != FindingStatus.Resolved);

    public bool NeedsAnswer => OpenQuestions.Count > 0;

    public bool ToApprove => !NeedsAnswer && (Proposals.Any(p => ShopWork.IsReady(p)) || Groups.Any(g => ShopWork.IsReady(g)));

    public bool ToResolve => Findings.Any(f => FindingStatusMachine.Unresolved.Contains(f.Status));

    public bool Published =>
        !Findings.Any(f => f.Status is FindingStatus.Open or FindingStatus.NeedsAnswer or FindingStatus.Proposed or FindingStatus.Approved)
        && Findings.Any(f => FindingStatusMachine.Fixed.Contains(f.Status));

    /// <summary>
    /// <c>needs_answer</c>, <c>to_approve</c>, <c>open</c>, <c>approved</c> (accepted, waiting for publication),
    /// <c>published</c>, or <c>decided</c> (every finding kept or dismissed).
    /// </summary>
    public string Status =>
        NeedsAnswer ? "needs_answer"
        : ToApprove ? "to_approve"
        : ToResolve ? "open"
        : Findings.Any(f => f.Status == FindingStatus.Approved) ? "approved"
        : Published ? "published"
        : "decided";

    /// <summary>Findings that still count (not resolved).</summary>
    public IEnumerable<WorkFinding> Current => Findings.Where(f => f.Status != FindingStatus.Resolved);

    /// <summary>Rank of the strictest current finding (smaller is stricter).</summary>
    public short Rank => Current.Select(f => f.Rank).DefaultIfEmpty(VerdictStrictness.None).Min();

    /// <summary>Number of current findings.</summary>
    public int Count => Current.Count();

    /// <summary>Sort title: the page title, a whole-site item first among equals.</summary>
    public string SortTitle => Page is null ? "\u0001" + Kind : Page.Title ?? Page.Path ?? Page.Url;

    public bool InTab(string tab) => tab switch
    {
        PageItems.TabToResolve => ToResolve,
        PageItems.TabToApprove => ToApprove,
        PageItems.TabNeedsAnswer => NeedsAnswer,
        PageItems.TabPublished => Published,
        _ => false,
    };
}

/// <summary>Builds and orders the items of „Opravy“ from <see cref="ShopWork"/>.</summary>
public static class PageItems
{
    public const string KindPage = "page";
    public const string SiteTemplate = "site_template";
    public const string SiteObligations = "site_obligations";

    public const string TabToResolve = "to_resolve";
    public const string TabToApprove = "to_approve";
    public const string TabNeedsAnswer = "needs_answer";
    public const string TabPublished = "published";

    public static readonly string[] TabNames = [TabToResolve, TabToApprove, TabNeedsAnswer, TabPublished];

    /// <summary>
    /// Every item with a finding. With <paramref name="visibleOnly"/> only the findings the merchant may see in detail make
    /// items (an e-shop with only the sample); the tabs count every finding.
    /// </summary>
    public static List<WorkItem> Build(ShopWork work, bool visibleOnly)
    {
        ArgumentNullException.ThrowIfNull(work);
        var findings = work.Findings.Where(f => !visibleOnly || work.IsVisible(f.Id)).ToList();
        var templateHashes = work.TemplateHashes;
        var openQuestions = work.Questions.Where(q => q.Status == QuestionStatus.Open).ToList();
        var proposals = work.LiveProposals.ToList();
        var items = new List<WorkItem>();

        var template = findings.Where(f => f.Scope != FindingScope.Site && f.SegmentHash is { } hash && templateHashes.Contains(hash)).ToList();
        if (template.Count > 0)
        {
            var ids = template.Select(f => f.Id).ToHashSet();
            items.Add(new WorkItem
            {
                Kind = SiteTemplate,
                Findings = template,
                OpenQuestions = openQuestions.Where(q => q.FindingId is { } id && ids.Contains(id)).ToList(),
                Proposals = [],
                Groups = work.Groups.Where(g => g.Kind == FixGroupKind.Template).ToList(),
            });
        }

        var site = findings.Where(f => f.Scope == FindingScope.Site).ToList();
        var siteQuestions = openQuestions.Where(q => q.Scope == QuestionScope.Site).ToList();
        if (site.Count > 0)
        {
            var ids = site.Select(f => f.Id).ToHashSet();
            items.Add(new WorkItem
            {
                Kind = SiteObligations,
                Findings = site,
                OpenQuestions = siteQuestions.Concat(openQuestions.Where(q => q.Scope != QuestionScope.Site && q.FindingId is { } id && ids.Contains(id))).ToList(),
                Proposals = [],
                Groups = work.Groups.Where(g => g.Kind == FixGroupKind.SiteObligation).ToList(),
            });
        }

        var byPage = new Dictionary<Guid, List<WorkFinding>>();
        foreach (var finding in findings.Where(f => f.Scope != FindingScope.Site && !(f.SegmentHash is { } hash && templateHashes.Contains(hash))))
        {
            foreach (var page in work.PagesOf.GetValueOrDefault(finding.Id) ?? [])
            {
                if (!byPage.TryGetValue(page, out var list))
                {
                    byPage[page] = list = [];
                }

                list.Add(finding);
            }
        }

        foreach (var (pageId, pageFindings) in byPage)
        {
            if (!work.Pages.TryGetValue(pageId, out var page))
            {
                continue;
            }

            var ids = pageFindings.Select(f => f.Id).ToHashSet();
            var pageProposals = proposals.Where(p => p.PageId == pageId).ToList();
            items.Add(new WorkItem
            {
                Kind = KindPage,
                Page = page,
                Findings = pageFindings,
                OpenQuestions = openQuestions.Where(q => q.Scope != QuestionScope.Site && q.FindingId is { } id && ids.Contains(id)).ToList(),
                Proposals = pageProposals,
                Groups = work.Groups.Where(g => pageProposals.Any(p => p.GroupId == g.Id)).ToList(),
            });
        }

        items.RemoveAll(i => !i.HasFinding);
        items.Sort(Compare);
        return items;
    }

    /// <summary>The order of the lists: the strictest finding, the number of findings (more first), the title, the key.</summary>
    public static int Compare(WorkItem a, WorkItem b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var rank = a.Rank.CompareTo(b.Rank);
        if (rank != 0)
        {
            return rank;
        }

        var count = b.Count.CompareTo(a.Count);
        if (count != 0)
        {
            return count;
        }

        var title = string.Compare(a.SortTitle, b.SortTitle, StringComparison.Ordinal);
        return title != 0 ? title : string.CompareOrdinal(a.Key, b.Key);
    }

    /// <summary>The items of a version of the language; whole-site items belong to every version.</summary>
    public static IEnumerable<WorkItem> InLanguage(IEnumerable<WorkItem> items, string? language) =>
        string.IsNullOrEmpty(language) ? items : items.Where(i => i.Page is null || i.Page.Language == language);

    public static PageTabsDto Tabs(IReadOnlyList<WorkItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new PageTabsDto(
            items.Count(i => i.ToResolve),
            items.Count(i => i.ToApprove),
            items.Count(i => i.NeedsAnswer),
            items.Count(i => i.Published),
            items.Count);
    }
}
