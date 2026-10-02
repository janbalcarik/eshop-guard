using EshopGuard.Core.Classify;
using EshopGuard.Core.Options;
using EshopGuard.Core.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Languages;

/// <summary>The pages of a crawl of a version and its sitemap entries.</summary>
internal sealed record VersionPages(DiscoveryResult Discovery, IReadOnlyList<ExtractedPageRecord> Pages, int Requests);

/// <summary>
/// Crawls one language version with the steps of change 5 in its scope (<see cref="VersionCrawlScope"/>): robots.txt and the
/// sitemap of its host, only its addresses, its cookies and <c>Accept-Language</c>, every page with its language. Either the
/// whole version up to a page limit, or only the given addresses (the sample of the analysis of versions).
/// </summary>
internal sealed class VersionCrawler(
    DiscoveryStep discoveryStep, FetchStep fetchStep, PageClassifier classifier, IOptions<EshopGuardOptions> options, ILogger<VersionCrawler> logger)
{
    private CrawlOptions Crawl => options.Value.Crawl;

    /// <summary>robots.txt, the sitemap entries in the scope and the frontier of the version.</summary>
    public Task<DiscoveryResult> DiscoverAsync(SiteScope site, int maxPages, CancellationToken ct) =>
        discoveryStep.DiscoverAsync(new DiscoveryInput(site, new CrawlLimits(maxPages, maxPages, [], [], null)), null, ct);

    /// <summary>The version from its home page, at most <paramref name="maxPages"/> pages.</summary>
    public async Task<VersionPages> CrawlAsync(SiteScope site, int maxPages, CancellationToken ct)
    {
        var discovery = await DiscoverAsync(site, maxPages, ct);
        var pages = await RunAsync(site, discovery, discovery.Frontier, discovery.Pace, ct);
        return new VersionPages(discovery, pages, discovery.Frontier.Counters.Requests);
    }

    /// <summary>Only the given addresses of the version (each must be in its scope), without its home page and without following links.</summary>
    public async Task<IReadOnlyList<ExtractedPageRecord>> FetchAsync(SiteScope site, DiscoveryResult discovery, IReadOnlyList<Uri> urls, CancellationToken ct)
    {
        var state = new UrlFrontierState
        {
            Home = site.Home,
            Scope = site.Version,
            Cookies = site.Version?.Cookies.ToDictionary(c => c.Key, c => c.Value) ?? [],
            HomeDone = true,
            MaxPages = urls.Count,
            MaxProducts = urls.Count,
            MaxLinkDepth = 0,
        };
        var frontier = new UrlFrontier(state, discovery.Robots.ToRobots(), Crawl.ExcludeUrlPatterns, classifier, logger);
        foreach (var url in urls)
        {
            frontier.Consider(url, depth: 0, productHint: false, foundOn: "sample");
        }

        return await RunAsync(site, discovery, state, discovery.Pace, ct);
    }

    private async Task<List<ExtractedPageRecord>> RunAsync(SiteScope site, DiscoveryResult discovery, UrlFrontierState frontier, PaceState pace, CancellationToken ct)
    {
        var pages = new List<ExtractedPageRecord>();
        var maxDuration = TimeSpan.FromSeconds(Math.Max(1, Crawl.FetchBatchMaxSeconds));
        while (!frontier.Stopped)
        {
            var batch = await fetchStep.FetchBatchAsync(
                new FetchBatchInput(site, discovery.Robots, frontier, pace, Math.Max(1, Crawl.FetchBatchMaxPages), maxDuration, ExtractInline: true), null, ct);
            pace = batch.Pace;
            pages.AddRange(batch.Pages.Select(p => p.Extract).OfType<ExtractedPageRecord>().Where(p => p.Status == ExtractionStatus.Ok));
            if (batch.FrontierExhausted)
            {
                break;
            }
        }

        return pages;
    }
}
