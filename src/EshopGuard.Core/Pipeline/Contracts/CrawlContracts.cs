using EshopGuard.Core.Crawl;
using EshopGuard.Core.Models;
using EshopGuard.Core.Profiles;

namespace EshopGuard.Core.Pipeline;

/// <summary>The scanned site: its home URL (normalized) and the key its profiles and stored data are kept under.</summary>
internal sealed record SiteScope(Uri SiteUrl)
{
    public Uri Home { get; } = UrlTools.Normalize(SiteUrl);

    /// <summary>Host without "www." (and the port when not default): www.shop.sk and shop.sk share profiles.</summary>
    public string SiteKey => ProfileStep.SiteKey(SiteUrl);

    /// <summary>
    /// The language version crawled (change 7): only its addresses, with its cookies and <c>Accept-Language</c>, every page
    /// with its language. Null crawls the whole site as before.
    /// </summary>
    public Languages.VersionCrawlScope? Version { get; init; }
}

/// <summary>Limits of one crawl (scan options over the settings).</summary>
internal sealed record CrawlLimits(int MaxPages, int SampleProducts, IReadOnlyList<string> Include, IReadOnlyList<string> Exclude, double? FixedRate);

/// <summary>robots.txt as read in discovery; parsed again by every step that needs it.</summary>
/// <param name="Status"><c>allow_all</c> (missing), <c>disallow_all</c> (unreachable) or <c>parsed</c>.</param>
/// <param name="Text">The text of robots.txt when parsed.</param>
/// <param name="ProductToken">Product token of the User-Agent the groups of robots.txt are matched with.</param>
internal sealed record RobotsSnapshot(string Status, string? Text, string ProductToken)
{
    public const string AllowAll = "allow_all";
    public const string DisallowAll = "disallow_all";
    public const string Parsed = "parsed";

    public RobotsTxt ToRobots() => Status switch
    {
        AllowAll => RobotsTxt.AllowAll,
        DisallowAll => RobotsTxt.DisallowAll,
        _ => RobotsTxt.Parse(Text ?? "", ProductToken),
    };
}

/// <summary>A page URL from a sitemap, with its <c>lastmod</c> and whether it came from a product sitemap.</summary>
internal sealed record SitemapEntry(Uri Url, DateTimeOffset? LastModified, bool ProductHint)
{
    /// <summary>Alternates of the page in other language versions (<c>xhtml:link hreflang</c>), language normalized.</summary>
    public IReadOnlyList<Extract.PageAlternate> Alternates { get; init; } = [];
}

/// <summary>State of the adaptive pace (<see cref="AdaptiveGate"/>) carried from batch to batch.</summary>
/// <param name="Rate">Requests per second now.</param>
/// <param name="MaxRate">Upper limit of the pace (lowered by Crawl-delay).</param>
/// <param name="Adaptive">False for a fixed pace (<c>--rate</c>).</param>
/// <param name="Throttled">Answers 429 or 503 so far.</param>
/// <param name="NextRequestAt">The next request may not be sent before this time (pace or Retry-After); null when it may go now.</param>
internal sealed record PaceState(double Rate, double MaxRate, bool Adaptive, int Throttled, DateTimeOffset? NextRequestAt = null);

/// <summary>A URL waiting in the frontier.</summary>
internal sealed record FrontierItem(Uri Url, int Depth, bool ProductHint);

/// <summary>Counters of a crawl, for the statistics and the report.</summary>
internal sealed class CrawlCounters
{
    /// <summary>Requests sent to the site (pages, robots.txt, sitemaps, redirects, retries).</summary>
    public int Requests { get; set; }

    public int Failed { get; set; }

    public int ExcludedByFilter { get; set; }

    public int OverLimit { get; set; }

    public int ProductOverLimit { get; set; }

    public List<string> RobotsBlocked { get; set; } = [];

    /// <summary>URLs not downloaded because their address leads into an internal network (SSRF protection).</summary>
    public List<string> SsrfBlocked { get; set; } = [];

    public List<UncheckedDocument> UncheckedDocuments { get; set; } = [];

    public double? CrawlDelaySeconds { get; set; }
}

