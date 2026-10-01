using System.Globalization;
using YamlDotNet.Serialization;

namespace EshopGuard.Core.Rules.Texts;

/// <summary>
/// Who wrote and who checked the texts of one file. The original texts of a rule set (written in that language by the
/// authors of the rules) are usable as they are; a translation is usable only when a person reviewed it.
/// </summary>
public sealed class TextReview
{
    /// <summary>The texts were written in this language by the authors of the rules (the source of translations).</summary>
    public bool Original { get; set; }

    /// <summary>Who translated the texts.</summary>
    public string? TranslatedBy { get; set; }

    /// <summary>Who reviewed the translation; required for a translation to be used.</summary>
    public string? ReviewedBy { get; set; }

    /// <summary>When the translation was reviewed (<c>yyyy-mm-dd</c>); required for a translation to be used.</summary>
    public string? ReviewedAt { get; set; }

    /// <summary>The translation was drafted by a language model and waits for a person.</summary>
    public bool MachineDraft { get; set; }

    /// <summary>Free note, e.g. where the texts came from.</summary>
    public string? Note { get; set; }

    /// <summary>True when the texts may be shown: original texts, or a translation reviewed by a person.</summary>
    [YamlIgnore]
    public bool IsUsable => Original || (!MachineDraft && !string.IsNullOrWhiteSpace(ReviewedBy) && !string.IsNullOrWhiteSpace(ReviewedAt));
}

/// <summary>
/// Texts of one rule in one language.
/// </summary>
public sealed class RuleText
{
    /// <summary>Title.</summary>
    public string Title { get; set; } = "";

    /// <summary>Why this is a problem.</summary>
    public string Explanation { get; set; } = "";

    /// <summary>Explanation for a jurisdiction whose law differs, by jurisdiction code.</summary>
    public Dictionary<string, string>? ExplanationByJurisdiction { get; set; }

    /// <summary>What to do.</summary>
    public string Recommendation { get; set; } = "";

    /// <summary>Wording of the questions for the user, by code.</summary>
    public Dictionary<string, string>? UserQuestions { get; set; }

    /// <summary>Explanation of the variant: the jurisdiction's own text, or the general one.</summary>
    public string ExplanationFor(string variant) =>
        ExplanationByJurisdiction is not null && ExplanationByJurisdiction.TryGetValue(variant, out var text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : Explanation;
}

/// <summary>
/// File <c>rules/texts/&lt;locale&gt;/&lt;set&gt;.yaml</c>: texts of the rules of one rule set in one language.
/// </summary>
public sealed class RuleSetTextFile
{
    /// <summary>Language of the texts; must match the folder.</summary>
    public string Locale { get; set; } = "";

    /// <summary>Name of the rule set (file name of the rules without extension).</summary>
    public string RuleSet { get; set; } = "";

    /// <summary>Version of the rule set the texts were written or reviewed for.</summary>
    public string SourceVersion { get; set; } = "";

    /// <summary>Who wrote and who checked the texts.</summary>
    public TextReview Review { get; set; } = new();

    /// <summary>Texts by rule id.</summary>
    public Dictionary<string, RuleText> Rules { get; set; } = [];

    /// <summary>Path of the file relative to the texts folder, for messages.</summary>
    [YamlIgnore]
    public string File { get; set; } = "";
}

/// <summary>
/// File <c>rules/texts/&lt;locale&gt;/_engine.yaml</c>: texts of the tool in one language.
/// </summary>
public sealed class EngineTextFile
{
    /// <summary>Language of the texts; must match the folder.</summary>
    public string Locale { get; set; } = "";

    /// <summary>Culture for numbers and dates (e.g. <c>cs-CZ</c>); the language code when empty.</summary>
    public string? Culture { get; set; }

    /// <summary>Who wrote and who checked the texts.</summary>
    public TextReview Review { get; set; } = new();

    /// <summary>Names of the jurisdictions, by code.</summary>
    public Dictionary<string, string> Jurisdictions { get; set; } = [];

