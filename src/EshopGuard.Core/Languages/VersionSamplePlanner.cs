using EshopGuard.Core.Crawl;
using EshopGuard.Core.Pipeline;

namespace EshopGuard.Core.Languages;

/// <summary>What the sample of one version is made from: its product URLs from the sitemap (in its scope) and its mandatory pages.</summary>
/// <param name="Key">Key of the version (<see cref="VersionMarketPlanner.Key"/>).</param>
internal sealed record VersionSampleInput(string Key, string Language, bool IsMain, IReadOnlyList<SitemapEntry> Products, IReadOnlyList<Uri> MandatoryPages);

/// <summary>An address of the sample: the version it belongs to and why (<c>pair</c>, <c>mandatory</c>, <c>product</c>).</summary>
public sealed record SampleUrl(string Url, string VersionKey, string Kind);

/// <summary>Two addresses of the same product in two versions.</summary>
public sealed record SamplePair(string MainUrl, string OtherUrl, string OtherKey);

/// <summary>The sample of the analysis of versions.</summary>
/// <param name="PairingMode"><c>hreflang</c> when pairs come from alternates, otherwise <c>identifiers</c> (EAN, SKU after the download).</param>
public sealed record VersionSamplePlan(IReadOnlyList<SampleUrl> Urls, IReadOnlyList<SamplePair> Pairs, string PairingMode, int Seed);

/// <summary>
/// Divides the sample of the analysis of versions (change 7, design section 6): about <c>markets.paired_products</c> products in
/// the main version and another one (alternates of the sitemap), the mandatory pages of each version, and random products of
/// each version for the rest of <c>markets.sample_pages</c>. The same seed gives the same plan.
/// </summary>
internal static class VersionSamplePlanner
{
    public const string ModeHreflang = "hreflang";
    public const string ModeIdentifiers = "identifiers";
    public const string ModeSentenceOverlap = "sentence_overlap";

    public static VersionSamplePlan Plan(IReadOnlyList<VersionSampleInput> versions, int budget, int pairs, int seed)
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

        var main = versions.FirstOrDefault(v => v.IsMain);
        var others = versions.Where(v => !v.IsMain).ToList();
        var samplePairs = new List<SamplePair>();
        if (main is not null && others.Count > 0)
        {
            var perVersion = Math.Max(1, pairs / others.Count);
            foreach (var other in others)
            {
                var otherProducts = other.Products.Select(p => p.Url.AbsoluteUri).ToHashSet(StringComparer.Ordinal);
                var candidates = Shuffle(main.Products.ToList(), random)
                    .Select(p => (Main: p.Url.AbsoluteUri, Other: p.Alternates.FirstOrDefault(a => Markets.LanguageTags.SamePrimary(a.Language, other.Language) && otherProducts.Contains(a.Url.AbsoluteUri))?.Url.AbsoluteUri))
                    .Where(p => p.Other is not null)
                    .Take(perVersion)
                    .ToList();
                foreach (var (mainUrl, otherUrl) in candidates)
                {
                    if (urls.Count + 2 <= budget && Add(mainUrl, main.Key, "pair") && Add(otherUrl!, other.Key, "pair"))
                    {
                        samplePairs.Add(new SamplePair(mainUrl, otherUrl!, other.Key));
                    }
                }
            }
        }

        foreach (var version in versions)
        {
            foreach (var page in version.MandatoryPages)
            {
                Add(page.AbsoluteUri, version.Key, "mandatory");
            }
        }

        // The rest evenly: one random product of each version in turn.
        var queues = versions.Select(v => (v.Key, Queue: new Queue<string>(Shuffle(v.Products.Select(p => p.Url.AbsoluteUri).ToList(), random)))).ToList();
        while (urls.Count < budget && queues.Any(q => q.Queue.Count > 0))
        {
            foreach (var (key, queue) in queues)
            {
                while (queue.Count > 0 && !Add(queue.Dequeue(), key, "product") && urls.Count < budget)
                {
                }
            }
        }

        return new VersionSamplePlan(urls, samplePairs, samplePairs.Count > 0 ? ModeHreflang : ModeIdentifiers, seed);
    }

    /// <summary>
    /// Pairs found on the downloaded product pages of the main version (their hreflang): at most <paramref name="perVersion"/>
    /// for each other version. The address of the other version replaces one of its random products, so the size of the
    /// sample stays; the main address becomes a pair.
    /// </summary>
    public static VersionSamplePlan WithPagePairs(VersionSamplePlan plan, string mainKey, IReadOnlyList<SamplePair> found, int perVersion)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(found);
        var urls = plan.Urls.ToList();
        var pairs = plan.Pairs.ToList();
        foreach (var group in found.GroupBy(p => p.OtherKey))
        {
            foreach (var pair in group.DistinctBy(p => p.MainUrl).DistinctBy(p => p.OtherUrl).Take(perVersion))
            {
                var mainIndex = urls.FindIndex(u => u.VersionKey == mainKey && Same(u.Url, pair.MainUrl));
                if (mainIndex >= 0)
                {
                    urls[mainIndex] = urls[mainIndex] with { Kind = "pair" };
                }

                var otherIndex = urls.FindIndex(u => u.VersionKey == pair.OtherKey && Same(u.Url, pair.OtherUrl));
                if (otherIndex >= 0)
                {
                    urls[otherIndex] = urls[otherIndex] with { Kind = "pair" };
                }
                else
                {
                    var replaced = urls.FindLastIndex(u => u.VersionKey == pair.OtherKey && u.Kind == "product");
                    if (replaced < 0)
                    {
                        continue;
                    }

                    urls[replaced] = new SampleUrl(pair.OtherUrl, pair.OtherKey, "pair");
                }

                pairs.Add(pair);
            }
        }

        return plan with { Urls = urls, Pairs = pairs, PairingMode = pairs.Count > 0 ? ModeHreflang : plan.PairingMode };
    }

    private static bool Same(string a, string b) =>
        string.Equals(UrlTools.Normalize(new Uri(a)).AbsoluteUri, UrlTools.Normalize(new Uri(b)).AbsoluteUri, StringComparison.Ordinal);

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
