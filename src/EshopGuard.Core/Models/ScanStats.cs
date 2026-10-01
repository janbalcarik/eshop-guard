namespace EshopGuard.Core.Models;

/// <summary>
/// Statistics of one scan.
/// </summary>
public sealed class ScanStats
{
    /// <summary>Successfully downloaded HTML pages.</summary>
    public int PagesFetched { get; set; }

    /// <summary>Downloaded pages by type.</summary>
    public Dictionary<PageType, int> PagesByType { get; set; } = [];

    /// <summary>Pages that failed to download or were not HTML.</summary>
    public int PagesFailed { get; set; }

    /// <summary>URLs skipped because of robots.txt.</summary>
    public int PagesBlockedByRobots { get; set; }

    /// <summary>URLs skipped by URL filters (cart, login, search, paging, filters, include/exclude).</summary>
    public int PagesExcludedByFilter { get; set; }

    /// <summary>Known URLs not downloaded because of the page limit.</summary>
    public int PagesOverLimit { get; set; }

    /// <summary>Product pages left out because of the product sample limit.</summary>
    public int ProductPagesOverLimit { get; set; }

    /// <summary>Pages whose main text came from SmartReader.</summary>
    public int PagesWithReadability { get; set; }

    /// <summary>Pages whose main text came from the fallback heuristic.</summary>
    public int PagesWithFallback { get; set; }

    /// <summary>Pages whose text was not in the downloaded HTML (see <see cref="PageInfo.TextNotLoaded"/>); they are not checked.</summary>
    public int PagesTextNotLoaded { get; set; }

    /// <summary>Readable characters on all downloaded pages.</summary>
    public long VisibleTextChars { get; set; }

    /// <summary>Checked characters (main text, frame and other text), at most the visible characters of each page.</summary>
    public long CheckedTextChars { get; set; }

    /// <summary>Characters in navigation, left out on purpose.</summary>
    public long NavigationTextChars { get; set; }

    /// <summary>Characters in tiles of other products, checked on those products' own pages.</summary>
    public long ListingTextChars { get; set; }

    /// <summary>Pages with a notable share of visible text that was neither checked nor navigation (<see cref="PageInfo.HasUncheckedText"/>).</summary>
    public int PagesWithUncheckedText { get; set; }

    /// <summary>Segments before deduplication (one per page and place).</summary>
    public int SegmentOccurrences { get; set; }

    /// <summary>Unique segments.</summary>
    public int UniqueSegments { get; set; }

    /// <summary>Unique sentence segments.</summary>
    public int SentenceSegments { get; set; }

    /// <summary>Unique legal paragraphs.</summary>
    public int LegalParagraphSegments { get; set; }

    /// <summary>Unique segments marked as boilerplate.</summary>
    public int BoilerplateSegments { get; set; }

    /// <summary>Calls to Jev (without cache hits).</summary>
    public int JevCalls { get; set; }

    /// <summary>Answers taken from the cache.</summary>
    public int JevCacheHits { get; set; }

    /// <summary>Failed calls to Jev.</summary>
    public int JevErrors { get; set; }

    /// <summary>Input tokens reported by Jev.</summary>
    public long InputTokens { get; set; }

    /// <summary>Time of the crawl in seconds.</summary>
    public double CrawlSeconds { get; set; }

    /// <summary>Requests sent to the site during the crawl.</summary>
    public int CrawlRequests { get; set; }

    /// <summary>Pace at the end of the crawl, in requests per second.</summary>
    public double CrawlFinalRate { get; set; }

    /// <summary>Answers 429 or 503 during the crawl.</summary>
    public int CrawlThrottled { get; set; }

    /// <summary>Crawl-delay from robots.txt in seconds, 0 when not given.</summary>
    public double CrawlDelaySeconds { get; set; }

    /// <summary>True when the block sieve ran before the detailed questions.</summary>
    public bool SieveEnabled { get; set; }

    /// <summary>Threshold of the sieve.</summary>
    public double SieveThreshold { get; set; }

    /// <summary>Maximum length of a sieve chunk in characters.</summary>
    public int SieveChunkChars { get; set; }

    /// <summary>Chunks of the main text asked by the sieve.</summary>
    public int SieveChunks { get; set; }

    /// <summary>Sieve requests sent to Jev (cache hits excluded).</summary>
    public int SieveCalls { get; set; }

    /// <summary>Sieve answers taken from the cache.</summary>
    public int SieveCacheHits { get; set; }

    /// <summary>Chunks the sieve could not ask; their sentences were evaluated in detail.</summary>
    public int SieveErrors { get; set; }

    /// <summary>Chunks longer than the request limit; their sentences were evaluated in detail.</summary>
    public int SieveTooLong { get; set; }

    /// <summary>Pairs of a sentence and a sieved module.</summary>
    public int SievePairs { get; set; }

    /// <summary>Pairs whose detailed questions the sieve left out.</summary>
    public int SieveSkippedPairs { get; set; }

    /// <summary>True when profiles of page templates were used or could be written.</summary>
    public bool ProfilesEnabled { get; set; }

    /// <summary>Profiles used by at least one page.</summary>
    public int ProfilesUsed { get; set; }

    /// <summary>Profiles written in this scan.</summary>
    public int ProfilesCreated { get; set; }

    /// <summary>Profiles planned before the confirmation (at most; a new profile may fit more groups).</summary>
    public int ProfilesPlanned { get; set; }

    /// <summary>Pages that used a profile.</summary>
    public int PagesWithProfile { get; set; }

    /// <summary>Pages that could use a profile but fit none; they were checked whole.</summary>
    public int PagesWithoutProfile { get; set; }

    /// <summary>Characters left out by profiles (cookie bar, forms, cart, filters, listings).</summary>
    public long ProfileSkippedTextChars { get; set; }

    /// <summary>Requests to the profile model.</summary>
    public int ProfileCalls { get; set; }

    /// <summary>Input tokens of the profile model.</summary>
    public long ProfileInputTokens { get; set; }

    /// <summary>Output tokens of the profile model, reasoning included.</summary>
    public long ProfileOutputTokens { get; set; }

    /// <summary>Price of the profile model in USD (OpenAI, apart from <see cref="EstimatedCostUsd"/>).</summary>
    public decimal ProfileCostUsd { get; set; }

    /// <summary>Estimated price of Jev in USD.</summary>
    public decimal EstimatedCostUsd { get; set; }

    /// <summary>Duration of the run in seconds.</summary>
    public double DurationSeconds { get; set; }
}
