using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EshopGuard.Core.Models;

namespace EshopGuard.Core.Rules.Texts;

/// <summary>A legal reference as shown: the reference in the language of the law and its status in the language of the reader.</summary>
/// <param name="Jurisdiction"><c>eu</c> or a jurisdiction code.</param>
/// <param name="Ref">The reference.</param>
/// <param name="Status">Status in the language of the reader, e.g. „ověřit“.</param>
/// <param name="StatusCode">Status code, e.g. <c>to_verify</c>.</param>
public sealed record RenderedLegalRef(string Jurisdiction, string Ref, string Status, string StatusCode);

/// <summary>A question for the user: its code and wording.</summary>
public sealed record RenderedUserQuestion(string Code, string Text);

/// <summary>
/// Texts of one verdict of a finding in the language of the reader.
/// </summary>
public sealed class RenderedFinding
{
    /// <summary>Language of the texts of the rule actually used: the requested one, or the language the rule set was written in when its translation is missing or not reviewed.</summary>
    public required string Locale { get; init; }

    /// <summary>Title.</summary>
    public required string Title { get; init; }

    /// <summary>Explanation for the jurisdiction of the verdict.</summary>
    public required string Explanation { get; init; }

    /// <summary>Recommendation.</summary>
    public required string Recommendation { get; init; }

    /// <summary>Notes of the tool.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>Legal references of the verdict.</summary>
    public IReadOnlyList<RenderedLegalRef> LegalRefs { get; init; } = [];

    /// <summary>Questions for the user.</summary>
    public IReadOnlyList<RenderedUserQuestion> UserQuestions { get; init; } = [];
}

/// <summary>
/// Composes the texts of findings, notes and warnings in a language from the codes and parameters the library returns: the
/// text of the rule set of the verdict (the explanation of the jurisdiction of the verdict when the rule has one), numbers and
/// dates in the culture of the language, legal references in the language of the law. A missing text is an error, never an
/// empty string.
/// </summary>
public sealed partial class RuleTextRenderer
{
    private readonly RuleCatalog _catalog;

    /// <summary>Renderer over the texts of <paramref name="catalog"/>.</summary>
    public RuleTextRenderer(RuleCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    private RuleTexts Texts => _catalog.Texts;

    /// <summary>Texts of the strictest verdict of <paramref name="finding"/>.</summary>
    public RenderedFinding Render(Finding finding, string locale)
    {
        ArgumentNullException.ThrowIfNull(finding);
        return Render(finding, finding.Strictest, locale);
    }

    /// <summary>Texts of <paramref name="verdict"/> of <paramref name="finding"/>.</summary>
    /// <exception cref="KeyNotFoundException">The rule has no text.</exception>
    public RenderedFinding Render(Finding finding, JurisdictionVerdict verdict, string locale)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(verdict);
        var (text, textLocale) = RuleTextOf(verdict.RuleSet, finding.RuleId, verdict.Jurisdiction, locale);
        var lawLanguage = _catalog.Jurisdictions.LawLanguage(verdict.Jurisdiction);
        return new RenderedFinding
        {
            Locale = textLocale,
            Title = Fill(text.Title, finding.Params, textLocale),
            Explanation = Fill(text.ExplanationFor(verdict.ExplanationVariant), finding.Params, textLocale),
            Recommendation = Fill(text.Recommendation, finding.Params, textLocale),
            Notes = verdict.Notes.Select(n => Note(n, locale)).ToList(),
            LegalRefs = verdict.LegalRefs.Select(r => new RenderedLegalRef(r.Jurisdiction, r.RefFor(lawLanguage), RefStatus(r.Status, locale), r.Status)).ToList(),
            UserQuestions = (text.UserQuestions ?? []).Select(q => new RenderedUserQuestion(q.Key, q.Value)).ToList(),
        };
    }

    /// <summary>Title of a rule of a rule set in a language.</summary>
    public string Title(string ruleSet, string ruleId, string jurisdiction, string locale) =>
        RuleTextOf(ruleSet, ruleId, jurisdiction, locale).Text.Title;

