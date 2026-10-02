using System.Text.Json.Nodes;
using EshopGuard.Core.Pipeline;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;

namespace EshopGuard.Jobs.Runs;

/// <summary>
/// What a run did not check, by reason (design of change 8, "Nezkontrolované a stav partial"; fail-closed: nothing is left out
/// silently). robots.txt, the URL filter, the page limit and pages that are not HTML are listed but do not make the run
/// partial (K rozhodnutí 7); pages that failed, were too large, not read in time, redirected away or into an internal network,
/// whose text was not loaded, segments Jev did not answer and documents that were not read make it <c>partial</c>.
/// </summary>
public sealed class UncheckedReport
{
    /// <summary>Reasons that make a run partial.</summary>
    public static readonly IReadOnlySet<string> PartialReasons = new HashSet<string>(StringComparer.Ordinal)
    {
        "failed", "too_large", "extract_timeout", "offsite_redirect", "ssrf_blocked", "not_loaded", "not_evaluated", "unread_documents",
    };

    private UncheckedReport(SortedDictionary<string, int> counts, List<UncheckedItem> items, int pagesChecked)
    {
        Counts = counts;
        Items = items;
        PagesChecked = pagesChecked;
    }

    /// <summary>Numbers by reason (only reasons with something).</summary>
    public IReadOnlyDictionary<string, int> Counts { get; }

    /// <summary>The full list (addresses and their reason), for <c>runs/{r}/unchecked.json.gz</c>.</summary>
    public IReadOnlyList<UncheckedItem> Items { get; }

    /// <summary>Pages whose text was read and checked.</summary>
    public int PagesChecked { get; }

    /// <summary>Something that makes the run partial.</summary>
    public bool IsPartial => Counts.Keys.Any(PartialReasons.Contains);

    internal static UncheckedReport Build(IReadOnlyList<RunUrlRow> urls, IReadOnlyList<CrawlCounters> counters, int notEvaluated, int sieveUnanswered, int pagesWithoutProfile)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var items = new List<UncheckedItem>();
        void Add(string reason, int count, IEnumerable<string>? listed = null)
        {
            if (count <= 0)
            {
                return;
            }

            counts[reason] = counts.GetValueOrDefault(reason) + count;
            items.AddRange((listed ?? []).Select(u => new UncheckedItem(u, reason)));
        }

        foreach (var group in urls.Where(u => u.State is not (RunUrlState.Extracted or RunUrlState.Fetched or RunUrlState.NotModified)).GroupBy(u => u.State))
        {
            var reason = group.Key switch
            {
                RunUrlState.Pending => "failed",
                RunUrlState.Gone => "failed",
                RunUrlState.OverLimit => "over_limit",
                _ => SnakeCaseEnumConverter<RunUrlState>.ToText(group.Key),
            };
            Add(reason, group.Count(), group.Select(u => u.Url));
        }

        var robotsListed = urls.Where(u => u.State == RunUrlState.RobotsBlocked).Select(u => u.Url).ToHashSet(StringComparer.Ordinal);
        var robots = counters.SelectMany(c => c.RobotsBlocked).Where(u => !robotsListed.Contains(u)).Distinct(StringComparer.Ordinal).ToList();
        Add("robots_blocked", robots.Count, robots);
        var ssrfListed = urls.Where(u => u.State == RunUrlState.SsrfBlocked).Select(u => u.Url).ToHashSet(StringComparer.Ordinal);
        var ssrf = counters.SelectMany(c => c.SsrfBlocked).Where(u => !ssrfListed.Contains(u)).Distinct(StringComparer.Ordinal).ToList();
        Add("ssrf_blocked", ssrf.Count, ssrf);
        Add("excluded", counters.Sum(c => c.ExcludedByFilter));
        Add("over_limit", counters.Sum(c => c.OverLimit + c.ProductOverLimit));
        var documents = counters.SelectMany(c => c.UncheckedDocuments).Select(d => d.Url).Distinct(StringComparer.Ordinal).ToList();
        Add("unread_documents", documents.Count, documents);
        Add("not_evaluated", notEvaluated);
        Add("sieve_unanswered", sieveUnanswered);
        Add("pages_without_profile", pagesWithoutProfile);
        var checkedPages = urls.Count(u => u.State == RunUrlState.Extracted);
        return new UncheckedReport(counts, items, checkedPages);
    }

    /// <summary>The numbers as JSON (<c>runs.stats.unchecked</c>).</summary>
    public JsonObject ToJson() => new(Counts.Select(c => KeyValuePair.Create(c.Key, (JsonNode?)c.Value)));

    /// <summary>
    /// The code of a run that checked no page: robots.txt forbids everything, every address leads into an internal network,
    /// the site was unreachable, or nothing it returned was a page.
    /// </summary>
    internal string FailureCode(IReadOnlyList<RunUrlRow> urls)
    {
        if (Counts.ContainsKey("robots_blocked") && urls.All(u => u.State == RunUrlState.RobotsBlocked))
        {
            return RunCodes.RobotsDisallowAll;
        }

        if (Counts.ContainsKey("ssrf_blocked") && !urls.Any(u => u.State is RunUrlState.Extracted or RunUrlState.NotLoaded or RunUrlState.Failed))
        {
            return RunCodes.TargetNotAllowed;
        }

        return urls.Any(u => u.State == RunUrlState.NotLoaded) || urls.Any(u => u.State is RunUrlState.NotHtml) ? RunCodes.NoHtmlPages : RunCodes.SiteUnreachable;
    }
}

/// <summary>One address that was not checked and why.</summary>
public sealed record UncheckedItem(string Url, string Reason);