    /// <summary>Notes, warnings and reasons by code (<see cref="EngineCodes"/>), with placeholders <c>{name}</c> or <c>{name:format}</c>.</summary>
    public Dictionary<string, string> Codes { get; set; } = [];

    /// <summary>Texts of the built-in rules (<c>legal_pages_missing</c>).</summary>
    public Dictionary<string, RuleText> Rules { get; set; } = [];

    /// <summary>Path of the file relative to the texts folder, for messages.</summary>
    [YamlIgnore]
    public string File { get; set; } = "";
}

/// <summary>
/// File <c>rules/texts/&lt;locale&gt;/_labels.yaml</c>: remarks on labels in one language, by the id of the remark in the labels file.
/// </summary>
public sealed class LabelTextFile
{
    /// <summary>Language of the texts; must match the folder.</summary>
    public string Locale { get; set; } = "";

    /// <summary>Who wrote and who checked the texts.</summary>
    public TextReview Review { get; set; } = new();

    /// <summary>Remarks by id.</summary>
    public Dictionary<string, string> Notes { get; set; } = [];

    /// <summary>Path of the file relative to the texts folder, for messages.</summary>
    [YamlIgnore]
    public string File { get; set; } = "";
}

/// <summary>
/// All texts of one language folder.
/// </summary>
public sealed class LocaleTexts
{
    /// <summary>Language code (folder name), e.g. <c>cs</c>.</summary>
    public required string Locale { get; init; }

    /// <summary>Texts of the rule sets, by name of the set.</summary>
    public IReadOnlyDictionary<string, RuleSetTextFile> Sets { get; init; } = new Dictionary<string, RuleSetTextFile>();

    /// <summary>Texts of the tool, if the folder has them.</summary>
    public EngineTextFile? Engine { get; init; }

    /// <summary>Remarks on labels, if the folder has them.</summary>
    public LabelTextFile? Labels { get; init; }

    /// <summary>Culture for numbers and dates.</summary>
    public CultureInfo Culture => CultureOf(Engine?.Culture, Locale);

    internal static CultureInfo CultureOf(string? culture, string locale)
    {
        try
        {
            return CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(culture) ? locale : culture);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}

/// <summary>
/// Texts of the rules, the tool and the labels in every language, with what the check of completeness found.
/// </summary>
public sealed class RuleTexts
{
    /// <summary>No texts.</summary>
    public static RuleTexts Empty { get; } = new();

    /// <summary>Texts by language code.</summary>
    public IReadOnlyDictionary<string, LocaleTexts> Locales { get; init; } = new Dictionary<string, LocaleTexts>();

    /// <summary>Language of the original texts of every rule set, by name of the set.</summary>
    public IReadOnlyDictionary<string, string> OriginalLocales { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Languages in which every enabled rule set, the tool and the labels have complete usable texts (original or reviewed).
    /// </summary>
    public IReadOnlyList<string> CompleteLocales { get; init; } = [];

    /// <summary>Languages whose texts of the tool and the labels are complete and usable; a report can be written in them.</summary>
    public IReadOnlyList<string> ToolLocales { get; init; } = [];

    /// <summary>What is missing or not reviewed, by language: file and problem. Such texts are not used.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Problems { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Usable and complete texts of the set per language, by language (filled by the check).</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> UsableSets { get; init; } = new Dictionary<string, IReadOnlySet<string>>();

    /// <summary>
    /// Texts of a rule set to show in <paramref name="locale"/>: the reviewed translation when it is complete, otherwise the
    /// original texts; the language actually used is returned with them.
    /// </summary>
    public (RuleSetTextFile File, string Locale)? SetTexts(string ruleSet, string locale)
    {
        if (UsableSets.TryGetValue(locale, out var usable) && usable.Contains(ruleSet)
            && Locales.TryGetValue(locale, out var texts) && texts.Sets.TryGetValue(ruleSet, out var file))
        {
            return (file, locale);
        }

        if (OriginalLocales.TryGetValue(ruleSet, out var original)
            && Locales.TryGetValue(original, out var originalTexts) && originalTexts.Sets.TryGetValue(ruleSet, out var originalFile))
        {
            return (originalFile, original);
        }

        return null;
    }
}
