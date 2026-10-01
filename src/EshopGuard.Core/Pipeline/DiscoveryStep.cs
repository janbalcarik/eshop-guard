using System.Xml;
using EshopGuard.Core.Classify;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Rules;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Pipeline;

/// <summary>
/// Finds out what a crawl of the site will download, before any page: robots.txt (with Crawl-delay), the sitemaps and
/// sitemap indexes up to depth 3 and at most <c>crawl.max_sitemap_urls</c> URLs; without sitemap URLs the crawl follows
/// links from the home page. Returns the frontier ready for the first batch of <see cref="FetchStep"/>.
/// </summary>
internal sealed class DiscoveryStep(
    IPageFetcher fetcher,
    PageClassifier classifier,
    IOptions<EshopGuardOptions> options,
    ILogger<DiscoveryStep> logger)
{
    private const int MaxSitemapDepth = 3;

    private CrawlOptions Crawl => options.Value.Crawl;

    private IPageFetcher Fetcher => fetcher;

    private ILogger Logger => logger;

    public async Task<DiscoveryResult> DiscoverAsync(DiscoveryInput input, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var crawl = Crawl;
        var limits = input.Limits;
        var home = input.Site.Home;
        var state = new UrlFrontierState
        {
            Home = home,
            MaxPages = limits.MaxPages,
            MaxProducts = limits.SampleProducts,
            MaxLinkDepth = crawl.MaxLinkDepth,
            Include = limits.Include,
            Exclude = limits.Exclude,
        };

        // --rate sets a fixed pace; otherwise it adapts between the start rate and the maximum.
        var gate = limits.FixedRate is { } fixedRate
            ? new AdaptiveGate(fixedRate, fixedRate, adaptive: false)
            : new AdaptiveGate(crawl.RequestsPerSecond, crawl.MaxRequestsPerSecond, adaptive: true);
        var run = new Run(this, state, gate, ct);
        progress?.Report(new UrlFrontier(state, RobotsTxt.AllowAll, [], classifier, logger).Progress(ScanStage.Discovery, null));

        var (robotsSnapshot, blocked) = await run.LoadRobotsAsync(home);
        var robots = robotsSnapshot.ToRobots();
        var frontier = new UrlFrontier(state, robots, crawl.ExcludeUrlPatterns, classifier, logger);
        var sitemapEntries = new List<SitemapEntry>();
        DiscoveryResult Result() => new(robotsSnapshot, sitemapEntries, state, gate.ToState(), run.Warnings, blocked);

        if (robots.CrawlDelay is { } delay)
        {
            state.Counters.CrawlDelaySeconds = delay.TotalSeconds;
            gate.Limit(1 / delay.TotalSeconds);
            logger.LogInformation("robots.txt asks for Crawl-delay {Delay} s, pace at most {Rate:0.00}/s", delay.TotalSeconds, 1 / delay.TotalSeconds);
        }

        if (robotsSnapshot.Status == RobotsSnapshot.DisallowAll)
        {
            // The warning about the unreachable (or blocked) robots.txt is already recorded.
            state.Stopped = true;
            return Result();
        }

        if (!robots.IsAllowed(home))
        {
            state.Counters.RobotsBlocked.Add(home.AbsoluteUri);
            run.Warnings.Add(new ScanWarning(EngineCodes.RobotsHomeDisallowed, NoteParams.None));
            state.Stopped = true;
            return Result();
        }

        var sitemapUrls = robots.Sitemaps
            .Select(s => UrlTools.TryResolve(s, home))
            .OfType<Uri>()
            .Where(frontier.IsSameSite)
            .ToList();
        var explicitSitemaps = sitemapUrls.Count > 0;
        if (!explicitSitemaps)
        {
            sitemapUrls.Add(new Uri(home, "/sitemap.xml"));
        }

        sitemapEntries.AddRange(await run.ReadSitemapsAsync(sitemapUrls, explicitSitemaps, frontier));
        foreach (var entry in sitemapEntries)
        {
            frontier.Consider(entry.Url, depth: 0, entry.ProductHint, foundOn: "sitemap");
        }

        state.LinkMode = sitemapEntries.Count == 0;
        if (state.LinkMode)
        {
            logger.LogInformation("No sitemap URLs found, following links from the home page up to depth {Depth}", crawl.MaxLinkDepth);
        }

        return Result();
    }

    internal static string ProductToken(string userAgent)
    {
        var agent = userAgent.Trim();
        var end = agent.IndexOfAny(['/', ' ']);
        return end > 0 ? agent[..end] : agent;
    }

    /// <summary>Why a download failed: the error, the HTTP status, or the note that there was no answer.</summary>
    internal static object DescribeFailure(FetchResponse response) =>
        response.Error is { } error ? error
        : response.StatusCode > 0 ? $"HTTP {response.StatusCode}"
        : new FindingNote(EngineCodes.NoResponse, NoteParams.None);

    /// <summary>One discovery: the requests share the pace and the counters.</summary>
    private sealed class Run(DiscoveryStep owner, UrlFrontierState state, AdaptiveGate gate, CancellationToken ct)
    {
        public List<ScanWarning> Warnings { get; } = [];

        private ILogger Log => owner.Logger;

        public async Task<(RobotsSnapshot Robots, bool HomeBlocked)> LoadRobotsAsync(Uri home)
        {
            var url = new Uri(home, "/robots.txt");
            var token = ProductToken(owner.Crawl.UserAgent);
            var response = await FetchFollowingRedirectsAsync(url, home);
            if (response.IsSuccess)
            {
                return (new RobotsSnapshot(RobotsSnapshot.Parsed, HtmlDecoding.Decode(response.Body!, response.Charset), token), false);
            }

            if (response.Error == SsrfGuard.Error)
            {
                state.Counters.SsrfBlocked.Add(home.AbsoluteUri);
                Log.LogWarning("The site {Url} leads into an internal or local network, nothing is downloaded", home);
                Warnings.Add(new ScanWarning(EngineCodes.SsrfBlocked, NoteParams.Of(("url", home.AbsoluteUri))));
                return (new RobotsSnapshot(RobotsSnapshot.DisallowAll, null, token), true);
            }

            if (response.StatusCode is >= 400 and < 500)
            {
                Log.LogInformation("robots.txt not found ({Status}), everything is allowed", response.StatusCode);
                return (new RobotsSnapshot(RobotsSnapshot.AllowAll, null, token), false);
            }

            Log.LogWarning("robots.txt unreachable ({Status}, {Error}), nothing is allowed", response.StatusCode, response.Error);
            Warnings.Add(new ScanWarning(EngineCodes.RobotsUnreachable, NoteParams.Of(("reason", DescribeFailure(response)))));
            return (new RobotsSnapshot(RobotsSnapshot.DisallowAll, null, token), false);
        }

        public async Task<List<SitemapEntry>> ReadSitemapsAsync(List<Uri> sitemapUrls, bool explicitSitemaps, UrlFrontier frontier)
        {
            var max = owner.Crawl.MaxSitemapUrls;
            var found = new List<SitemapEntry>();
            var seen = new HashSet<string>();
            var queue = new Queue<(Uri Url, int Depth, bool Hint)>(sitemapUrls.Select(u => (u, 0, IsProductSitemap(u))));
            while (queue.Count > 0 && found.Count < max)
            {
                var (url, depth, hint) = queue.Dequeue();
                if (!seen.Add(url.AbsoluteUri))
                {
                    continue;
                }

                var response = await FetchFollowingRedirectsAsync(url, frontier.Home);
                if (!response.IsSuccess)
                {
                    Log.LogInformation("Sitemap {Url} not available ({Status}, {Error})", url, response.StatusCode, response.Error);
                    if (explicitSitemaps || depth > 0)
                    {
                        Warnings.Add(new ScanWarning(EngineCodes.SitemapUnreadable, NoteParams.Of(("url", url.ToString()), ("reason", DescribeFailure(response)))));
                    }

                    continue;
                }

                SitemapParser.Result parsed;
                try
                {
                    parsed = SitemapParser.Parse(response.Body!);
                }
                catch (Exception ex) when (ex is XmlException or InvalidDataException)
                {
                    Log.LogWarning("Sitemap {Url} is not valid: {Message}", url, ex.Message);
                    Warnings.Add(new ScanWarning(EngineCodes.SitemapInvalid, NoteParams.Of(("url", url.ToString()))));
                    continue;
                }

                foreach (var location in parsed.Entries)
                {
                    var target = UrlTools.TryResolve(location.Url, url);
                    if (target is null)
                    {
                        continue;
                    }

                    if (parsed.IsIndex)
                    {
                        if (depth < MaxSitemapDepth && frontier.IsSameSite(target))
                        {
                            queue.Enqueue((target, depth + 1, hint || IsProductSitemap(target)));
                        }
                    }
                    else
                    {
                        found.Add(new SitemapEntry(target, location.LastModified, hint));
                        if (found.Count >= max)
                        {
                            Warnings.Add(new ScanWarning(EngineCodes.SitemapTooMany, NoteParams.Of(("max", max))));
                            break;
                        }
                    }
                }

                Log.LogInformation("Sitemap {Url}: {Count} locations (index: {IsIndex})", url, parsed.Entries.Count, parsed.IsIndex);
            }

            return found;
        }

        private async Task<FetchResponse> FetchFollowingRedirectsAsync(Uri url, Uri home)
        {
            var current = url;
            FetchResponse response;
            var hop = 0;
            do
            {
                response = await FetchStep.FetchPacedAsync(owner.Fetcher, gate, new FetchRequest(current), state.Counters, Log, ct);
                if (response.RedirectLocation is not { } location || !UrlTools.IsSameSite(location, home))
                {
                    break;
                }

                current = location;
            }
            while (++hop <= owner.Crawl.MaxRedirects);

            return response;
        }

        private bool IsProductSitemap(Uri url)
        {
            var path = url.AbsolutePath.ToLowerInvariant();
            return owner.Crawl.ProductSitemapHints.Any(h => path.Contains(h, StringComparison.OrdinalIgnoreCase));
        }
    }
}
