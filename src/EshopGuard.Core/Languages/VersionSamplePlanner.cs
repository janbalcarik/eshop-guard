using EshopGuard.Core.Pipeline;

namespace EshopGuard.Core.Languages;

/// <summary>What the sample of one version is made from: its product URLs from the sitemap (in its scope) and its mandatory pages.</summary>
/// <param name="Key">Key of the version (<see cref="VersionMarketPlanner.Key"/>).</param>
internal sealed record VersionSampleInput(string Key, string Language, bool IsMain, IReadOnlyList<SitemapEntry> Products, IReadOnlyList<Uri> MandatoryPages);

/// <summary>An address of the sample: the version it belongs to and why (<c>mandatory</c>, <c>product</c>).</summary>
public sealed record SampleUrl(string Url, string VersionKey, string Kind);

/// <summary>The sample of the free analysis of a shop and its versions.</summary>
public sealed record VersionSamplePlan(IReadOnlyList<SampleUrl> Urls, int Seed);

/// <summary>
/// Divides the sample of the analysis of versions (change 7, design section 6; without pairs since 2. 10. 2026): the mandatory
/// pages of each version, then random products of each version in turn for the rest of <c>markets.sample_pages</c>. The
/// versions are not compared, so no product is paired with its counterpart. The same seed gives the same plan.
/// </summary>
internal static class VersionSamplePlanner
{
    public const string Mandatory = "mandatory";
    public const string Product = "product";

    public static VersionSamplePlan Plan(IReadOnlyList<VersionSampleInput> versions, int budget, int seed)
    {
        var random = new Random(seed);
        var urls = new List<SampleUrl>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        bool Add(string url, string key, string kind)
        {
            if (urls.Count >= budget || !used.Add(url))
            {
                return false;
            }

            urls.Add(new SampleUrl(url, key, kind));
            return true;
        }

        foreach (var version in versions)
        {
            foreach (var page in version.MandatoryPages)
            {
                Add(page.AbsoluteUri, version.Key, Mandatory);
            }
        }

        // The rest evenly: one random product of each version in turn.
        var queues = versions.Select(v => (v.Key, Queue: new Queue<string>(Shuffle(v.Products.Select(p => p.Url.AbsoluteUri).ToList(), random)))).ToList();
        while (urls.Count < budget && queues.Any(q => q.Queue.Count > 0))
        {
            foreach (var (key, queue) in queues)
            {
                while (queue.Count > 0 && !Add(queue.Dequeue(), key, Product) && urls.Count < budget)
                {
                }
            }
        }

        return new VersionSamplePlan(urls, seed);
    }

    private static List<T> Shuffle<T>(List<T> items, Random random)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }

        return items;
    }
}
