using System.Diagnostics;
using EshopGuard.Core.Classify;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Pipeline;

/// <summary>
/// Downloads one batch of pages of one site (at most <see cref="FetchBatchInput.MaxPages"/> URLs or
/// <see cref="FetchBatchInput.MaxDuration"/>), the home page first: same-site redirects with robots.txt checked at their
/// target, the adaptive pace carried in <see cref="PaceState"/>, conditional requests from the validators of the last run.
/// Pages whose links are followed (the home page, every page without a sitemap) and, with
/// <see cref="FetchBatchInput.ExtractInline"/>, every page are extracted right away by <see cref="ExtractStep"/>; the
/// others only have their HTML stored for a later extraction. The frontier is changed in place and returned.
/// </summary>
internal sealed class FetchStep(
    IPageFetcher fetcher,
    ExtractStep extractStep,
    PageClassifier classifier,
    IPageContentStore contents,
    IOptions<EshopGuardOptions> options,
    ILogger<FetchStep> logger)
{
    private CrawlOptions Crawl => options.Value.Crawl;

    private IPageFetcher Fetcher => fetcher;

    private ExtractStep Extractor => extractStep;

    private IPageContentStore Contents => contents;

    private ILogger Logger => logger;

    public async Task<FetchBatchResult> FetchBatchAsync(FetchBatchInput input, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var frontier = new UrlFrontier(input.Frontier, input.Robots.ToRobots(), Crawl.ExcludeUrlPatterns, classifier, logger);
        var gate = AdaptiveGate.FromState(input.Pace);
        var batch = new Batch(this, input, frontier, gate, progress, ct);
        var clock = Stopwatch.StartNew();
        var state = input.Frontier;

        if (!state.HomeDone && !state.Stopped)
        {
            state.HomeDone = true;
            await batch.ProcessAsync(new FrontierItem(state.Home, 0, ProductHint: false), isHome: true);
        }

        while (!state.Stopped && batch.Processed < input.MaxPages && clock.Elapsed < input.MaxDuration && frontier.TryDequeue(out var item, out var isPriority))
        {
            if (!isPriority && state.Fetched >= state.MaxPages)
            {
                frontier.StopOverLimit();
                break;
            }

            if (!isPriority && item.ProductHint && state.ProductsIncluded >= state.MaxProducts)
            {
                state.Counters.ProductOverLimit++;
                continue;
            }

            await batch.ProcessAsync(item, isHome: false);
        }

        if (frontier.IsExhausted)
        {
            state.Stopped = true;
        }

        return new FetchBatchResult(batch.Pages, state, gate.ToState(), frontier.IsExhausted);
    }

    /// <summary>
    /// One request at the current pace; the answer time adapts the pace. When the server answers 429 or 503, the request is
    /// repeated at most twice after the wait it asked for.
    /// </summary>
    internal static async Task<FetchResponse> FetchPacedAsync(
        IPageFetcher fetcher, AdaptiveGate gate, FetchRequest request, CrawlCounters counters, ILogger logger, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            await gate.WaitAsync(ct);
            var started = Stopwatch.GetTimestamp();
            var response = await fetcher.FetchAsync(request, ct);
            counters.Requests++;
            var wait = gate.Report(response, Stopwatch.GetElapsedTime(started));
            if (wait is null || attempt >= 2)
            {
                return response;
            }

            logger.LogInformation("{Url} answered {Status}, waiting {Seconds:0.0} s and slowing down to {Rate:0.00}/s",
                request.Url, response.StatusCode, wait.Value.TotalSeconds, gate.Rate);
        }
    }

    internal static bool IsHtml(FetchResponse response) =>
        response.MediaType is null
            ? LooksLikeHtml(response.Body!)
            : response.MediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase)
              || response.MediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeHtml(byte[] body)
    {
        var head = System.Text.Encoding.Latin1.GetString(body, 0, Math.Min(body.Length, 1024));
        return head.Contains("<html", StringComparison.OrdinalIgnoreCase) || head.Contains("<!doctype html", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>One batch: the pages it processed, in order.</summary>
    private sealed class Batch(FetchStep owner, FetchBatchInput input, UrlFrontier frontier, AdaptiveGate gate, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        public List<FetchedPage> Pages { get; } = [];

        /// <summary>URLs taken from the frontier in this batch (the home page included).</summary>
        public int Processed { get; private set; }

        private UrlFrontierState State => frontier.State;

        private ILogger Log => owner.Logger;

        public async Task ProcessAsync(FrontierItem item, bool isHome)
        {
            Processed++;
            if (!frontier.MarkVisited(item.Url))
            {
                return;
            }

            progress?.Report(frontier.Progress(ScanStage.Pages, item.Url.AbsoluteUri));
            var download = await FetchHtmlAsync(item.Url);
            if (download.Html is null)
            {
                Pages.Add(new FetchedPage(item.Url, download.FinalUrl, download.Outcome, isHome, item.Depth));
                return;
            }

            if (download.Outcome == FetchOutcome.NotModified && frontier.NeedsLinks(isHome))
            {
                // The links of an unchanged page come from its stored extraction; without it the page is downloaded whole,
                // so that no page behind its links is lost.
                var stored = await owner.Contents.GetExtractAsync(Key(download.FinalUrl), ct);
                if (stored is not null)
                {
                    frontier.ConsiderLinks(PipelineJson.Deserialize<ExtractedPageRecord>(stored), item.Depth);
                }
                else
                {
                    download = await FetchHtmlAsync(download.FinalUrl, conditional: false);
                    if (download.Html is null)
                    {
                        Pages.Add(new FetchedPage(item.Url, download.FinalUrl, download.Outcome, isHome, item.Depth));
                        return;
                    }
                }
            }

            if (download.Outcome == FetchOutcome.NotModified)
            {
                Pages.Add(new FetchedPage(item.Url, download.FinalUrl, FetchOutcome.NotModified, isHome, item.Depth)
                {
                    ETag = download.ETag,
                    LastModified = download.LastModified,
                });
                return;
            }

            State.Fetched++;
            var finalUrl = download.FinalUrl;
            ExtractedPageRecord? extract = null;
            if (input.ExtractInline || frontier.NeedsLinks(isHome))
            {
                extract = await owner.Extractor.ExtractPageAsync(input.Site, finalUrl, download.Html, isHome, input.StoredProfiles, ct, input.PersistExtracts);
                if (extract.Status == ExtractionStatus.Ok)
                {
                    frontier.CountProduct(extract.Info);
                    frontier.ConsiderLinks(extract, item.Depth);
                }
            }
            else
            {
                await owner.Contents.PutHtmlAsync(Key(finalUrl), PageContent.Compress(download.Html), ct);
            }

            Pages.Add(new FetchedPage(item.Url, finalUrl, FetchOutcome.Ok, isHome, item.Depth)
            {
                ETag = download.ETag,
                LastModified = download.LastModified,
                Extract = extract,
            });
        }

        private PageContentKey Key(Uri url) => new(input.Site.SiteKey, url.AbsoluteUri);

        private async Task<Download> FetchHtmlAsync(Uri url, bool conditional = true)
        {
            var current = url;
            var crawl = owner.Crawl;
            for (var hop = 0; hop <= crawl.MaxRedirects; hop++)
            {
                var request = new FetchRequest(current)
                {
                    Cookies = State.Cookies.Count > 0 ? new Dictionary<string, string>(State.Cookies) : null,
                    AcceptLanguage = State.Scope?.AcceptLanguage,
                };
                if (conditional && input.Validators.TryGetValue(current.AbsoluteUri, out var validators))
                {
                    request = request with { IfNoneMatch = validators.ETag, IfModifiedSince = validators.LastModified };
                }

                var response = await FetchPacedAsync(owner.Fetcher, gate, request, State.Counters, Log, ct);
                CookieJar.Apply(State.Cookies, response.SetCookies, DateTimeOffset.UtcNow);
                if (response.Error == SsrfGuard.Error)
                {
                    State.Counters.SsrfBlocked.Add(current.AbsoluteUri);
                    return new Download(current, FetchOutcome.Blocked);
                }

                if (response.NotModified)
                {
                    Log.LogDebug("{Url} not modified since the last download", current);
                    return new Download(current, FetchOutcome.NotModified, Html: "", response.ETag, response.LastModified);
                }

                if (response.RedirectLocation is { } location)
                {
                    var next = UrlTools.Normalize(location);
                    if (!frontier.IsInScope(next))
                    {
                        Log.LogInformation("Not following redirect from {Url} to another site or language version {Target}", current, next);
                        State.Counters.Failed++;
                        return new Download(current, FetchOutcome.RedirectOffSite);
                    }

                    next = UrlTools.AlignWith(next, State.Home);
                    if (!frontier.IsAllowedByRobots(next))
                    {
                        State.Counters.RobotsBlocked.Add(next.AbsoluteUri);
                        return new Download(next, FetchOutcome.RobotsBlocked);
                    }

                    if (!frontier.MarkVisited(next))
                    {
                        return new Download(next, FetchOutcome.Duplicate);
                    }

                    current = next;
                    continue;
                }

                if (!response.IsSuccess)
                {
                    Log.LogWarning("Failed to download {Url}: status {Status}, {Error}", current, response.StatusCode, response.Error);
                    State.Counters.Failed++;
                    return new Download(current, FetchOutcome.Failed);
                }

                if (!IsHtml(response))
                {
                    Log.LogInformation("Skipping {Url}: content type {MediaType} is not HTML", current, response.MediaType);
                    State.Counters.Failed++;
                    return new Download(current, FetchOutcome.NotHtml);
                }

                return new Download(current, FetchOutcome.Ok, HtmlDecoding.Decode(response.Body!, response.Charset), response.ETag, response.LastModified);
            }

            Log.LogWarning("Too many redirects from {Url}", url);
            State.Counters.Failed++;
            return new Download(current, FetchOutcome.Failed);
        }
    }

    private readonly record struct Download(Uri FinalUrl, FetchOutcome Outcome, string? Html = null, string? ETag = null, DateTimeOffset? LastModified = null);
}
