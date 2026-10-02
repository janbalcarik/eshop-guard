using EshopGuard.Core.Crawl;
using EshopGuard.Core.Languages;
using EshopGuard.Core.Pipeline;

namespace EshopGuard.Jobs.Runs;

/// <summary>What the sitemap says about one checked version: its product pages and other pages (in its scope).</summary>
public sealed record VersionSitemap(string? Language, string BaseUrl, int ProductPages, int OtherPages)
{
    public const string File = "versions";

    internal static VersionSitemap Of(string? language, string baseUrl, VersionCrawlScope? scope, DiscoveryResult discovery)
    {
        var home = new Uri(baseUrl);
        var entries = discovery.SitemapEntries.Where(e => scope?.Contains(e.Url) ?? UrlTools.IsSameSite(e.Url, home)).DistinctBy(e => e.Url.AbsoluteUri).ToList();
        return new VersionSitemap(language, baseUrl, entries.Count(e => e.ProductHint), entries.Count(e => !e.ProductHint));
    }
}
