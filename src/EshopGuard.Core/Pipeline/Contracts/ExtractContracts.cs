using EshopGuard.Core.Extract;
using EshopGuard.Core.Models;
using EshopGuard.Core.Profiles;

namespace EshopGuard.Core.Pipeline;

/// <summary>Whether the text of a downloaded page was read.</summary>
internal enum ExtractionStatus
{
    /// <summary>Extracted and classified.</summary>
    Ok,

    /// <summary>
    /// Not read (the extraction took longer than <c>crawl.extract_timeout_seconds</c>); the page is listed among the pages
    /// that were not checked and never passes for a page without findings.
    /// </summary>
    NotProcessed,
}

/// <summary>The stored or new profile a page uses, with the characters it left out by role.</summary>
internal sealed record ProfileFit(string ProfileId, IReadOnlyDictionary<string, int> SkippedCharsByRole);

/// <summary>
/// One extracted page: what the report shows (<see cref="Info"/>), the text blocks for segmentation and rules
/// (<see cref="Content"/>), the structure of the page for profiles of templates and the hash of its text for versions.
/// </summary>
internal sealed class ExtractedPageRecord : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;

    public required PageInfo Info { get; init; }

    /// <summary>The blocks; replaced when a profile is applied.</summary>
    public required ExtractedPage Content { get; set; }

    public ExtractionStatus Status { get; init; } = ExtractionStatus.Ok;

    /// <summary>Why the page was not read (<c>extract_timeout</c>), or null.</summary>
    public string? NotProcessedReason { get; init; }

    public bool IsHome { get; init; }

    /// <summary>SHA-256 of the readable text (title, descriptions, main text, frame, other text); a new version only when it changes.</summary>
    public string TextHash { get; init; } = "";

    /// <summary>
    /// The page may use a profile of its template: profiles are enabled, it is not a legal page (missing information there is a
    /// finding of its own) and its text was loaded.
    /// </summary>
    public bool ProfileEligible { get; init; }

    /// <summary>Tags and classes of the page structure (<see cref="ProfileMatcher.StructureTokens"/>), for grouping pages by template.</summary>
    public HashSet<string> StructureTokens { get; init; } = [];

    /// <summary>The profile the page uses, or null (the page is checked whole).</summary>
    public ProfileFit? Fit { get; set; }
}

/// <summary>Input of <see cref="ExtractStep.ExtractBatchAsync"/>: pages of one batch whose HTML is in the content store.</summary>
internal sealed record ExtractInput(SiteScope Site, IReadOnlyList<FetchedPage> Pages) : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;

    /// <summary>Stored profiles of the site's templates, applied to the pages.</summary>
    public IReadOnlyList<PageProfile> StoredProfiles { get; init; } = [];
}

/// <summary>Output of <see cref="ExtractStep.ExtractBatchAsync"/>.</summary>
internal sealed record ExtractResult(IReadOnlyList<ExtractedPageRecord> Pages) : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;
}
