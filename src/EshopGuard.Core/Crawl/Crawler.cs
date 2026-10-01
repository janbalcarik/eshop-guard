using System.Xml;
using EshopGuard.Core.Classify;
using EshopGuard.Core.Extract;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Crawl;

/// <summary>
/// A downloaded page with everything extracted from it. The HTML is kept compressed until the profiles of page templates
/// are matched (about a tenth of its size), then it is not needed.
/// </summary>
internal sealed record CrawledPage(PageInfo Info, ExtractedPage Content, byte[]? CompressedHtml = null)
{
    public string? Html => CompressedHtml is null ? null : Decompress(CompressedHtml);

    public static byte[] Compress(string html)
    {
        using var output = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionLevel.Fastest))
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(html);
            gzip.Write(bytes, 0, bytes.Length);
        }

        return output.ToArray();
    }

    private static string Decompress(byte[] data)
    {
        using var gzip = new System.IO.Compression.GZipStream(new MemoryStream(data), System.IO.Compression.CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

/// <summary>
/// Result of crawling one site.
/// </summary>
internal sealed class CrawlResult
{
    public List<CrawledPage> Pages { get; } = [];

    public List<UncheckedDocument> UncheckedDocuments { get; } = [];

    public List<string> RobotsBlocked { get; } = [];

    public List<string> Warnings { get; } = [];

    public int Failed { get; set; }

    public int ExcludedByFilter { get; set; }

    public int OverLimit { get; set; }

    public int ProductOverLimit { get; set; }

    /// <summary>Time from the first to the last request.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Requests sent to the site (pages, robots.txt, sitemaps, redirects, retries).</summary>
    public int Requests { get; set; }

    /// <summary>Answers 429 or 503: the server asked to slow down.</summary>
    public int Throttled { get; set; }

    /// <summary>Pace at the end of the crawl, in requests per second.</summary>
    public double FinalRate { get; set; }

    /// <summary>Crawl-delay from robots.txt, if given.</summary>
    public TimeSpan? CrawlDelay { get; set; }
}

/// <summary>
/// Discovers and downloads pages: robots.txt, sitemaps (or links when there is no sitemap), URL filters,
/// page and product limits, same-site redirects and a polite request rate.
/// </summary>
internal sealed class Crawler
{
    private const int MaxSitemapDepth = 3;

    private readonly IPageFetcher _fetcher;
    private readonly ContentExtractor _extractor;
    private readonly PageClassifier _classifier;
    private readonly CrawlOptions _options;
    private readonly ILogger<Crawler> _logger;

    public Crawler(
        IPageFetcher fetcher,
        ContentExtractor extractor,
        PageClassifier classifier,
        IOptions<EshopGuardOptions> options,
        ILogger<Crawler> logger)
    {
        _fetcher = fetcher;
        _extractor = extractor;
        _classifier = classifier;
        _options = options.Value.Crawl;
        _logger = logger;
    }

    public Task<CrawlResult> CrawlAsync(Uri siteUrl, ScanOptions scan, IProgress<ScanProgress>? progress, CancellationToken ct) =>
        new Run(this, siteUrl, scan, progress, ct).ExecuteAsync();

    private sealed class Run
    {
        private readonly Crawler _owner;
        private readonly CrawlOptions _crawl;
        private readonly Uri _home;
        private readonly AdaptiveGate _gate;
        private readonly System.Diagnostics.Stopwatch _clock = new();
        private readonly UrlFilter _filter;
        private readonly int _maxPages;
        private readonly int _maxProducts;
        private readonly IProgress<ScanProgress>? _progress;
        private readonly CancellationToken _ct;
        private readonly CrawlResult _result = new();
        private readonly HashSet<string> _visited = [];
        private readonly HashSet<string> _queued = [];
        private readonly HashSet<string> _uncheckedKeys = [];
        private readonly Queue<Item> _legalQueue = new();
        private readonly Queue<Item> _productHintQueue = new();
        private readonly Queue<Item> _otherQueue = new();
        private RobotsTxt _robots = RobotsTxt.AllowAll;
        private bool _linkMode;
        private bool _takeProductNext;
        private int _fetched;
        private int _productsIncluded;

        public Run(Crawler owner, Uri siteUrl, ScanOptions scan, IProgress<ScanProgress>? progress, CancellationToken ct)
        {
            _owner = owner;
            _crawl = owner._options;
            _home = UrlTools.Normalize(siteUrl);
            // --rate sets a fixed pace; otherwise it adapts between the start rate and the maximum.
            _gate = scan.RequestsPerSecond is { } fixedRate
                ? new AdaptiveGate(fixedRate, fixedRate, adaptive: false)
                : new AdaptiveGate(_crawl.RequestsPerSecond, _crawl.MaxRequestsPerSecond, adaptive: true);
            _filter = new UrlFilter(_crawl.ExcludeUrlPatterns, scan.Include, scan.Exclude);
            _maxPages = scan.MaxPages ?? _crawl.MaxPages;
            _maxProducts = scan.SampleProducts ?? _crawl.SampleProducts;
            _progress = progress;
            _ct = ct;
        }

        private ILogger Log => _owner._logger;

        public async Task<CrawlResult> ExecuteAsync()
        {
            Report(ScanStage.Discovery, null);
            _clock.Start();
            try
            {
                return await CrawlAsync();
            }
            finally
            {
                _result.Duration = _clock.Elapsed;
                _result.Throttled = _gate.Throttled;
                _result.FinalRate = _gate.Rate;
                Log.LogInformation("Crawl finished: {Requests} requests in {Seconds:0.0} s, final pace {Rate:0.00}/s, throttled {Throttled}×",
                    _result.Requests, _result.Duration.TotalSeconds, _result.FinalRate, _result.Throttled);
            }
        }

        private async Task<CrawlResult> CrawlAsync()
        {
            _robots = await LoadRobotsAsync();
            if (_robots.CrawlDelay is { } delay)
            {
                _result.CrawlDelay = delay;
                _gate.Limit(1 / delay.TotalSeconds);
                Log.LogInformation("robots.txt asks for Crawl-delay {Delay} s, pace at most {Rate:0.00}/s", delay.TotalSeconds, 1 / delay.TotalSeconds);
            }

            if (ReferenceEquals(_robots, RobotsTxt.DisallowAll))
            {
                // The warning about the unreachable robots.txt is already recorded.
                return _result;
            }

            if (!_robots.IsAllowed(_home))
            {
                _result.RobotsBlocked.Add(_home.AbsoluteUri);
                _result.Warnings.Add("robots.txt zakazuje stahování úvodní stránky, web se neprocházel.");
                return _result;
            }

            var sitemapUrls = _robots.Sitemaps
                .Select(s => UrlTools.TryResolve(s, _home))
                .OfType<Uri>()
                .Where(IsSameSite)
                .ToList();
            var explicitSitemaps = sitemapUrls.Count > 0;
            if (!explicitSitemaps)
            {
                sitemapUrls.Add(new Uri(_home, "/sitemap.xml"));
            }

            var discovered = await ReadSitemapsAsync(sitemapUrls, explicitSitemaps);
            foreach (var (url, productHint) in discovered)
            {
                Consider(url, depth: 0, productHint, foundOn: "sitemap");
            }

            _linkMode = discovered.Count == 0;
            if (_linkMode)
            {
                Log.LogInformation("No sitemap URLs found, following links from the home page up to depth {Depth}", _crawl.MaxLinkDepth);
            }

            await ProcessAsync(new Item(_home, 0, ProductHint: false), isHome: true);

            while (TryDequeue(out var item, out var isPriority))
            {
                if (!isPriority && _fetched >= _maxPages)
                {
                    _result.OverLimit += 1 + _productHintQueue.Count + _otherQueue.Count;
                    break;
                }

                if (!isPriority && item.ProductHint && _productsIncluded >= _maxProducts)
                {
                    _result.ProductOverLimit++;
                    continue;
                }

                await ProcessAsync(item, isHome: false);
            }

            return _result;
        }

        private async Task ProcessAsync(Item item, bool isHome)
        {
            if (!_visited.Add(UrlTools.Key(item.Url)))
            {
                return;
            }

            Report(ScanStage.Pages, item.Url.AbsoluteUri);
            var page = await FetchHtmlAsync(item.Url);
            if (page is null)
            {
                return;
            }

            var (finalUrl, html) = page.Value;
            _fetched++;
            var content = _owner._extractor.Extract(finalUrl, html);
            var type = _owner._classifier.Classify(finalUrl, content, isHome);
            var info = new PageInfo
            {
                Url = finalUrl.AbsoluteUri,
                Type = type,
                Title = content.Title,
                MetaDescription = content.MetaDescription,
                JsonLdDescription = content.JsonLdDescription,
                Extraction = content.Method,
                Images = content.Images,
                MainText = string.Join('\n', content.MainBlocks.Select(b => b.Text)),
                Category = content.Category,
                VisibleTextChars = content.Render.VisibleChars,
                RestText = string.Join('\n', content.RestBlocks.Select(b => b.Text)),
                NavigationTextChars = content.NavigationChars,
                ListingTextChars = content.ListingChars,
                CheckedTextChars = content.MainBlocks.Concat(content.ChromeRegions.SelectMany(r => r)).Concat(content.RestBlocks).Sum(b => b.Text.Length),
                ScriptApp = content.Render.ScriptApp,
                TextNotLoaded = content.Render.VisibleChars < _crawl.MinPageTextChars,
            };

            if (type == PageType.Product)
            {
                if (_productsIncluded >= _maxProducts)
                {
                    info.IncludedInAnalysis = false;
                    _result.ProductOverLimit++;
                }
                else
                {
                    _productsIncluded++;
                }
            }

            _result.Pages.Add(new CrawledPage(info, content, CrawledPage.Compress(html)));
            Log.LogDebug("Page {Url}: {Type}, {Method}, {Blocks} main blocks", finalUrl, type, content.Method, content.MainBlocks.Count);
            if (info.TextNotLoaded)
            {
                Log.LogWarning("Page {Url}: only {Chars} characters of readable text, JavaScript sign: {App}", finalUrl, info.VisibleTextChars, info.ScriptApp ?? "none");
            }

            foreach (var link in content.Links)
            {
                if (UrlFilter.IsPdf(link.Url))
                {
                    if (IsSameSite(link.Url) && (_owner._classifier.IsLegalUrl(link.Url) || _owner._classifier.IsLegalText(link.Text)))
                    {
                        AddUnchecked(link.Url, link.Text, finalUrl.AbsoluteUri);
                    }

                    continue;
                }

                if (isHome || _linkMode)
                {
                    Consider(link.Url, item.Depth + 1, productHint: false, foundOn: finalUrl.AbsoluteUri, onlyLegal: !_linkMode);
                }
            }
        }

        private void Consider(Uri url, int depth, bool productHint, string foundOn, bool onlyLegal = false)
        {
            if (!IsSameSite(url))
            {
                return;
            }

            var normalized = UrlTools.AlignWith(UrlTools.Normalize(url), _home);
            var key = UrlTools.Key(normalized);
            if (_visited.Contains(key) || _queued.Contains(key))
            {
                return;
            }

            if (UrlFilter.IsPdf(normalized))
            {
                if (_owner._classifier.IsLegalUrl(normalized))
                {
                    AddUnchecked(normalized, null, foundOn);
                }

                return;
            }

            var isLegal = _owner._classifier.IsLegalUrl(normalized);
            if (onlyLegal && !isLegal)
            {
                return;
            }

            if (_linkMode && depth > _crawl.MaxLinkDepth)
            {
                return;
            }

            _queued.Add(key);
            if (!_robots.IsAllowed(normalized))
            {
                _result.RobotsBlocked.Add(normalized.AbsoluteUri);
                Log.LogInformation("Skipping {Url}: disallowed by robots.txt", normalized);
                return;
            }

            if (_filter.IsExcluded(normalized))
            {
                _result.ExcludedByFilter++;
                Log.LogDebug("Skipping {Url}: excluded by URL filter", normalized);
                return;
            }

            var item = new Item(normalized, depth, productHint);
            if (isLegal)
            {
                _legalQueue.Enqueue(item);
            }
            else if (productHint)
            {
                _productHintQueue.Enqueue(item);
            }
            else
            {
                _otherQueue.Enqueue(item);
            }
        }

        private bool TryDequeue(out Item item, out bool isPriority)
        {
            isPriority = _legalQueue.Count > 0;
            if (isPriority)
            {
                item = _legalQueue.Dequeue();
                return true;
            }

            // Alternate product and other pages so a small limit still covers both.
            var useProduct = _productHintQueue.Count > 0 && (_takeProductNext || _otherQueue.Count == 0);
            _takeProductNext = !_takeProductNext;
            if (useProduct)
            {
                item = _productHintQueue.Dequeue();
                return true;
            }

            if (_otherQueue.Count > 0)
            {
                item = _otherQueue.Dequeue();
                return true;
            }

            item = default;
            return false;
        }

        private async Task<(Uri Url, string Html)?> FetchHtmlAsync(Uri url)
        {
            var current = url;
            for (var hop = 0; hop <= _crawl.MaxRedirects; hop++)
            {
                var response = await FetchPacedAsync(current);
                if (response.RedirectLocation is { } location)
                {
                    var next = UrlTools.Normalize(location);
                    if (!IsSameSite(next))
                    {
                        Log.LogInformation("Not following redirect from {Url} to another site {Target}", current, next);
                        _result.Failed++;
                        return null;
                    }

                    next = UrlTools.AlignWith(next, _home);
                    if (!_robots.IsAllowed(next))
                    {
                        _result.RobotsBlocked.Add(next.AbsoluteUri);
                        return null;
                    }

                    if (!_visited.Add(UrlTools.Key(next)))
                    {
                        return null;
                    }

                    current = next;
                    continue;
                }

                if (!response.IsSuccess)
                {
                    Log.LogWarning("Failed to download {Url}: status {Status}, {Error}", current, response.StatusCode, response.Error);
                    _result.Failed++;
                    return null;
                }

                if (!IsHtml(response))
                {
                    Log.LogInformation("Skipping {Url}: content type {MediaType} is not HTML", current, response.MediaType);
                    _result.Failed++;
                    return null;
                }

                return (current, HtmlDecoding.Decode(response.Body!, response.Charset));
            }

            Log.LogWarning("Too many redirects from {Url}", url);
            _result.Failed++;
            return null;
        }

        /// <summary>
        /// One request at the current pace; the answer time adapts the pace. When the server answers 429 or 503,
        /// the request is repeated at most twice after the wait it asked for.
        /// </summary>
        private async Task<FetchResponse> FetchPacedAsync(Uri url)
        {
            for (var attempt = 0; ; attempt++)
            {
                await _gate.WaitAsync(_ct);
                var started = System.Diagnostics.Stopwatch.GetTimestamp();
                var response = await _owner._fetcher.FetchAsync(url, _ct);
                _result.Requests++;
                var wait = _gate.Report(response, System.Diagnostics.Stopwatch.GetElapsedTime(started));
                if (wait is null || attempt >= 2)
                {
                    return response;
                }

                Log.LogInformation("{Url} answered {Status}, waiting {Seconds:0.0} s and slowing down to {Rate:0.00}/s",
                    url, response.StatusCode, wait.Value.TotalSeconds, _gate.Rate);
            }
        }

        private async Task<FetchResponse> FetchFollowingRedirectsAsync(Uri url)
        {
            var current = url;
            FetchResponse response;
            var hop = 0;
            do
            {
                response = await FetchPacedAsync(current);
                if (response.RedirectLocation is not { } location || !IsSameSite(location))
                {
                    break;
                }

                current = location;
            }
            while (++hop <= _crawl.MaxRedirects);

            return response;
        }

        private async Task<RobotsTxt> LoadRobotsAsync()
        {
            var url = new Uri(_home, "/robots.txt");
            var response = await FetchFollowingRedirectsAsync(url);
            if (response.IsSuccess)
            {
                return RobotsTxt.Parse(HtmlDecoding.Decode(response.Body!, response.Charset), ProductToken());
            }

            if (response.StatusCode is >= 400 and < 500)
            {
                Log.LogInformation("robots.txt not found ({Status}), everything is allowed", response.StatusCode);
                return RobotsTxt.AllowAll;
            }

            Log.LogWarning("robots.txt unreachable ({Status}, {Error}), nothing is allowed", response.StatusCode, response.Error);
            _result.Warnings.Add($"robots.txt je nedostupný ({DescribeFailure(response)}); podle RFC 9309 se web neprocházel.");
            return RobotsTxt.DisallowAll;
        }

        private async Task<List<(Uri Url, bool ProductHint)>> ReadSitemapsAsync(List<Uri> sitemapUrls, bool explicitSitemaps)
        {
            var found = new List<(Uri, bool)>();
            var seen = new HashSet<string>();
            var queue = new Queue<(Uri Url, int Depth, bool Hint)>(sitemapUrls.Select(u => (u, 0, IsProductSitemap(u))));
            while (queue.Count > 0 && found.Count < _crawl.MaxSitemapUrls)
            {
                var (url, depth, hint) = queue.Dequeue();
                if (!seen.Add(url.AbsoluteUri))
                {
                    continue;
                }

                var response = await FetchFollowingRedirectsAsync(url);
                if (!response.IsSuccess)
                {
                    Log.LogInformation("Sitemap {Url} not available ({Status}, {Error})", url, response.StatusCode, response.Error);
                    if (explicitSitemaps || depth > 0)
                    {
                        _result.Warnings.Add($"Sitemap {url} se nepodařilo načíst ({DescribeFailure(response)}).");
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
                    _result.Warnings.Add($"Sitemap {url} není platné XML.");
                    continue;
                }

                foreach (var location in parsed.Locations)
                {
                    var target = UrlTools.TryResolve(location, url);
                    if (target is null)
                    {
                        continue;
                    }

                    if (parsed.IsIndex)
                    {
                        if (depth < MaxSitemapDepth && IsSameSite(target))
                        {
                            queue.Enqueue((target, depth + 1, hint || IsProductSitemap(target)));
                        }
                    }
                    else
                    {
                        found.Add((target, hint));
                        if (found.Count >= _crawl.MaxSitemapUrls)
                        {
                            _result.Warnings.Add($"Sitemap má víc než {_crawl.MaxSitemapUrls} URL, další se nečetly.");
                            break;
                        }
                    }
                }

                Log.LogInformation("Sitemap {Url}: {Count} locations (index: {IsIndex})", url, parsed.Locations.Count, parsed.IsIndex);
            }

            return found;
        }

        private void AddUnchecked(Uri url, string? linkText, string foundOn)
        {
            var normalized = UrlTools.Normalize(url);
            if (_uncheckedKeys.Add(UrlTools.Key(normalized)))
            {
                _result.UncheckedDocuments.Add(new UncheckedDocument
                {
                    Url = normalized.AbsoluteUri,
                    LinkText = string.IsNullOrWhiteSpace(linkText) ? null : linkText,
                    FoundOn = foundOn,
                });
            }
        }

        private bool IsSameSite(Uri url) => UrlTools.IsSameSite(url, _home);

        private bool IsProductSitemap(Uri url)
        {
            var path = url.AbsolutePath.ToLowerInvariant();
            return _crawl.ProductSitemapHints.Any(h => path.Contains(h, StringComparison.OrdinalIgnoreCase));
        }

        private string ProductToken()
        {
            var agent = _crawl.UserAgent.Trim();
            var end = agent.IndexOfAny(['/', ' ']);
            return end > 0 ? agent[..end] : agent;
        }

        private static bool IsHtml(FetchResponse response) =>
            response.MediaType is null
                ? LooksLikeHtml(response.Body!)
                : response.MediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase)
                  || response.MediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase);

        private static bool LooksLikeHtml(byte[] body)
        {
            var head = System.Text.Encoding.Latin1.GetString(body, 0, Math.Min(body.Length, 1024));
            return head.Contains("<html", StringComparison.OrdinalIgnoreCase) || head.Contains("<!doctype html", StringComparison.OrdinalIgnoreCase);
        }

        private static string DescribeFailure(FetchResponse response) =>
            response.Error ?? (response.StatusCode > 0 ? $"HTTP {response.StatusCode}" : "bez odpovědi");

        private void Report(ScanStage stage, string? url) =>
            _progress?.Report(new ScanProgress
            {
                Stage = stage,
                Completed = _fetched,
                Total = Math.Max(_fetched, Math.Min(_maxPages, _fetched + _productHintQueue.Count + _otherQueue.Count) + _legalQueue.Count),
                CurrentUrl = url,
            });
    }

    private readonly record struct Item(Uri Url, int Depth, bool ProductHint);
}
