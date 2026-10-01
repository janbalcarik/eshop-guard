namespace EshopGuard.Core.Crawl;

/// <summary>
/// Downloads one URL. Implementations do not follow redirects, respect robots.txt or throttle;
/// the crawler does that, so a test fetcher or a headless browser can be plugged in.
/// </summary>
public interface IPageFetcher
{
    /// <summary>Downloads the URL. Network errors are returned in <see cref="FetchResponse.Error"/>, not thrown.</summary>
    Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct);
}

/// <summary>
/// Response of <see cref="IPageFetcher"/>.
/// </summary>
public sealed class FetchResponse
{
    /// <summary>The requested URL.</summary>
    public required Uri Url { get; init; }

    /// <summary>HTTP status code, or 0 when there was no response.</summary>
    public int StatusCode { get; init; }

    /// <summary>Absolute target of a redirect (3xx), if any.</summary>
    public Uri? RedirectLocation { get; init; }

    /// <summary>Media type from Content-Type, e.g. <c>text/html</c>.</summary>
    public string? MediaType { get; init; }

    /// <summary>Charset from Content-Type, if given.</summary>
    public string? Charset { get; init; }

    /// <summary>Response body.</summary>
    public byte[]? Body { get; init; }

    /// <summary>Error description when the request failed (timeout, too large, network error).</summary>
    public string? Error { get; init; }

    /// <summary>How long the server asked to wait (Retry-After of a 429 or 503 answer).</summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>ETag of the response, for a later conditional request.</summary>
    public string? ETag { get; init; }

    /// <summary>Last-Modified of the response, for a later conditional request.</summary>
    public DateTimeOffset? LastModified { get; init; }

    /// <summary>True for a 2xx response with a body.</summary>
    public bool IsSuccess => Error is null && StatusCode is >= 200 and < 300 && Body is not null;
}
