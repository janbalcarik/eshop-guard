using YamlDotNet.Serialization;

namespace EshopGuard.Core.Rules;

/// <summary>
/// All rule sets and label lists, as loaded by <see cref="IRuleSetProvider"/>.
/// </summary>
public sealed class RuleCatalog
{
    /// <summary>Rule sets, one per YAML file.</summary>
    public IReadOnlyList<RuleSet> RuleSets { get; init; } = [];

    /// <summary>Label lists and image keywords.</summary>
    public LabelConfiguration Labels { get; init; } = new();

    /// <summary>Legal requirements for all products of a category (SK point 15), for <c>claim_list_match</c>.</summary>
    public LegalRequirementList LegalRequirements { get; init; } = LegalRequirementList.Empty;

    /// <summary>The block sieve; null when its file is missing.</summary>
    public SieveDefinition? Sieve { get; init; }

    /// <summary>Known jurisdictions from <c>config/jurisdictions.yaml</c>.</summary>
    public JurisdictionRegistry Jurisdictions { get; init; } = JurisdictionRegistry.Empty;

    /// <summary>Texts of the rules, the tool and the labels in every language of <c>rules/texts/</c>.</summary>
    public Texts.RuleTexts Texts { get; init; } = Rules.Texts.RuleTexts.Empty;

    /// <summary>
    /// Languages in which every enabled rule set, the tool and the labels have complete texts written or reviewed by a person;
    /// only these can be offered to users.
    /// </summary>
    public IReadOnlyList<string> CompleteLocales => Texts.CompleteLocales;

    /// <summary>What is stored about every rule set version (<see cref="RuleSetDescriptor"/>) for a model and question language.</summary>
    public IReadOnlyList<RuleSetDescriptor> Describe(string model, string questionLanguage = "en") =>
        RuleSetDescriptor.Describe(this, model, questionLanguage);
}

/// <summary>
/// One rule set (a YAML file in <c>rules/</c>): questions for Jev and rules that combine the answers.
/// </summary>
public sealed class RuleSet
{
    /// <summary>Version of the question set; part of the cache key and of the report header.</summary>
    public string Version { get; set; } = "";

    /// <summary>Module name, e.g. <c>eco</c> or <c>legal</c>.</summary>
    public string Module { get; set; } = "";

    /// <summary><c>sentence</c> or <c>legal_paragraph</c>.</summary>
    public string AppliesTo { get; set; } = "";

    /// <summary>Countries the set applies to (<c>cz</c>, <c>sk</c>).</summary>
    public List<string> Jurisdictions { get; set; } = [];

    /// <summary>A disabled set is loaded and validated but never run.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary><c>site_presence</c>: information is present when the best paragraph reaches this probability.</summary>
    public double PresenceThreshold { get; set; } = 0.7;

    /// <summary>Questions keyed by id.</summary>
    public Dictionary<string, QuestionDefinition> Questions { get; set; } = [];

    /// <summary>Rules of the set.</summary>
    public List<RuleDefinition> Rules { get; set; } = [];

    /// <summary>File the set was loaded from.</summary>
    public string SourceFile { get; set; } = "";

    /// <summary>Name of the set: file name without extension (e.g. <c>legal_sk</c>); texts and answers are keyed by it.</summary>
    [YamlIgnore]
    public string Name => Path.GetFileNameWithoutExtension(SourceFile);

    /// <summary>
    /// Rule ids with an explanation of their own for some jurisdictions, from the original texts of the set; a verdict for such a
    /// jurisdiction gets that explanation variant.
    /// </summary>
    [YamlIgnore]
    public IReadOnlyDictionary<string, IReadOnlySet<string>> ExplanationVariants { get; set; } = new Dictionary<string, IReadOnlySet<string>>();
}

/// <summary>
/// A question for Jev in English and in the language of the country.
/// </summary>
public sealed class QuestionDefinition
{
    /// <summary><c>yes_no</c>, <c>choice</c> or <c>score</c>.</summary>
    public string Type { get; set; } = "yes_no";

    /// <summary>Question in English.</summary>
    public string TextEn { get; set; } = "";

    /// <summary>Question in the language of the country (Czech; Slovak in Slovak rule sets).</summary>
    public string TextCs { get; set; } = "";

    /// <summary><c>choice</c>: options and their descriptions.</summary>
    public Dictionary<string, string>? Options { get; set; }

    /// <summary><c>score</c>: ordered levels.</summary>
    public List<string>? Levels { get; set; }
}

