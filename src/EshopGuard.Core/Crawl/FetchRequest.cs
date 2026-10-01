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

    /// <summary>True when the request carries a validator.</summary>
    public bool IsConditional => IfNoneMatch is not null || IfModifiedSince is not null;
}
