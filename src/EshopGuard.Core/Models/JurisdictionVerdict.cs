using EshopGuard.Core.Rules;

namespace EshopGuard.Core.Models;

/// <summary>
/// Whether an obligation already applies on the date of the evaluation.
/// </summary>
public enum VerdictStatus
{
    /// <summary>The rule applies; the finding counts.</summary>
    Finding,

    /// <summary>The rule applies only from <see cref="JurisdictionVerdict.EffectiveFrom"/>; the finding is a warning ahead.</summary>
    Upcoming,
}

/// <summary>
/// What one rule concluded about a finding in one jurisdiction: band, severity, group, legal references of the EU and of
/// the jurisdiction, the rule set it comes from and the notes of the tool as codes.
/// </summary>
public sealed class JurisdictionVerdict
{
    /// <summary>Jurisdiction code from the rule set, e.g. <c>sk</c> or <c>cz</c>.</summary>
    public required string Jurisdiction { get; init; }

    /// <summary><see cref="VerdictStatus.Upcoming"/> before the rule takes effect in the jurisdiction.</summary>
    public VerdictStatus Status { get; init; }

    /// <summary>Confidence band.</summary>
    public FindingBand Band { get; init; }

    /// <summary>Score from 0 to 1 computed from the question probabilities.</summary>
    public double Score { get; init; }

    /// <summary><c>high</c>, <c>medium</c> or <c>low</c>, after <c>jurisdiction_overrides</c>.</summary>
    public required string Severity { get; init; }

    /// <summary><c>text</c>, <c>assess</c>, <c>verify</c> or <c>not_checkable</c>, after <c>jurisdiction_overrides</c>.</summary>
    public required string Checkability { get; init; }

    /// <summary>Legal references of the EU and of this jurisdiction, in the language of the law.</summary>
    public IReadOnlyList<LegalReference> LegalRefs { get; init; } = [];

    /// <summary>Name of the rule set (file name without extension, e.g. <c>legal_sk</c>); its texts render the verdict.</summary>
    public required string RuleSet { get; init; }

    /// <summary>Version of the rule set.</summary>
    public required string RuleSetVersion { get; init; }

    /// <summary><c>default</c>, or the jurisdiction code when the rule has its own explanation for it.</summary>
    public string ExplanationVariant { get; init; } = DefaultVariant;

    /// <summary>Date from which the rule applies in the jurisdiction; null when it applies without a date.</summary>
    public DateOnly? EffectiveFrom { get; init; }

    /// <summary>Notes of the tool, e.g. the probability of the closest paragraph.</summary>
    public IReadOnlyList<FindingNote> Notes { get; init; } = [];

    /// <summary>Probabilities of the questions the rule used, by question id of <see cref="RuleSet"/>.</summary>
    public IReadOnlyDictionary<string, double> QuestionProbs { get; init; } = new Dictionary<string, double>();

    /// <summary>Value of <see cref="ExplanationVariant"/> for the general explanation.</summary>
    public const string DefaultVariant = "default";
}
