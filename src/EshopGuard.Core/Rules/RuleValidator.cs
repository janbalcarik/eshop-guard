using System.Text.RegularExpressions;

namespace EshopGuard.Core.Rules;

/// <summary>
/// Checks rule sets for mistakes that would otherwise silently change results: unknown questions,
/// wrong scopes, missing lists, invalid regular expressions, duplicate ids.
/// </summary>
internal static class RuleValidator
{
    public const string Sentence = "sentence";
    public const string LegalParagraph = "legal_paragraph";
    public const string SegmentScope = "segment";
    public const string SitePresenceScope = "site_presence";
    public const string SiteSignalScope = "site_signal";
    public const string AllowlistAbsent = "allowlist_absent";
    public const string RegexRequired = "regex_required";
    public const string SitePatternRequired = "site_pattern_required";
    public const string SitePatternForbidden = "site_pattern_forbidden";
    public const string ClaimListMatch = "claim_list_match";
    public const string LabelNotes = "label_notes";
    public static readonly IReadOnlySet<string> SignalSources = new HashSet<string>(StringComparer.Ordinal) { "all", "text", "links", "images" };

    private static readonly HashSet<string> QuestionTypes = ["yes_no", "choice", "score"];
    private static readonly HashSet<string> Countries = ["cz", "sk"];
    private static readonly HashSet<string> Severities = ["high", "medium", "low"];
    private static readonly HashSet<string> Checkabilities = ["text", "assess", "verify", "not_checkable"];
    private static readonly HashSet<string> RefJurisdictions = ["eu", "cz", "sk"];

    public static List<string> Validate(IReadOnlyList<RuleSet> ruleSets, LabelConfiguration labels, LegalRequirementList? legalRequirements = null)
    {
        var errors = new List<string>();
        foreach (var set in ruleSets)
        {
            ValidateSet(set, labels, legalRequirements ?? LegalRequirementList.Empty, errors);
        }

        // Sets that can run for the same country must not share rule or question ids.
        for (var i = 0; i < ruleSets.Count; i++)
        {
            for (var j = i + 1; j < ruleSets.Count; j++)
            {
                var (a, b) = (ruleSets[i], ruleSets[j]);
                if (!a.Jurisdictions.Intersect(b.Jurisdictions).Any())
                {
                    continue;
                }

                foreach (var id in a.Rules.Select(r => r.Id).Intersect(b.Rules.Select(r => r.Id)))
                {
                    errors.Add($"{a.SourceFile} a {b.SourceFile}: pravidlo „{id}“ je definované dvakrát pro stejnou zemi.");
                }

                foreach (var id in a.Questions.Keys.Intersect(b.Questions.Keys))
                {
                    errors.Add($"{a.SourceFile} a {b.SourceFile}: otázka „{id}“ je definovaná dvakrát pro stejnou zemi.");
                }
            }
        }

        return errors;
    }

    /// <summary>The sieve needs a version, a threshold between 0 and 1, sensible chunk sizes and questions of sentence modules.</summary>
    public static List<string> ValidateSieve(SieveDefinition sieve, IReadOnlyList<RuleSet> ruleSets, string fileName)
    {
        var errors = new List<string>();
        void Error(string message) => errors.Add($"{fileName}: {message}");
        if (string.IsNullOrWhiteSpace(sieve.Version))
        {
            Error("chybí version.");
        }

        if (sieve.Threshold is <= 0 or >= 1)
        {
            Error($"threshold musí být mezi 0 a 1, je {sieve.Threshold}.");
        }

        if (sieve.MaxChunkChars <= 0 || sieve.MaxChunkChars >= sieve.MaxRequestChars)
        {
            Error("max_chunk_chars musí být kladné a menší než max_request_chars.");
        }

        var sentenceModules = ruleSets.Where(s => s.AppliesTo == Sentence).Select(s => s.Module).ToHashSet();
        foreach (var (module, question) in sieve.Questions)
        {
            if (!sentenceModules.Contains(module))
            {
                Error($"otázka síta pro modul „{module}“, který nemá sadu pravidel pro věty.");
            }

            if (string.IsNullOrWhiteSpace(question.TextEn) || string.IsNullOrWhiteSpace(question.TextCs))
            {
                Error($"otázka síta pro modul „{module}“ potřebuje text_en i text_cs.");
            }
        }

        return errors;
    }