/// <summary>
/// A rule: how answers combine into a finding. Its texts (title, explanation, recommendation, questions for the user) are in
/// <c>rules/texts/&lt;locale&gt;/&lt;set&gt;.yaml</c>; only disabled sets may still keep them inline until they are enabled.
/// </summary>
public sealed class RuleDefinition
{
    /// <summary>Rule id.</summary>
    public string Id { get; set; } = "";

    /// <summary><c>segment</c>, <c>site_presence</c> or <c>site_signal</c>.</summary>
    public string Scope { get; set; } = "segment";

    /// <summary><c>segment</c>: conditions on question probabilities.</summary>
    public RuleLogic? Logic { get; set; }

    /// <summary><c>site_presence</c>: the question that must be answered "yes" by at least one legal paragraph.</summary>
    public string? Question { get; set; }

    /// <summary>Checks done by code (allowlists, regular expressions).</summary>
    public List<CodeCheck> CodeChecks { get; set; } = [];

    /// <summary>Score thresholds of the bands.</summary>
    public Bands Bands { get; set; } = new();

    /// <summary><c>high</c>, <c>medium</c> or <c>low</c>.</summary>
    public string Severity { get; set; } = "medium";

    /// <summary>
    /// Group of the finding: <c>text</c> (violation by the text of the law), <c>assess</c> (depends on how the average
    /// consumer understands the text, case by case), <c>verify</c> (depends on facts outside the website) or <c>not_checkable</c>.
    /// </summary>
    public string Checkability { get; set; } = "text";

    /// <summary>Legal references with their verification status, in the language of the law.</summary>
    public List<LegalReference> LegalRefs { get; set; } = [];

    /// <summary>Date from which the rule applies, by jurisdiction; a jurisdiction without a date applies already.</summary>
    public Dictionary<string, DateOnly> EffectiveFrom { get; set; } = [];

    /// <summary>Severity, group or bands that differ in a jurisdiction of the set.</summary>
    public Dictionary<string, RuleOverride> JurisdictionOverrides { get; set; } = [];

    /// <summary>Codes of the questions for the user (texts in the text files), e.g. <c>evidence_available</c>.</summary>
    public List<string> UserQuestions { get; set; } = [];

    /// <summary>Inline title; allowed only in disabled sets (until their texts are moved to <c>rules/texts</c>).</summary>
    public string? Title { get; set; }

    /// <summary>Inline explanation; allowed only in disabled sets.</summary>
    public string? Explanation { get; set; }

    /// <summary>Inline explanations by jurisdiction; allowed only in disabled sets.</summary>
    public Dictionary<string, string>? ExplanationByJurisdiction { get; set; }

    /// <summary>Inline recommendation; allowed only in disabled sets.</summary>
    public string? Recommendation { get; set; }

    /// <summary>True when the rule still has inline texts.</summary>
    [YamlIgnore]
    public bool HasInlineTexts => Title is not null || Explanation is not null || ExplanationByJurisdiction is not null || Recommendation is not null;

    /// <summary>Severity in the jurisdiction.</summary>
    public string SeverityFor(string jurisdiction) =>
        JurisdictionOverrides.TryGetValue(jurisdiction, out var o) && o.Severity is { } severity ? severity : Severity;

    /// <summary>Group of the finding in the jurisdiction.</summary>
    public string CheckabilityFor(string jurisdiction) =>
        JurisdictionOverrides.TryGetValue(jurisdiction, out var o) && o.Checkability is { } checkability ? checkability : Checkability;

    /// <summary>Bands in the jurisdiction.</summary>
    public Bands BandsFor(string jurisdiction) =>
        JurisdictionOverrides.TryGetValue(jurisdiction, out var o) && o.Bands is { } bands ? bands : Bands;

    /// <summary>Date from which the rule applies in the jurisdiction, or null.</summary>
    public DateOnly? EffectiveFromFor(string jurisdiction) =>
        EffectiveFrom.TryGetValue(jurisdiction, out var date) ? date : null;
}

/// <summary>
/// What differs for one jurisdiction of a rule set (<c>jurisdiction_overrides</c>); empty fields keep the value of the rule.
/// </summary>
public sealed class RuleOverride
{
    /// <summary><c>high</c>, <c>medium</c> or <c>low</c>.</summary>
    public string? Severity { get; set; }

    /// <summary><c>text</c>, <c>assess</c>, <c>verify</c> or <c>not_checkable</c>.</summary>
    public string? Checkability { get; set; }

    /// <summary>Score thresholds of the bands.</summary>
    public Bands? Bands { get; set; }
}

