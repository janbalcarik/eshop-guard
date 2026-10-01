using EshopGuard.Core.Rules;

namespace EshopGuard.Core.Models;

/// <summary>
/// Confidence band of a finding.
/// </summary>
public enum FindingBand
{
    /// <summary>"Vysoká jistota": the score reached the high threshold of the rule.</summary>
    High,

    /// <summary>"K ověření": a person should check the finding.</summary>
    Review,
}

/// <summary>
/// A potential problem found by a rule, either in one segment or for the whole site, with a verdict for every jurisdiction
/// where the rule found it. The finding carries codes and parameters only; titles, explanations and recommendations are
/// composed in the language of the reader by <see cref="Rules.Texts.RuleTextRenderer"/>.
/// </summary>
public sealed class Finding
{
    /// <summary>Rule id (the logical rule; rule sets of different jurisdictions may implement it with their own questions).</summary>
    public required string RuleId { get; init; }

    /// <summary>Rule module.</summary>
    public required string Module { get; init; }

    /// <summary><c>segment</c> for a finding in one text, <c>site</c> for information missing on the whole site.</summary>
    public required string Scope { get; init; }

    /// <summary>The sentence or paragraph as it is on the web; for a site finding the closest paragraph found, if any.</summary>
    public string? Text { get; init; }

    /// <summary>Preceding sentences.</summary>
    public string ContextBefore { get; init; } = "";

    /// <summary>Following sentences.</summary>
    public string ContextAfter { get; init; } = "";

    /// <summary>Parts of pages where the text occurs.</summary>
    public IReadOnlyList<SegmentSource> Sources { get; init; } = [];

    /// <summary>Pages the finding applies to.</summary>
    public IReadOnlyList<string> Urls { get; init; } = [];

    /// <summary>Number of pages the finding applies to.</summary>
    public int Occurrences => Urls.Count;

    /// <summary>True when the text is page frame or menu.</summary>
    public bool Boilerplate { get; init; }

    /// <summary>Hash of the segment the finding is based on.</summary>
    public string? SegmentHash { get; init; }

    /// <summary>Fingerprint of the sentence (<see cref="Segmentation.SentenceFingerprint"/>), for finding it again after small edits.</summary>
    public long? TextFingerprint { get; init; }

    /// <summary>Verdicts of the jurisdictions where the rule found the problem, ordered by <see cref="VerdictOrder"/>.</summary>
    public IReadOnlyList<JurisdictionVerdict> Verdicts { get; init; } = [];

    /// <summary>Parameters for the texts of the rule (none of today's rules uses any).</summary>
    public IReadOnlyDictionary<string, object?> Params { get; init; } = new Dictionary<string, object?>();

    /// <summary>The strictest verdict (<see cref="VerdictOrder"/>); the summary properties below come from it.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public JurisdictionVerdict Strictest => VerdictOrder.Strictest(Verdicts);

    /// <summary><c>high</c>, <c>medium</c> or <c>low</c> of the strictest verdict.</summary>
    public string Severity => Strictest.Severity;

    /// <summary>
    /// Group of the strictest verdict: <c>text</c> (violation by the text of the law), <c>assess</c> (depends on how the average
    /// consumer understands the text, case by case), <c>verify</c> (depends on facts outside the website) or <c>not_checkable</c>.
    /// </summary>
    public string Checkability => Strictest.Checkability;

    /// <summary>Confidence band of the strictest verdict.</summary>
    public FindingBand Band => Strictest.Band;

    /// <summary>Score of the strictest verdict.</summary>
    public double Score => Strictest.Score;

    /// <summary>Probabilities of the questions of the strictest verdict, by question id of its rule set.</summary>
    public IReadOnlyDictionary<string, double> QuestionProbs => Strictest.QuestionProbs;
}

/// <summary>
/// An image whose alt text or file name contains an environmental keyword; Jev does not see images.
/// </summary>
public sealed class ImageForReview
{
    /// <summary>Page with the image.</summary>
    public required string PageUrl { get; init; }

    /// <summary>Image URL.</summary>
    public required string Src { get; init; }

    /// <summary>File name.</summary>
    public required string FileName { get; init; }

    /// <summary>Alt text.</summary>
    public string? Alt { get; init; }

    /// <summary>Keyword that matched.</summary>
    public required string Keyword { get; init; }
}

/// <summary>
/// A rule set used in a run.
/// </summary>
public sealed class RuleSetInfo
{
    /// <summary>Module.</summary>
    public required string Module { get; init; }

    /// <summary>Version of the question set.</summary>
    public required string Version { get; init; }

    /// <summary>Source file.</summary>
    public required string File { get; init; }

    /// <summary>Name of the set (file name without extension); probabilities are keyed by <see cref="QuestionKey"/> with it.</summary>
    public string Name { get; init; } = "";

    /// <summary>Jurisdictions the set ran for in this run.</summary>
    public IReadOnlyList<string> Jurisdictions { get; init; } = [];

    /// <summary>Question ids in the order of the file.</summary>
    public IReadOnlyList<string> QuestionIds { get; init; } = [];
}

/// <summary>
/// What a rule concluded for one segment, including rules that did not produce a finding (for <c>check-text</c>).
/// </summary>
public sealed class RuleResult
{
    /// <summary>Rule id.</summary>
    public required string RuleId { get; init; }

    /// <summary>Segment the result is about; null for site rules.</summary>
    public string? SegmentHash { get; init; }

    /// <summary>Jurisdiction the rule was evaluated for.</summary>
    public string Jurisdiction { get; init; } = "";

    /// <summary>Name of the rule set.</summary>
    public string RuleSet { get; init; } = "";

    /// <summary>What happened.</summary>
    public RuleOutcome Outcome { get; init; }

    /// <summary>Computed score, if the rule got that far.</summary>
    public double? Score { get; init; }

    /// <summary>Band of the finding, if there is one.</summary>
    public FindingBand? Band { get; init; }
}

/// <summary>
/// Outcome of a rule for one segment or for the site.
/// </summary>
public enum RuleOutcome
{
    /// <summary>A finding was produced.</summary>
    Finding,

    /// <summary>The conditions of the rule did not hold.</summary>
    ConditionsNotMet,

    /// <summary>The conditions held but the score is below the review band.</summary>
    BelowThreshold,

    /// <summary>An item from the allowlist was found, so there is no finding.</summary>
    ExcludedByAllowlist,

    /// <summary>The list of legal requirements does not give the outcome the rule needs for the claim and the product category.</summary>
    NotInList,

    /// <summary>The sieve found no topic of the module in any chunk with the segment, so its questions were not asked.</summary>
    SkippedBySieve,

    /// <summary>The same text is already a finding of the rule on the page (title or meta description repeating the page text).</summary>
    Duplicate,

    /// <summary>The required information is present.</summary>
    Present,

    /// <summary>Some question probabilities are missing (Jev error).</summary>
    NotEvaluated,
}
