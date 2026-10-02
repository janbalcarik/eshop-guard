using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Checks;

/// <summary>States of an address of a run (<c>checks.run_urls.state</c>).</summary>
public enum RunUrlState
{
    Pending,
    Fetched,
    Extracted,
    Failed,
    RobotsBlocked,
    Excluded,
    OverLimit,
    NotLoaded,
    ExtractTimeout,
    TooLarge,
    OffsiteRedirect,
    SsrfBlocked,
    Gone,
    NotHtml,
    NotModified,
}

/// <summary>
/// The outcome of one address of a run (change 8): what happened to it, its page and the code why it was not checked. Together
/// with <see cref="RunScope"/> it is the list of what was and was not checked (fail-closed). Table <c>checks.run_urls</c>.
/// </summary>
public sealed class RunUrl : ITenantOwned, IHasTimestamps
{
    public Guid TenantId { get; set; }

    public Guid RunId { get; set; }

    /// <summary>Crawl scope: the root address of the language version.</summary>
    public required string ScopeKey { get; set; }

    /// <summary>64-bit fingerprint of the normalized address, the same as <c>pages.url_hash</c>.</summary>
    public long UrlHash { get; set; }

    public required string Url { get; set; }

    public string? Language { get; set; }

    public RunUrlState State { get; set; }

    /// <summary>
    /// Where the address came from: <c>home</c>, <c>legal</c>, <c>product</c>, <c>other</c> (the crawl) or the plan of the free
    /// sample <c>sample_pair</c>, <c>sample_mandatory</c>, <c>sample_random</c>; null when not known.
    /// </summary>
    public string? Queue { get; set; }

    /// <summary>Order in which the scope processed the address; later steps read the pages in this order, as the CLI does.</summary>
    public int? Seq { get; set; }

    /// <summary>Number of the batch that processed the address.</summary>
    public int? BatchNo { get; set; }

    public short Attempts { get; set; }

    public short? HttpStatus { get; set; }

    /// <summary>Code of the reason, never a text of the page.</summary>
    public string? ErrorCode { get; set; }

    public Guid? PageId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// One crawl scope of a run (a language version, or the whole site): its definition, robots.txt as read, the frontier of the
/// crawl and the pace, carried from batch to batch in the transaction that completes the batch. Table <c>checks.run_scopes</c>.
/// </summary>
public sealed class RunScope : ITenantOwned, IHasTimestamps
{
    public Guid TenantId { get; set; }

    public Guid RunId { get; set; }

    public required string ScopeKey { get; set; }

    /// <summary>Home address of the scope.</summary>
    public required string BaseUrl { get; set; }

    public string? Language { get; set; }

    /// <summary>The language version (<c>VersionCrawlScope</c>); null for the whole site.</summary>
    public JsonDocument? Scope { get; set; }

    /// <summary><c>RobotsSnapshot</c>.</summary>
    public required JsonDocument Robots { get; set; }

    /// <summary><c>UrlFrontierState</c>.</summary>
    public required JsonDocument Frontier { get; set; }

    /// <summary><c>PaceState</c>.</summary>
    public required JsonDocument Pace { get; set; }

    /// <summary>Nothing is left to download.</summary>
    public bool Exhausted { get; set; }

    /// <summary>Batches of downloads completed.</summary>
    public int Batches { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