/// <summary>
/// Everything the crawl of one site needs to go on after a break: the queues (legal pages, products from sitemaps, other
/// pages), the URLs already visited or queued, the limits and the counters.
/// </summary>
internal sealed class UrlFrontierState : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;

    public required Uri Home { get; init; }

    /// <summary>The language version crawled; null crawls the whole site (same host).</summary>
    public Languages.VersionCrawlScope? Scope { get; init; }

    /// <summary>Cookies of this crawl: those of the version and those the site set during the crawl; never shared with another crawl.</summary>
    public Dictionary<string, string> Cookies { get; init; } = [];

    public bool LinkMode { get; set; }

    /// <summary>The home page was processed (it always goes first, before every queue).</summary>
    public bool HomeDone { get; set; }

    /// <summary>The crawl stopped for good (robots.txt forbids it, page limit reached).</summary>
    public bool Stopped { get; set; }

    public int MaxPages { get; init; }

    public int MaxProducts { get; init; }

    public int MaxLinkDepth { get; init; }

    /// <summary>Patterns of URLs to download only (<c>--include</c>); empty means all.</summary>
    public IReadOnlyList<string> Include { get; init; } = [];

    /// <summary>Patterns of URLs never to download (<c>--exclude</c>), besides <c>crawl.exclude_url_patterns</c>.</summary>
    public IReadOnlyList<string> Exclude { get; init; } = [];

    /// <summary>Legal pages: always downloaded first, also over the page limit.</summary>
    public Queue<FrontierItem> LegalQueue { get; init; } = new();

    /// <summary>Pages from product sitemaps.</summary>
    public Queue<FrontierItem> ProductQueue { get; init; } = new();

    public Queue<FrontierItem> OtherQueue { get; init; } = new();

    public HashSet<string> Visited { get; init; } = [];

    public HashSet<string> Queued { get; init; } = [];

    public HashSet<string> UncheckedKeys { get; init; } = [];

    public bool TakeProductNext { get; set; }

    public int Fetched { get; set; }

    public int ProductsIncluded { get; set; }

    public CrawlCounters Counters { get; init; } = new();
}

/// <summary>Input of <see cref="DiscoveryStep"/>.</summary>
internal sealed record DiscoveryInput(SiteScope Site, CrawlLimits Limits) : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;
}

/// <summary>Output of <see cref="DiscoveryStep"/>: robots.txt, sitemap and the frontier ready for the first batch.</summary>
/// <param name="Robots">robots.txt of the site.</param>
/// <param name="SitemapEntries">Page URLs from the sitemaps.</param>
/// <param name="Frontier">The frontier with the sitemap URLs.</param>
/// <param name="Pace">The pace after the requests of the discovery.</param>
/// <param name="Warnings">Warnings for the report (unreachable robots.txt, invalid sitemap).</param>
/// <param name="HomeBlocked">The address of the site leads into an internal network (SSRF protection); nothing was downloaded.</param>
internal sealed record DiscoveryResult(
    RobotsSnapshot Robots,
    IReadOnlyList<SitemapEntry> SitemapEntries,
    UrlFrontierState Frontier,
    PaceState Pace,
    IReadOnlyList<ScanWarning> Warnings,
    bool HomeBlocked = false) : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;
}

/// <summary>Validators of the last version of a page for a conditional request.</summary>
internal sealed record ConditionalHeaders(string? ETag, DateTimeOffset? LastModified);

/// <summary>How a URL of the frontier ended in a batch.</summary>
internal enum FetchOutcome
{
    /// <summary>HTML downloaded.</summary>
    Ok,

    /// <summary>304: the page did not change since the last version; nothing is extracted or counted.</summary>
    NotModified,

    /// <summary>Error, HTTP error status or too many redirects.</summary>
    Failed,

    /// <summary>The answer is not HTML.</summary>
    NotHtml,

    /// <summary>Redirect to another site.</summary>
    RedirectOffSite,

    /// <summary>Redirect to a URL robots.txt forbids.</summary>
    RobotsBlocked,

    /// <summary>Redirect to a URL already visited.</summary>
    Duplicate,

    /// <summary>The address leads into an internal network (SSRF protection); no connection was made.</summary>
    Blocked,
}

/// <summary>One URL processed in a batch. The HTML of a page is in <see cref="Storage.IPageContentStore"/>.</summary>
internal sealed record FetchedPage(Uri RequestedUrl, Uri FinalUrl, FetchOutcome Outcome, bool IsHome, int Depth)
{
    public string? ETag { get; init; }

    public DateTimeOffset? LastModified { get; init; }

    /// <summary>The extraction, when the batch extracted the page right away (CLI, runs with a product limit, home page).</summary>
    public ExtractedPageRecord? Extract { get; init; }
}

/// <summary>Input of one batch of <see cref="FetchStep"/>.</summary>
internal sealed record FetchBatchInput(
    SiteScope Site,
    RobotsSnapshot Robots,
    UrlFrontierState Frontier,
    PaceState Pace,
    int MaxPages,
    TimeSpan MaxDuration,
    bool ExtractInline) : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;

    /// <summary>Validators of the pages from the last run (monitoring); empty in the CLI.</summary>
    public IReadOnlyDictionary<string, ConditionalHeaders> Validators { get; init; } = new Dictionary<string, ConditionalHeaders>();

    /// <summary>Stored profiles of page templates, applied by the inline extraction.</summary>
    public IReadOnlyList<PageProfile> StoredProfiles { get; init; } = [];

    /// <summary>The inline extraction is also stored in the content store (the worker; the CLI keeps it in memory).</summary>
    public bool PersistExtracts { get; init; }
}

/// <summary>Output of one batch of <see cref="FetchStep"/>.</summary>
internal sealed record FetchBatchResult(IReadOnlyList<FetchedPage> Pages, UrlFrontierState Frontier, PaceState Pace, bool FrontierExhausted) : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;
}
