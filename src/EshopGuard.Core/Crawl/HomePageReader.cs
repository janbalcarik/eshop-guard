using EshopGuard.Core.Pipeline;

namespace EshopGuard.Core.Crawl;

/// <summary>Why the home page could not be read (codes of <c>DetectionDto.failureCode</c> and of the check of ownership).</summary>
public static class HomePageCodes
{
    /// <summary>robots.txt forbids the page to <c>EshopGuard</c>.</summary>
    public const string RobotsBlocked = "robots_blocked";

    /// <summary>Network error, an error status or too many redirects.</summary>
    public const string FetchFailed = "fetch_failed";

    /// <summary>The site did not answer in time.</summary>
    public const string Timeout = "timeout";

    /// <summary>The answer is not HTML.</summary>
    public const string NotHtml = "not_html";
}

/// <summary>The home page of an e-shop: the final answer, or the code why there is none, or the other domain it redirects to.</summary>
public sealed record HomePageResult(Uri FinalUrl, FetchResponse? Response, string? FailureCode, string? RedirectedToHost)
{
    /// <summary>True when the page was read.</summary>
    public bool IsSuccess => Response is not null;
}

/// <summary>
/// Reads one home page the user named, for the recognition of the platform and the check of ownership (change 10): robots.txt
/// first (a 4xx answer allows all, another failure forbids all, as the crawl), then the page through the fetcher with the
/// protection against SSRF, redirects followed only within the same site (with or without <c>www.</c>, at most 5). A redirect
/// to another domain is reported, never followed: only sites the user named are downloaded.
/// </summary>
public static class HomePageReader
{
    private const int MaxRedirects = 5;

    /// <summary>Reads the page; failures are codes in the result, nothing is thrown except the cancellation.</summary>
    public static async Task<HomePageResult> ReadAsync(
        IPageFetcher fetcher, Uri url, string userAgent, long maxBytes, bool captureHeaders, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fetcher);
        ArgumentNullException.ThrowIfNull(url);
        var token = DiscoveryStep.ProductToken(userAgent);
        var robots = new Dictionary<string, RobotsTxt>(StringComparer.OrdinalIgnoreCase);
        var current = url;
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            var authority = current.GetLeftPart(UriPartial.Authority);
            if (!robots.TryGetValue(authority, out var rules))
            {
                var answer = await fetcher.FetchAsync(new FetchRequest(new Uri(authority + "/robots.txt")), ct).ConfigureAwait(false);
                rules = answer.IsSuccess
                    ? RobotsTxt.Parse(HtmlDecoding.Decode(answer.Body!, answer.Charset), token)
                    : answer.Error is null && answer.StatusCode is >= 400 and < 500 ? RobotsTxt.AllowAll : RobotsTxt.DisallowAll;
                robots[authority] = rules;
            }

            if (!rules.IsAllowed(current))
            {
                return new HomePageResult(current, null, HomePageCodes.RobotsBlocked, null);
            }

            var response = await fetcher.FetchAsync(new FetchRequest(current) { MaxBytes = maxBytes, CaptureHeaders = captureHeaders }, ct).ConfigureAwait(false);
            if (response.RedirectLocation is { } location)
            {
                if (!UrlTools.IsSameSite(location, url))
                {
                    return new HomePageResult(current, null, null, location.IdnHost.ToLowerInvariant());
                }

                current = location;
                continue;
            }

            if (response.Error is not null)
            {
                return new HomePageResult(current, null, response.Error == "timeout" ? HomePageCodes.Timeout : HomePageCodes.FetchFailed, null);
            }

            if (!response.IsSuccess)
            {
                return new HomePageResult(current, null, HomePageCodes.FetchFailed, null);
            }

            return FetchStep.IsHtml(response)
                ? new HomePageResult(current, response, null, null)
                : new HomePageResult(current, null, HomePageCodes.NotHtml, null);
        }

        return new HomePageResult(current, null, HomePageCodes.FetchFailed, null);
    }
}
