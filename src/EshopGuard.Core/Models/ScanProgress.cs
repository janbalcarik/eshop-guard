namespace EshopGuard.Core.Models;

/// <summary>
/// Stage of a scan reported through <see cref="IProgress{T}"/>.
/// </summary>
public enum ScanStage
{
    /// <summary>Reading robots.txt and sitemaps.</summary>
    Discovery,

    /// <summary>Downloading pages.</summary>
    Pages,

    /// <summary>Splitting texts into segments and deduplicating them.</summary>
    Segmentation,

    /// <summary>Asking the sieve which chunks of the main text contain the topics of the modules.</summary>
    Sieve,

    /// <summary>Evaluating segments with Jev.</summary>
    Evaluation,
}

/// <summary>
/// Progress of a scan.
/// </summary>
public sealed class ScanProgress
{
    /// <summary>Current stage.</summary>
    public ScanStage Stage { get; init; }

    /// <summary>Finished items in the stage.</summary>
    public int Completed { get; init; }

    /// <summary>Expected items in the stage (may grow while links are discovered).</summary>
    public int Total { get; init; }

    /// <summary>URL being processed, if any.</summary>
    public string? CurrentUrl { get; init; }
}
