using System.Globalization;
using EshopGuard.Core.Options;
using EshopGuard.Core.Rules.Texts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.Converters;
using YamlDotNet.Serialization.NamingConventions;

namespace EshopGuard.Core.Rules;

/// <summary>
/// Reads every <c>*.yaml</c> file in the rules directory (not its subfolders, so <c>rules/texts</c> is never a rule set), the
/// known jurisdictions, the labels file, the list of legal requirements and the texts, then validates them. Unknown fields are
/// errors, so a typo in YAML never silently disables a check.
/// </summary>
internal sealed class YamlRuleSetProvider(
    IOptions<EshopGuardOptions> options,
    ILogger<YamlRuleSetProvider> logger,
    IRuleTextProvider? textProvider = null) : IRuleSetProvider
{
    private const string ImageKeywordsKey = "eco_image_keywords";

    private const string LabelNotesKey = "label_notes";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithTypeConverter(new DateOnlyConverter(CultureInfo.InvariantCulture, false, "yyyy-MM-dd"))
        .Build();

    private IRuleTextProvider Texts => textProvider ?? new YamlRuleTextProvider(options);

    public async Task<RuleCatalog> LoadAsync(CancellationToken ct = default)
    {
        var settings = options.Value.Rules;
        var errors = new List<string>();

        var jurisdictions = await LoadJurisdictionsAsync(settings.JurisdictionsFile, errors, ct);
        var labels = await LoadLabelsAsync(settings.LabelsFile, errors, ct);
        var legalRequirements = await LoadLegalRequirementsAsync(settings.LegalRequirementsFile, errors, ct);
        var sieve = await LoadSieveAsync(settings.SieveFile, errors, ct);
        var ruleSets = new List<RuleSet>();
        if (!Directory.Exists(settings.Directory))
        {
            errors.Add($"Složka s pravidly „{Path.GetFullPath(settings.Directory)}“ neexistuje.");
        }
        else
        {
            foreach (var file in Directory.GetFiles(settings.Directory, "*.yaml").Order(StringComparer.Ordinal))
            {
                var ruleSet = await LoadRuleSetAsync(file, errors, ct);
                if (ruleSet is not null)
                {
                    ruleSets.Add(ruleSet);
                }
            }

            if (ruleSets.Count == 0 && errors.Count == 0)
            {
                errors.Add($"Ve složce „{Path.GetFullPath(settings.Directory)}“ není žádný soubor *.yaml s pravidly.");
            }
        }

        errors.AddRange(RuleValidator.Validate(ruleSets, labels, legalRequirements, jurisdictions));
        if (sieve is not null)
        {
            errors.AddRange(RuleValidator.ValidateSieve(sieve, ruleSets, Path.GetFileName(settings.SieveFile)));
        }

        errors.AddRange(await QuestionSetHashes.CheckAsync(ruleSets, settings.ResolvedQuestionSetHashesFile, ct));
        var texts = RuleTextValidator.Validate(await Texts.LoadAsync(errors, ct), ruleSets, labels, jurisdictions, settings.RequiredLocales, errors);
        if (errors.Count > 0)
        {
            throw new RuleValidationException(errors);
        }

        foreach (var set in ruleSets)
        {
            logger.LogInformation("Loaded rule set {Module} {Version} from {File}: {Questions} questions, {Rules} rules, enabled {Enabled}",
                set.Module, set.Version, set.SourceFile, set.Questions.Count, set.Rules.Count, set.Enabled);
        }

        foreach (var (locale, problems) in texts.Problems.Where(p => p.Value.Count > 0))
        {
            logger.LogInformation("Texts in {Locale}: {Count} problems, translations with problems are not used (eshopguard rules check-texts)", locale, problems.Count);
        }

        return new RuleCatalog
        {
            RuleSets = ruleSets, Labels = labels, LegalRequirements = legalRequirements, Sieve = sieve, Jurisdictions = jurisdictions, Texts = texts,
        };
    }

    /// <summary>The known jurisdictions; without the file no rule set can name a jurisdiction.</summary>
    private static async Task<JurisdictionRegistry> LoadJurisdictionsAsync(string file, List<string> errors, CancellationToken ct)
    {
        if (!File.Exists(file))
        {
            errors.Add($"Soubor se seznamem jurisdikcí „{Path.GetFullPath(file)}“ neexistuje.");
            return JurisdictionRegistry.Empty;
        }

        var name = Path.GetFileName(file);
        try
        {
            var raw = Deserializer.Deserialize<Dictionary<string, JurisdictionInfo>?>(await File.ReadAllTextAsync(file, ct)) ?? [];
            foreach (var (code, info) in raw)
            {
                if (code.Length == 0 || code != code.ToLowerInvariant() || code == "eu")
                {
                    errors.Add($"{name}: kód jurisdikce „{code}“ musí být malými písmeny a nesmí být eu.");
                }

                if (info is null || string.IsNullOrWhiteSpace(info.LawLanguage))
                {
                    errors.Add($"{name}: jurisdikce „{code}“ potřebuje law_language.");
                }
                else
                {
                    errors.AddRange(Markets.MarketCatalog.Validate(code, info).Select(e => $"{name}: {e}"));
                }
            }

            return new JurisdictionRegistry(raw.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
        }
        catch (YamlException ex)
        {
            errors.Add($"{name}, řádek {ex.Start.Line}: {Describe(ex)}");
            return JurisdictionRegistry.Empty;
        }
    }

    /// <summary>A missing file turns the sieve off.</summary>
    private static async Task<SieveDefinition?> LoadSieveAsync(string file, List<string> errors, CancellationToken ct)
    {
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            return Deserializer.Deserialize<SieveDefinition?>(await File.ReadAllTextAsync(file, ct));
        }
        catch (YamlException ex)
        {
            errors.Add($"{Path.GetFileName(file)}, řádek {ex.Start.Line}: {Describe(ex)}");
            return null;
        }
    }

    /// <summary>A missing file is not an error by itself: only rules with <c>claim_list_match</c> need the list.</summary>
    private static async Task<LegalRequirementList> LoadLegalRequirementsAsync(string file, List<string> errors, CancellationToken ct)
    {
        if (!File.Exists(file))
        {
            return LegalRequirementList.Empty;
        }

        var name = Path.GetFileName(file);
        try
        {
            var raw = Deserializer.Deserialize<LegalRequirementFile?>(await File.ReadAllTextAsync(file, ct)) ?? new LegalRequirementFile();
            return LegalRequirementList.Compile(raw, name, errors);
        }
        catch (YamlException ex)
        {
            errors.Add($"{name}, řádek {ex.Start.Line}: {Describe(ex)}");
            return LegalRequirementList.Empty;
        }
    }

    internal static async Task<RuleSet?> LoadRuleSetAsync(string file, List<string> errors, CancellationToken ct)
    {
        var name = Path.GetFileName(file);
        try
        {
            var ruleSet = Deserializer.Deserialize<RuleSet?>(await File.ReadAllTextAsync(file, ct));
            if (ruleSet is null)
            {
                errors.Add($"{name}: soubor je prázdný.");
                return null;
            }

            ruleSet.SourceFile = name;
            return ruleSet;
        }
        catch (YamlException ex)
        {
            errors.Add($"{name}, řádek {ex.Start.Line}: {Describe(ex)}");
            return null;
        }
    }

    private static async Task<LabelConfiguration> LoadLabelsAsync(string file, List<string> errors, CancellationToken ct)
    {
        if (!File.Exists(file))
        {
            errors.Add($"Soubor se seznamy značek „{Path.GetFullPath(file)}“ neexistuje.");
            return new LabelConfiguration();
        }

        var name = Path.GetFileName(file);
        try
        {
            var raw = Deserializer.Deserialize<Dictionary<string, object>?>(await File.ReadAllTextAsync(file, ct)) ?? [];
            var lists = new Dictionary<string, IReadOnlyList<string>>();
            var notes = new List<LabelNote>();
            foreach (var (key, value) in raw)
            {
                if (key == LabelNotesKey)
                {
                    notes = ReadLabelNotes(value, name, errors);
                }
                else if (Strings(value) is { } items)
                {
                    lists[key] = items.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                }
                else
                {
                    errors.Add($"{name}: „{key}“ musí být seznam textů.");
                }
            }

            return new LabelConfiguration
            {
                Lists = lists.Where(p => p.Key != ImageKeywordsKey).ToDictionary(p => p.Key, p => p.Value),
                EcoImageKeywords = lists.TryGetValue(ImageKeywordsKey, out var keywords) ? keywords : [],
                Notes = notes,
            };
        }
        catch (YamlException ex)
        {
            errors.Add($"{name}, řádek {ex.Start.Line}: {Describe(ex)}");
            return new LabelConfiguration();
        }
    }

    /// <summary>Each remark is a mapping with <c>id</c> (text id in <c>_labels.yaml</c>) and <c>names</c> (list of texts).</summary>
    private static List<LabelNote> ReadLabelNotes(object? value, string fileName, List<string> errors)
    {
        var notes = new List<LabelNote>();
        if (value is not List<object> items)
        {
            errors.Add($"{fileName}: {LabelNotesKey} musí být seznam poznámek s poli id a names.");
            return notes;
        }

        foreach (var item in items)
        {
            if (item is Dictionary<object, object> map
                && map.Keys.All(k => k is "id" or "names")
                && map.TryGetValue("id", out var id) && id is string { Length: > 0 } noteId
                && map.TryGetValue("names", out var names) && Strings(names) is { Count: > 0 } nameList)
            {
                notes.Add(new LabelNote { Id = noteId, Names = nameList });
            }
            else
            {
                errors.Add($"{fileName}: každá položka {LabelNotesKey} potřebuje jen pole id (text poznámky v rules/texts/<jazyk>/_labels.yaml) a names (seznam názvů).");
            }
        }

        foreach (var duplicate in notes.GroupBy(n => n.Id).Where(g => g.Count() > 1))
        {
            errors.Add($"{fileName}: id poznámky „{duplicate.Key}“ je v {LabelNotesKey} víckrát.");
        }

        return notes;
    }

    private static List<string>? Strings(object? value) =>
        value is List<object> items && items.All(i => i is string) ? items.Cast<string>().ToList() : null;

    private static string Describe(YamlException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("not found on type", StringComparison.Ordinal)
            ? $"neznámé pole ({message})"
            : message;
    }
}
