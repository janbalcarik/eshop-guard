using EshopGuard.Core.Models;

namespace EshopGuard.Core.Options;

/// <summary>
/// Options of one scan. Values left null fall back to <see cref="EshopGuardOptions"/>.
/// </summary>
public sealed class ScanOptions
{
    /// <summary>Maximum number of downloaded pages.</summary>
    public int? MaxPages { get; init; }

    /// <summary>Maximum number of product pages included in the analysis.</summary>
    public int? SampleProducts { get; init; }

    /// <summary>Rule modules to run, e.g. <c>eco</c> and <c>legal</c>.</summary>
    public IReadOnlyList<string> Modules { get; init; } = [];

    /// <summary>Jurisdiction of legal rules and language of the report (<c>cz</c> or <c>sk</c>).</summary>
    public string Country { get; init; } = "sk";

    /// <summary>Language of the questions sent to Jev (<c>en</c> or <c>cs</c>).</summary>
    public string QuestionLanguage { get; init; } = "en";

    /// <summary>Requests per second to the scanned site.</summary>
    public double? RequestsPerSecond { get; init; }

    /// <summary>Parallel requests to Jev.</summary>
    public int? Concurrency { get; init; }

    /// <summary>Only URLs matching at least one of these regular expressions are downloaded (the home page always is).</summary>
    public IReadOnlyList<string> Include { get; init; } = [];

    /// <summary>URLs matching any of these regular expressions are not downloaded.</summary>
    public IReadOnlyList<string> Exclude { get; init; } = [];

    /// <summary>
    /// When false, every sentence goes to the detailed questions of every module (no block sieve).
    /// </summary>
    public bool UseSieve { get; init; } = true;

    /// <summary>
    /// Called with the estimate after crawling and before Jev is called. The library never prints anything;
    /// the host shows the estimate and, when <see cref="JevCallEstimate.RequiresConfirmation"/> is set, asks the user.
    /// Returning false skips the evaluation; pages and segments are still returned.
    /// </summary>
    public Func<JevCallEstimate, CancellationToken, Task<bool>>? ConfirmJevCalls { get; init; }
}
