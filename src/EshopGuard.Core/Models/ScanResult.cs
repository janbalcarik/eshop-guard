using EshopGuard.Core.Profiles;

namespace EshopGuard.Core.Models;

/// <summary>
/// Result of scanning one site. A plain object model that serializes to JSON.
/// </summary>
public sealed class ScanResult
{
    /// <summary>The scanned site as requested.</summary>
    public required string SiteUrl { get; init; }

    /// <summary>Start of the scan.</summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>End of the scan.</summary>
    public DateTimeOffset FinishedAt { get; init; }

    /// <summary>Rule modules requested for the scan.</summary>
    public IReadOnlyList<string> Modules { get; init; } = [];

    /// <summary>The first chosen jurisdiction (the only one in a run for one country).</summary>
    public string Country { get; init; } = "sk";

    /// <summary>Jurisdictions the rules were evaluated for.</summary>
    public IReadOnlyList<string> Jurisdictions { get; init; } = [];

    /// <summary>Date of the evaluation: rules with a later <c>effective_from</c> give upcoming verdicts.</summary>
    public DateOnly AsOf { get; init; }

    /// <summary>Language of the questions sent to Jev.</summary>
    public string QuestionLanguage { get; init; } = "en";

    /// <summary>Downloaded pages.</summary>
    public IReadOnlyList<PageInfo> Pages { get; init; } = [];

    /// <summary>Unique segments with their question probabilities.</summary>
    public IReadOnlyList<Segment> Segments { get; init; } = [];

    /// <summary>Answers of the sieve for every chunk of the main text (empty when the sieve is off).</summary>
    public IReadOnlyList<SieveChunkResult> SieveChunks { get; init; } = [];

    /// <summary>Findings of all rules.</summary>
    public IReadOnlyList<Finding> Findings { get; init; } = [];

    /// <summary>Images whose alt text or file name contains an environmental keyword, for manual review.</summary>
    public IReadOnlyList<ImageForReview> ImagesForReview { get; init; } = [];

    /// <summary>Rule sets used in the run.</summary>
    public IReadOnlyList<RuleSetInfo> RuleSets { get; init; } = [];

    /// <summary>Model that answered, or <c>mock</c> for the test double.</summary>
    public string? JevModel { get; init; }

    /// <summary>True when Jev was not called because the host did not confirm the estimate.</summary>
    public bool EvaluationSkipped { get; init; }

    /// <summary>Legal documents that were found but not read (PDF).</summary>
    public IReadOnlyList<UncheckedDocument> UncheckedDocuments { get; init; } = [];

    /// <summary>URLs that were not downloaded because robots.txt disallows them.</summary>
    public IReadOnlyList<string> RobotsBlockedUrls { get; init; } = [];

    /// <summary>URLs that were not downloaded because their address leads into an internal or local network (SSRF protection).</summary>
    public IReadOnlyList<string> BlockedUrls { get; init; } = [];

    /// <summary>Pages that were downloaded but not read (the extraction took too long); they were not checked.</summary>
    public IReadOnlyList<NotProcessedPage> NotProcessedPages { get; init; } = [];

    /// <summary>Profiles of page templates used in the scan, with the pages that used them and what they left out.</summary>
    public IReadOnlyList<ProfileUse> Profiles { get; init; } = [];

    /// <summary>Problems worth showing in the report, e.g. unreadable sitemap or robots.txt, as codes.</summary>
    public IReadOnlyList<ScanWarning> Warnings { get; init; } = [];

    /// <summary>Obligations for the whole site in every chosen jurisdiction.</summary>
    public IReadOnlyList<SiteObligation> SiteObligations { get; init; } = [];

    /// <summary>Which modules ran for which jurisdiction.</summary>
    public IReadOnlyList<JurisdictionCoverage> JurisdictionCoverage { get; init; } = [];

    /// <summary>Statistics of the run.</summary>
    public required ScanStats Stats { get; init; }
}

/// <summary>
/// A downloaded page whose text was not read, so nothing on it was checked.
/// </summary>
public sealed class NotProcessedPage
{
    /// <summary>Final URL of the page.</summary>
    public required string Url { get; init; }

    /// <summary>Why: <c>extract_timeout</c> (the extraction took longer than <c>crawl.extract_timeout_seconds</c>).</summary>
    public required string Reason { get; init; }
}
