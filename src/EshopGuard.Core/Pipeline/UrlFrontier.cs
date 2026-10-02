using EshopGuard.Core.Classify;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Models;
using Microsoft.Extensions.Logging;

namespace EshopGuard.Core.Pipeline;

/// <summary>
/// Decides which URL is downloaded next, over a <see cref="UrlFrontierState"/> that is saved between batches: legal pages
/// first (also over the page limit), then product pages from sitemaps and other pages in turn, so that a small limit still
/// covers both; same site only, robots.txt, URL filters, the link depth when the site has no sitemap, and PDF legal
/// documents listed as not checked.
/// </summary>
internal sealed class UrlFrontier
{
    private readonly RobotsTxt _robots;
    private readonly UrlFilter _filter;
    private readonly PageClassifier _classifier;
    private readonly ILogger _logger;

    public UrlFrontier(UrlFrontierState state, RobotsTxt robots, IEnumerable<string> defaultExclusions, PageClassifier classifier, ILogger logger)
    {
        State = state;
        _robots = robots;
        _filter = new UrlFilter(defaultExclusions, state.Include, state.Exclude);
        _classifier = classifier;
        _logger = logger;
    }

    public UrlFrontierState State { get; }

    public Uri Home => State.Home;

    /// <summary>Nothing is left to download: the crawl stopped or every queue is empty.</summary>
    public bool IsExhausted => State.Stopped || (State.HomeDone && State.LegalQueue.Count == 0 && State.ProductQueue.Count == 0 && State.OtherQueue.Count == 0);

    /// <summary>Links of the page are followed: the home page (legal pages only) and every page when the site has no sitemap.</summary>
    public bool NeedsLinks(bool isHome) => isHome || State.LinkMode;

    public bool IsSameSite(Uri url) => UrlTools.IsSameSite(url, State.Home);

    /// <summary>The address belongs to the crawl: the language version when there is one, otherwise the same site.</summary>
    public bool IsInScope(Uri url) => State.Scope is { } scope ? UrlTools.IsInScope(url, scope) : IsSameSite(url);

    public bool IsAllowedByRobots(Uri url) => _robots.IsAllowed(url);

    /// <summary>Marks the URL as visited; false when it was visited before.</summary>
    public bool MarkVisited(Uri url) => State.Visited.Add(UrlTools.Key(url));

    /// <summary>Adds a URL found in a sitemap or a link, unless it is another site, known, forbidden or filtered out.</summary>
    public void Consider(Uri url, int depth, bool productHint, string foundOn, bool onlyLegal = false)
    {
        if (!IsInScope(url))
        {
            return;
        }

        var normalized = UrlTools.AlignWith(UrlTools.Normalize(url), State.Home);
        var key = UrlTools.Key(normalized);
        if (State.Visited.Contains(key) || State.Queued.Contains(key))
        {
            return;
        }

        if (UrlFilter.IsPdf(normalized))
        {
            if (_classifier.IsLegalUrl(normalized))
            {
                AddUnchecked(normalized, null, foundOn);
            }

            return;
        }

        var isLegal = _classifier.IsLegalUrl(normalized);
        if (onlyLegal && !isLegal)
        {
            return;
        }

        if (State.LinkMode && depth > State.MaxLinkDepth)
        {
            return;
        }

        State.Queued.Add(key);
        if (!_robots.IsAllowed(normalized))
        {
            State.Counters.RobotsBlocked.Add(normalized.AbsoluteUri);
            _logger.LogInformation("Skipping {Url}: disallowed by robots.txt", normalized);
            return;
        }

        if (_filter.IsExcluded(normalized))
        {
            State.Counters.ExcludedByFilter++;
            _logger.LogDebug("Skipping {Url}: excluded by URL filter", normalized);
            return;
        }

        var item = new FrontierItem(normalized, depth, productHint);
        if (isLegal)
        {
            State.LegalQueue.Enqueue(item);
        }
        else if (productHint)
        {
            State.ProductQueue.Enqueue(item);
        }
        else
        {
            State.OtherQueue.Enqueue(item);
        }
    }

    /// <summary>The links of a downloaded page: legal PDF documents are listed as not checked, pages are considered when <see cref="NeedsLinks"/>.</summary>
    public void ConsiderLinks(ExtractedPageRecord page, int depth)
    {
        var finalUrl = page.Info.Url;
        foreach (var link in page.Content.Links)
        {
            if (UrlFilter.IsPdf(link.Url))
            {
                if (IsInScope(link.Url) && (_classifier.IsLegalUrl(link.Url) || _classifier.IsLegalText(link.Text)))
                {
                    AddUnchecked(link.Url, link.Text, finalUrl);
                }

                continue;
            }

            if (NeedsLinks(page.IsHome))
            {
                Consider(link.Url, depth + 1, productHint: false, foundOn: finalUrl, onlyLegal: !State.LinkMode);
            }
        }
    }

    /// <summary>
    /// The next URL: a legal page (<paramref name="isPriority"/>), otherwise product and other pages in turn.
    /// </summary>
    public bool TryDequeue(out FrontierItem item, out bool isPriority)
    {
        isPriority = State.LegalQueue.Count > 0;
        if (isPriority)
        {
            item = State.LegalQueue.Dequeue();
            return true;
        }

        // Alternate product and other pages so a small limit still covers both.
        var useProduct = State.ProductQueue.Count > 0 && (State.TakeProductNext || State.OtherQueue.Count == 0);
        State.TakeProductNext = !State.TakeProductNext;
        if (useProduct)
        {
            item = State.ProductQueue.Dequeue();
            return true;
        }

        if (State.OtherQueue.Count > 0)
        {
            item = State.OtherQueue.Dequeue();
            return true;
        }

        item = null!;
        return false;
    }

    /// <summary>
    /// The page limit is reached: the rest of the queues is counted as over the limit and the crawl stops (the item just
    /// taken counts too).
    /// </summary>
    public void StopOverLimit()
    {
        State.Counters.OverLimit += 1 + State.ProductQueue.Count + State.OtherQueue.Count;
        State.ProductQueue.Clear();
        State.OtherQueue.Clear();
        State.Stopped = true;
    }

    /// <summary>A product page counts against the product sample; over it, the page stays in the report but not in the analysis.</summary>
    public void CountProduct(PageInfo info)
    {
        if (info.Type != PageType.Product)
        {
            return;
        }

        if (State.ProductsIncluded >= State.MaxProducts)
        {
            info.IncludedInAnalysis = false;
            State.Counters.ProductOverLimit++;
        }
        else
        {
            State.ProductsIncluded++;
        }
    }

    public void AddUnchecked(Uri url, string? linkText, string foundOn)
    {
        var normalized = UrlTools.Normalize(url);
        if (State.UncheckedKeys.Add(UrlTools.Key(normalized)))
        {
            State.Counters.UncheckedDocuments.Add(new UncheckedDocument
            {
                Url = normalized.AbsoluteUri,
                LinkText = string.IsNullOrWhiteSpace(linkText) ? null : linkText,
                FoundOn = foundOn,
            });
        }
    }

    /// <summary>Progress of the crawl for the host.</summary>
    public ScanProgress Progress(ScanStage stage, string? url) => new()
    {
        Stage = stage,
        Completed = State.Fetched,
        Total = Math.Max(State.Fetched, Math.Min(State.MaxPages, State.Fetched + State.ProductQueue.Count + State.OtherQueue.Count) + State.LegalQueue.Count),
        CurrentUrl = url,
    };
}
