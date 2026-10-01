namespace EshopGuard.Core.Models;

/// <summary>
/// Kind of a text given to <see cref="IEshopGuard.AnalyzeTextsAsync"/>.
/// </summary>
public enum TextKind
{
    /// <summary>Marketing text such as a product description; split into sentences for the eco module.</summary>
    Sentence,

    /// <summary>Text of a legal page; split into paragraphs for the legal module and into sentences for the eco module.</summary>
    Legal,
}

/// <summary>
/// A text to evaluate without crawling, e.g. a product description from a database before publication.
/// </summary>
public sealed class TextInput
{
    /// <summary>The text. Line breaks separate blocks (paragraphs, list items).</summary>
    public required string Text { get; init; }

    /// <summary>Kind of the text.</summary>
    public TextKind Kind { get; init; } = TextKind.Sentence;

    /// <summary>Optional id of the text in the caller's system; used as its URL when <see cref="Url"/> is not set.</summary>
    public string? Id { get; init; }

    /// <summary>Optional URL where the text is published.</summary>
    public string? Url { get; init; }

    /// <summary>
    /// Optional product category (product name, breadcrumb trail or shop category) for rules that depend on it,
    /// such as the list of legal requirements (module lr). The sentence itself is never used as the category.
    /// </summary>
    public string? Category { get; init; }
}

/// <summary>
/// Estimate of Jev calls shown before the evaluation starts.
/// </summary>
public sealed class JevCallEstimate
{
    /// <summary>Number of requests to send (one per segment, with the questions of all its modules), cache hits excluded.</summary>
    public int Calls { get; init; }

    /// <summary>Answers of one module for one segment taken from the cache; they cost nothing.</summary>
    public int CachedCalls { get; init; }

    /// <summary>Estimated input tokens.</summary>
    public long EstimatedInputTokens { get; init; }

    /// <summary>Estimated price in USD.</summary>
    public decimal EstimatedCostUsd { get; init; }

    /// <summary>True when the mock client is used, so nothing is paid.</summary>
    public bool IsMock { get; init; }

    /// <summary>True when the number of calls exceeds the configured limit and the host must confirm.</summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    /// True when the estimate includes every sentence although the sieve will leave some out: an upper bound.
    /// </summary>
    public bool UpperBound { get; init; }

    /// <summary>Of <see cref="Calls"/>, requests of the sieve.</summary>
    public int SieveCalls { get; init; }

    /// <summary>New profiles of page templates the scan would write (at most; a new profile may fit more groups).</summary>
    public int ProfileTemplates { get; init; }

    /// <summary>Estimated price of the new profiles in USD (OpenAI, billed apart from Jev).</summary>
    public decimal ProfileCostUsd { get; init; }

    /// <summary>True when the new profiles will be written after confirmation.</summary>
    public bool ProfilesWillRun { get; init; }

    /// <summary>Why planned profiles will not be written (mock run, missing key), or null.</summary>
    public string? ProfilesUnavailableReason { get; init; }
}

/// <summary>
/// Result of <see cref="IEshopGuard.AnalyzeTextsAsync"/>.
/// </summary>
public sealed class AnalysisResult
{
    /// <summary>Unique segments with their probabilities.</summary>
    public IReadOnlyList<Segment> Segments { get; init; } = [];

    /// <summary>Findings.</summary>
    public IReadOnlyList<Finding> Findings { get; init; } = [];

    /// <summary>What every rule concluded for every segment, including rules without a finding.</summary>
    public IReadOnlyList<RuleResult> RuleResults { get; init; } = [];

    /// <summary>Rule sets used.</summary>
    public IReadOnlyList<RuleSetInfo> RuleSets { get; init; } = [];

    /// <summary>Model that answered, or <c>mock</c>.</summary>
    public string? JevModel { get; init; }

    /// <summary>Jev calls, errors, tokens and price.</summary>
    public required ScanStats Stats { get; init; }

    /// <summary>Problems worth showing, e.g. a disabled rule set.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