    private static void ValidateSet(RuleSet set, LabelConfiguration labels, LegalRequirementList legalRequirements, List<string> errors)
    {
        var file = set.SourceFile;
        void Error(string message) => errors.Add($"{file}: {message}");

        if (string.IsNullOrWhiteSpace(set.Version))
        {
            Error("chybí version.");
        }

        if (string.IsNullOrWhiteSpace(set.Module))
        {
            Error("chybí module.");
        }

        if (set.AppliesTo is not (Sentence or LegalParagraph))
        {
            Error($"applies_to musí být {Sentence} nebo {LegalParagraph}, je „{set.AppliesTo}“.");
        }

        if (set.Jurisdictions.Count == 0 || set.Jurisdictions.Any(j => !Countries.Contains(j)))
        {
            Error($"jurisdictions musí obsahovat cz nebo sk, je [{string.Join(", ", set.Jurisdictions)}].");
        }

        if (set.PresenceThreshold is <= 0 or > 1)
        {
            Error("presence_threshold musí být mezi 0 a 1.");
        }

        if (set.Questions.Count == 0)
        {
            Error("sada nemá žádné otázky.");
        }

        foreach (var (id, question) in set.Questions)
        {
            ValidateQuestion(id, question, Error);
        }

        if (set.Rules.Count == 0)
        {
            Error("sada nemá žádná pravidla.");
        }

        foreach (var duplicate in set.Rules.GroupBy(r => r.Id).Where(g => g.Count() > 1))
        {
            Error($"pravidlo „{duplicate.Key}“ je v sadě víckrát.");
        }

        foreach (var rule in set.Rules)
        {
            ValidateRule(set, rule, labels, legalRequirements, message => Error($"pravidlo „{rule.Id}“: {message}"));
        }
    }

    private static void ValidateQuestion(string id, QuestionDefinition question, Action<string> error)
    {
        if (!QuestionTypes.Contains(question.Type))
        {
            error($"otázka „{id}“ má neznámý typ „{question.Type}“ (povolené: yes_no, choice, score).");
        }

        if (string.IsNullOrWhiteSpace(question.TextEn) || string.IsNullOrWhiteSpace(question.TextCs))
        {
            error($"otázka „{id}“ musí mít text_en i text_cs.");
        }

        if (question.Type == "choice" && (question.Options is null || question.Options.Count is < 2 or > 255))
        {
            error($"otázka „{id}“ typu choice musí mít 2 až 255 možností v options.");
        }

        if (question.Type == "score" && (question.Levels is null || question.Levels.Count is < 2 or > 10))
        {
            error($"otázka „{id}“ typu score musí mít 2 až 10 úrovní v levels.");
        }
    }

    private static void ValidateRule(RuleSet set, RuleDefinition rule, LabelConfiguration labels, LegalRequirementList legalRequirements, Action<string> error)
    {
        if (string.IsNullOrWhiteSpace(rule.Id))
        {
            error("chybí id.");
        }

        if (string.IsNullOrWhiteSpace(rule.Title))
        {
            error("chybí title.");
        }

        if (string.IsNullOrWhiteSpace(rule.Explanation) || string.IsNullOrWhiteSpace(rule.Recommendation))
        {
            error("musí mít explanation i recommendation.");
        }

        foreach (var (country, text) in rule.ExplanationByJurisdiction ?? [])
        {
            if (!Countries.Contains(country) || string.IsNullOrWhiteSpace(text))
            {
                error($"explanation_by_jurisdiction smí mít jen neprázdné texty pro cz nebo sk, je „{country}“.");
            }
        }

        if (!Severities.Contains(rule.Severity))
        {
            error($"severity musí být high, medium nebo low, je „{rule.Severity}“.");
        }

        if (!Checkabilities.Contains(rule.Checkability))
        {
            error($"checkability musí být text, assess, verify nebo not_checkable, je „{rule.Checkability}“.");
        }

        if (rule.Bands.Review <= 0 || rule.Bands.Review > rule.Bands.High || rule.Bands.High > 1)
        {
            error("bands musí splňovat 0 < review ≤ high ≤ 1.");
        }

        foreach (var reference in rule.LegalRefs)
        {
            if (!RefJurisdictions.Contains(reference.Jurisdiction) || string.IsNullOrWhiteSpace(reference.Ref) || string.IsNullOrWhiteSpace(reference.Status))
            {
                error("každý odkaz v legal_refs musí mít jurisdiction (eu, cz, sk), ref a status.");
            }
        }

        switch (rule.Scope)
        {
            case SegmentScope:
                ValidateSegmentRule(set, rule, error);
                break;
            case SitePresenceScope:
                ValidateSitePresenceRule(set, rule, error);
                break;
            case SiteSignalScope:
                ValidateSiteSignalRule(rule, error);
                break;
            default:
                error($"scope musí být {SegmentScope}, {SitePresenceScope} nebo {SiteSignalScope}, je „{rule.Scope}“.");
                break;
        }

        foreach (var check in rule.CodeChecks)
        {
            ValidateCodeCheck(rule, check, labels, legalRequirements, error);
        }
    }

    private static void ValidateSegmentRule(RuleSet set, RuleDefinition rule, Action<string> error)
    {
        if (rule.Question is not null)
        {
            error("pole question patří jen k scope site_presence; u segment použijte logic.");
        }

        if (rule.Logic is null || rule.Logic.All.Count + rule.Logic.Any.Count == 0)
        {
            error("scope segment potřebuje logic s aspoň jednou podmínkou v all nebo any.");
            return;
        }

        foreach (var condition in rule.Logic.All.Concat(rule.Logic.Any).Concat(rule.Logic.None))
        {
            if (!set.Questions.TryGetValue(condition.Q, out var question))
            {
                error($"podmínka odkazuje na neznámou otázku „{condition.Q}“.");
            }
            else if (question.Type != "yes_no")
            {
                error($"podmínka na otázku „{condition.Q}“ vyžaduje typ yes_no.");
            }

            if (condition.Gte is < 0 or > 1)
            {
                error($"práh gte u otázky „{condition.Q}“ musí být mezi 0 a 1.");
            }
        }
    }