    /// <summary>A note in a language.</summary>
    public string Note(FindingNote note, string locale)
    {
        ArgumentNullException.ThrowIfNull(note);
        return Code(note.Code, note.Params, locale);
    }

    /// <summary>A warning in a language.</summary>
    public string Warning(ScanWarning warning, string locale)
    {
        ArgumentNullException.ThrowIfNull(warning);
        return Code(warning.Code, warning.Params, locale);
    }

    /// <summary>The text of a code of the tool with its parameters.</summary>
    /// <exception cref="KeyNotFoundException">The code has no text.</exception>
    public string Code(string code, IReadOnlyDictionary<string, object?>? parameters, string locale)
    {
        var (engine, engineLocale) = Engine(locale);
        if (!engine.Codes.TryGetValue(code, out var template) || string.IsNullOrWhiteSpace(template))
        {
            throw new KeyNotFoundException($"Kód „{code}“ nemá text v rules/texts/{engineLocale}/_engine.yaml.");
        }

        if (code == EngineCodes.LabelNote)
        {
            parameters = WithLabelText(parameters, locale);
        }

        return Fill(template, parameters ?? new Dictionary<string, object?>(), engineLocale);
    }

    /// <summary>Status of a legal reference (<c>to_verify</c>, <c>to_complete</c>) in a language.</summary>
    public string RefStatus(string code, string locale) => Code(code, null, locale);

    /// <summary>Name of a jurisdiction in a language, or its code in capitals when the texts have no name.</summary>
    public string JurisdictionName(string jurisdiction, string locale) =>
        Engine(locale).File.Jurisdictions.TryGetValue(jurisdiction, out var name) && !string.IsNullOrWhiteSpace(name) ? name : jurisdiction.ToUpperInvariant();

    private (RuleText Text, string Locale) RuleTextOf(string ruleSet, string ruleId, string jurisdiction, string locale)
    {
        if (ruleSet == BuiltInRuleSet)
        {
            var (engine, engineLocale) = Engine(locale);
            return engine.Rules.TryGetValue(ruleId, out var builtIn)
                ? (builtIn, engineLocale)
                : throw new KeyNotFoundException($"Vestavěné pravidlo „{ruleId}“ nemá text v rules/texts/{engineLocale}/_engine.yaml.");
        }

        // Findings read from an older findings.json do not know their rule set: the set of the rule for the jurisdiction.
        if (ruleSet.Length == 0)
        {
            ruleSet = _catalog.RuleSets.FirstOrDefault(s => s.Jurisdictions.Contains(jurisdiction) && s.Rules.Any(r => r.Id == ruleId))?.Name ?? "";
        }

        if (Texts.SetTexts(ruleSet, locale) is { } texts && texts.File.Rules.TryGetValue(ruleId, out var text))
        {
            return (text, texts.Locale);
        }

        // A disabled set may still keep its texts in the rule file.
        var rule = _catalog.RuleSets.FirstOrDefault(s => s.Name == ruleSet)?.Rules.FirstOrDefault(r => r.Id == ruleId);
        if (rule is { HasInlineTexts: true })
        {
            return (new RuleText
            {
                Title = rule.Title ?? "",
                Explanation = rule.Explanation ?? "",
                ExplanationByJurisdiction = rule.ExplanationByJurisdiction,
                Recommendation = rule.Recommendation ?? "",
            }, Texts.OriginalLocales.GetValueOrDefault(ruleSet, locale));
        }

        throw new KeyNotFoundException($"Pravidlo „{ruleId}“ sady „{ruleSet}“ nemá texty (rules/texts/<jazyk>/{ruleSet}.yaml).");
    }

    /// <summary>Texts of the tool in the language, or in the first language whose texts of the tool are complete.</summary>
    private (EngineTextFile File, string Locale) Engine(string locale)
    {
        foreach (var candidate in Texts.ToolLocales.Contains(locale) ? [locale] : Texts.ToolLocales)
        {
            if (Texts.Locales.TryGetValue(candidate, out var texts) && texts.Engine is { } engine)
            {
                return (engine, candidate);
            }
        }

        throw new KeyNotFoundException($"Texty nástroje (_engine.yaml) nejsou úplné v žádném jazyce, ani v {locale}.");
    }

