namespace EshopGuard.Core.Crawl;

/// <summary>
/// A request for one URL; with validators of the last download it is conditional, and an unchanged page answers 304
/// (<see cref="FetchResponse.NotModified"/>) without a body.
/// </summary>
public sealed record FetchRequest(Uri Url)
{
    /// <summary>ETag of the last download (<c>If-None-Match</c>).</summary>
    public string? IfNoneMatch { get; init; }

    /// <summary>Last-Modified of the last download (<c>If-Modified-Since</c>).</summary>
    public DateTimeOffset? IfModifiedSince { get; init; }

    /// <summary>Cookies of the crawl scope (the language version), sent in the <c>Cookie</c> header; null sends none.</summary>
    public IReadOnlyDictionary<string, string>? Cookies { get; init; }

    /// <summary>Size limit of this response in bytes (sitemaps); null takes <c>crawl.max_page_bytes</c>.</summary>
    public long? MaxBytes { get; init; }

    /// <summary><c>Accept-Language</c> of the crawl scope; null keeps the default of the crawler.</summary>
    public string? AcceptLanguage { get; init; }

    /// <summary>
    /// Keep the headers of the response in <see cref="FetchResponse.Headers"/> (recognition of the platform, change 10); a crawl
    /// does not keep them, so thousands of pages do not hold them in memory.
    /// </summary>
    public bool CaptureHeaders { get; init; }

    /// <summary>True when the request carries a validator.</summary>
    public bool IsConditional => IfNoneMatch is not null || IfModifiedSince is not null;
}