    private static void ValidateSitePresenceRule(RuleSet set, RuleDefinition rule, Action<string> error)
    {
        if (set.AppliesTo != LegalParagraph)
        {
            error("scope site_presence lze použít jen v sadě s applies_to: legal_paragraph.");
        }

        if (rule.Logic is not null)
        {
            error("scope site_presence nepoužívá logic, jen question.");
        }

        if (rule.Question is null || !set.Questions.TryGetValue(rule.Question, out var question))
        {
            error($"question „{rule.Question}“ není mezi otázkami sady.");
        }
        else if (question.Type != "yes_no")
        {
            error($"question „{rule.Question}“ musí být typu yes_no.");
        }
    }

    private static void ValidateSiteSignalRule(RuleDefinition rule, Action<string> error)
    {
        if (rule.Logic is not null || rule.Question is not null)
        {
            error("scope site_signal nepoužívá logic ani question, jen code_checks.");
        }

        var patterns = rule.CodeChecks.Count(c => c.Type is SitePatternRequired or SitePatternForbidden);
        if (patterns == 0 || patterns != rule.CodeChecks.Count)
        {
            error("scope site_signal potřebuje v code_checks jen kontroly site_pattern_required nebo site_pattern_forbidden, aspoň jednu.");
        }

        if (rule.CodeChecks.Select(c => c.Type).Distinct().Count() > 1)
        {
            error("pravidlo site_signal nesmí míchat site_pattern_required a site_pattern_forbidden.");
        }
    }

    private static void ValidateCodeCheck(RuleDefinition rule, CodeCheck check, LabelConfiguration labels, LegalRequirementList legalRequirements, Action<string> error)
    {
        switch (check.Type)
        {
            case LabelNotes:
                if (rule.Scope != SegmentScope)
                {
                    error("label_notes lze použít jen u scope segment.");
                }

                break;
            case ClaimListMatch:
                if (rule.Scope != SegmentScope)
                {
                    error("claim_list_match lze použít jen u scope segment.");
                }

                if (check.List != LegalRequirementList.ListName)
                {
                    error($"claim_list_match potřebuje list: {LegalRequirementList.ListName}, je „{check.List}“.");
                }
                else if (legalRequirements.IsEmpty)
                {
                    error("claim_list_match: seznam zákonných požadavků je prázdný nebo chybí jeho soubor (rules.legal_requirements_file).");
                }

                if (check.Outcomes is { Count: > 0 } outcomes && outcomes.FirstOrDefault(o => !LegalRequirementList.Outcomes.Contains(o)) is { } unknown)
                {
                    error($"claim_list_match: výsledek „{unknown}“ neexistuje, povolené jsou text, verify, none a irrelevant.");
                }

                if (check.Outcomes is not { Count: > 0 } && !check.Absent)
                {
                    error("claim_list_match potřebuje outcomes, absent: true, nebo obojí.");
                }

                break;
            case AllowlistAbsent:
                if (rule.Scope != SegmentScope)
                {
                    error("allowlist_absent lze použít jen u scope segment.");
                }

                if (check.List is null || !labels.Lists.ContainsKey(check.List))
                {
                    error($"seznam „{check.List}“ není v souboru se značkami.");
                }

                if (check.Where is not (null or "segment" or "page"))
                {
                    error($"where musí být segment nebo page, je „{check.Where}“.");
                }

                break;
            case RegexRequired:
                if (rule.Scope != SitePresenceScope)
                {
                    error("regex_required lze použít jen u scope site_presence.");
                }

                try
                {
                    _ = new Regex(check.Pattern ?? throw new ArgumentException("chybí pattern"), RegexOptions.None, TimeSpan.FromSeconds(1));
                }
                catch (ArgumentException ex)
                {
                    error($"pattern „{check.Pattern}“ není platný regulární výraz ({ex.Message}).");
                }

                break;
            case SitePatternRequired:
            case SitePatternForbidden:
                if (rule.Scope != SiteSignalScope)
                {
                    error($"{check.Type} lze použít jen u scope site_signal.");
                }

                if (check.Where is not null && !SignalSources.Contains(check.Where))
                {
                    error($"where u {check.Type} musí být all, text, links nebo images, je „{check.Where}“.");
                }

                try
                {
                    _ = new Regex(check.Pattern ?? throw new ArgumentException("chybí pattern"), RegexOptions.None, TimeSpan.FromSeconds(1));
                }
                catch (ArgumentException ex)
                {
                    error($"pattern „{check.Pattern}“ není platný regulární výraz ({ex.Message}).");
                }

                break;
            default:
                error($"neznámý typ kontroly „{check.Type}“ (povolené: allowlist_absent, regex_required, site_pattern_required, site_pattern_forbidden, claim_list_match, label_notes).");
                break;
        }
    }
}