/// <summary>
/// Conditions of a segment rule. All of <see cref="All"/> must hold, at least one of <see cref="Any"/>, none of <see cref="None"/>.
/// </summary>
public sealed class RuleLogic
{
    /// <summary>All must reach their threshold; the score is their minimum.</summary>
    public List<RuleCondition> All { get; set; } = [];

    /// <summary>At least one must reach its threshold; the score is their maximum.</summary>
    public List<RuleCondition> Any { get; set; } = [];

    /// <summary>None may reach its threshold; each enters the score as 1 minus its probability.</summary>
    public List<RuleCondition> None { get; set; } = [];
}

/// <summary>
/// A condition on the probability of one yes/no question.
/// </summary>
public sealed class RuleCondition
{
    /// <summary>Question id.</summary>
    public string Q { get; set; } = "";

    /// <summary>Threshold the probability must reach.</summary>
    public double Gte { get; set; } = 0.5;
}

/// <summary>
/// A check done by code: <c>allowlist_absent</c>, <c>regex_required</c>, <c>site_pattern_required</c>,
/// <c>site_pattern_forbidden</c>, <c>claim_list_match</c> or <c>label_notes</c>.
/// </summary>
public sealed class CodeCheck
{
    /// <summary>Type of the check.</summary>
    public string Type { get; set; } = "";

    /// <summary><c>allowlist_absent</c>: name of the list in the labels file; <c>claim_list_match</c>: <c>legal_requirement_claims</c>.</summary>
    public string? List { get; set; }

    /// <summary>
    /// <c>claim_list_match</c>: the sentence makes the claim of an item whose category matches the page and whose outcome
    /// is one of these (<c>text</c>, <c>verify</c>, <c>none</c>, <c>irrelevant</c>).
    /// </summary>
    public List<string>? Outcomes { get; set; }

    /// <summary>
    /// <c>claim_list_match</c>: the check holds when no item matches; with <see cref="Outcomes"/>, when no item with one of
    /// those outcomes matches.
    /// </summary>
    public bool Absent { get; set; }

    /// <summary><c>allowlist_absent</c>: <c>segment</c> (default) or <c>page</c>.</summary>
    public string? Where { get; set; }

    /// <summary><c>regex_required</c>: .NET regular expression.</summary>
    public string? Pattern { get; set; }
}

/// <summary>
/// Score thresholds: from <see cref="High"/> "high confidence", from <see cref="Review"/> "to review".
/// </summary>
public sealed class Bands
{
    /// <summary>Lower bound of the high confidence band.</summary>
    public double High { get; set; } = 0.85;

    /// <summary>Lower bound of the review band; segment findings below it are ignored.</summary>
    public double Review { get; set; } = 0.5;
}

/// <summary>
/// A legal reference in the language of the law with its verification status as a code (<c>to_verify</c>, <c>to_complete</c>).
/// </summary>
public sealed class LegalReference
{
    /// <summary><c>eu</c> or a jurisdiction code.</summary>
    public string Jurisdiction { get; set; } = "";

    /// <summary>The reference in the language of the law (EU references in Czech).</summary>
    public string Ref { get; set; } = "";

    /// <summary>EU references only: the reference in other languages, by locale; shown in the language of the law of the verdict.</summary>
    public Dictionary<string, string>? RefByLanguage { get; set; }

    /// <summary>Verification status code.</summary>
    public string Status { get; set; } = "";

    /// <summary>The reference for a verdict whose law is in <paramref name="lawLanguage"/>.</summary>
    public string RefFor(string? lawLanguage) =>
        lawLanguage is not null && RefByLanguage is not null && RefByLanguage.TryGetValue(lawLanguage, out var text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : Ref;
}

/// <summary>
/// Label lists and keywords for image review, from <c>config/labels.yaml</c>.
/// </summary>
public sealed class LabelConfiguration
{
    /// <summary>Named lists of labels.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Lists { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Keywords that flag image alt texts and file names for manual review.</summary>
    public IReadOnlyList<string> EcoImageKeywords { get; init; } = [];

    /// <summary>Remarks on particular labels, added to findings of rules with a <c>label_notes</c> check.</summary>
    public IReadOnlyList<LabelNote> Notes { get; init; } = [];
}

/// <summary>
/// A remark on a label from <c>label_notes</c> in the labels file: the names it goes by; the remark itself (why it may not
/// meet the conditions of a sustainability label) is text <see cref="Id"/> in <c>rules/texts/&lt;locale&gt;/_labels.yaml</c>.
/// </summary>
public sealed class LabelNote
{
    /// <summary>Id of the remark.</summary>
    public string Id { get; init; } = "";

    /// <summary>Names found in the text as whole words, ignoring case, diacritics and punctuation.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];
}