    private IReadOnlyDictionary<string, object?> WithLabelText(IReadOnlyDictionary<string, object?>? parameters, string locale)
    {
        var id = parameters is not null && parameters.TryGetValue("label_id", out var value) ? AsString(value) : null;
        string? text = null;
        foreach (var candidate in new[] { locale }.Concat(Texts.ToolLocales))
        {
            if (Texts.ToolLocales.Contains(candidate) && Texts.Locales.TryGetValue(candidate, out var texts)
                && texts.Labels is { } labels && id is not null && labels.Notes.TryGetValue(id, out var note))
            {
                text = note;
                break;
            }
        }

        if (text is null)
        {
            throw new KeyNotFoundException($"Poznámka ke značce „{id}“ nemá text v rules/texts/{locale}/_labels.yaml.");
        }

        var result = (parameters ?? new Dictionary<string, object?>()).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        result["label_text"] = text;
        return result;
    }

    /// <summary>Puts the parameters into the placeholders; a placeholder without a parameter is an error.</summary>
    private string Fill(string template, IReadOnlyDictionary<string, object?> parameters, string locale)
    {
        var culture = Texts.Locales.TryGetValue(locale, out var texts) ? texts.Culture : LocaleTexts.CultureOf(null, locale);
        return Placeholder().Replace(template, match =>
        {
            var name = match.Groups["name"].Value;
            if (!parameters.TryGetValue(name, out var value))
            {
                throw new KeyNotFoundException($"Text „{template}“ potřebuje parametr {{{name}}}, který chybí.");
            }

            return Format(value, match.Groups["format"].Success ? match.Groups["format"].Value : null, culture, locale);
        });
    }

    private string Format(object? value, string? format, CultureInfo culture, string locale)
    {
        switch (value)
        {
            case null:
                return "";
            case JsonElement element:
                return FormatJson(element, format, culture, locale);
            case string text:
                return text;
            case FindingNote note:
                return Note(note, locale);
            case DateOnly date:
                return date.ToString(format ?? "d", culture);
            case IFormattable formattable:
                return formattable.ToString(format, culture);
            case System.Collections.IEnumerable items:
                return string.Join(", ", items.Cast<object?>().Select(i => Format(i, format, culture, locale)));
            default:
                return value.ToString() ?? "";
        }
    }

    private string FormatJson(JsonElement element, string? format, CultureInfo culture, string locale)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return "";
            case JsonValueKind.Number:
                return element.TryGetInt64(out var whole) && format is null
                    ? whole.ToString(culture)
                    : element.GetDouble().ToString(format, culture);
            case JsonValueKind.String:
                var text = element.GetString() ?? "";
                return format is not null && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    ? date.ToString(format, culture)
                    : text;
            case JsonValueKind.Array:
                return string.Join(", ", element.EnumerateArray().Select(e => FormatJson(e, format, culture, locale)));
            case JsonValueKind.Object when element.TryGetProperty("code", out var code):
                var nested = element.TryGetProperty("params", out var p) && p.ValueKind == JsonValueKind.Object
                    ? p.EnumerateObject().ToDictionary(x => x.Name, x => (object?)x.Value.Clone(), StringComparer.Ordinal)
                    : new Dictionary<string, object?>();
                return Code(code.GetString() ?? "", nested, locale);
            default:
                return element.GetRawText();
        }
    }

    private static string? AsString(object? value) => value switch
    {
        string text => text,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
        _ => value?.ToString(),
    };

    /// <summary>Name of the rule set of built-in rules in verdicts; their texts are in <c>_engine.yaml</c>.</summary>
    public const string BuiltInRuleSet = "_engine";

    [GeneratedRegex(@"\{(?<name>[a-z_][a-z0-9_]*)(?::(?<format>[^{}]*))?\}")]
    private static partial Regex Placeholder();
}
