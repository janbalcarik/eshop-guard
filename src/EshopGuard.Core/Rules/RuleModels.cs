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
/// A rule: how answers combine into a finding and what the finding says.
/// </summary>
public sealed class RuleDefinition
{
    /// <summary>Rule id.</summary>
    public string Id { get; set; } = "";

    /// <summary>Title shown in the report.</summary>
    public string Title { get; set; } = "";

    /// <summary><c>segment</c> or <c>site_presence</c>.</summary>
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

    /// <summary>Legal references with their verification status.</summary>
    public List<LegalReference> LegalRefs { get; set; } = [];

    /// <summary>Why this is a problem.</summary>
    public string Explanation { get; set; } = "";

    /// <summary>
    /// Explanation for a country whose law differs, e.g. for Czechia until the EmpCo directive is transposed.
    /// Keys are <c>cz</c> or <c>sk</c>; other countries get <see cref="Explanation"/>.
    /// </summary>
    public Dictionary<string, string>? ExplanationByJurisdiction { get; set; }

    /// <summary>What to do.</summary>
    public string Recommendation { get; set; } = "";

    /// <summary>Explanation for the given country.</summary>
    public string ExplanationFor(string country) =>
        ExplanationByJurisdiction is not null && ExplanationByJurisdiction.TryGetValue(country, out var specific) && !string.IsNullOrWhiteSpace(specific)
            ? specific
            : Explanation;
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
/// A legal reference with its verification status (e.g. "ověřit", "doplnit").
/// </summary>
public sealed class LegalReference
{
    /// <summary><c>eu</c>, <c>cz</c> or <c>sk</c>.</summary>
    public string Jurisdiction { get; set; } = "";

    /// <summary>The reference.</summary>
    public string Ref { get; set; } = "";

    /// <summary>Verification status.</summary>
    public string Status { get; set; } = "";
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
/// A remark on a label from <c>label_notes</c> in the labels file: the names it goes by and why it may not meet
/// the conditions of a sustainability label (for example that the owner of the scheme also certifies).
/// </summary>
public sealed class LabelNote
{
    /// <summary>Names found in the text as whole words, ignoring case, diacritics and punctuation.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];

    /// <summary>The remark shown with the finding.</summary>
    public string Note { get; init; } = "";
}
