using EshopGuard.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EshopGuard.Core.Rules;

/// <summary>
/// Reads every <c>*.yaml</c> file in the rules directory, the labels file and the list of legal requirements, then validates them.
/// Unknown fields are errors, so a typo in YAML never silently disables a check.
/// </summary>
internal sealed class YamlRuleSetProvider(IOptions<EshopGuardOptions> options, ILogger<YamlRuleSetProvider> logger) : IRuleSetProvider
{
    private const string ImageKeywordsKey = "eco_image_keywords";

    private const string LabelNotesKey = "label_notes";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    public async Task<RuleCatalog> LoadAsync(CancellationToken ct = default)
    {
        var settings = options.Value.Rules;
        var errors = new List<string>();

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

        errors.AddRange(RuleValidator.Validate(ruleSets, labels, legalRequirements));
        if (sieve is not null)
        {
            errors.AddRange(RuleValidator.ValidateSieve(sieve, ruleSets, Path.GetFileName(settings.SieveFile)));
        }
        if (errors.Count > 0)
        {
            throw new RuleValidationException(errors);
        }

        foreach (var set in ruleSets)
        {
            logger.LogInformation("Loaded rule set {Module} {Version} from {File}: {Questions} questions, {Rules} rules, enabled {Enabled}",
                set.Module, set.Version, set.SourceFile, set.Questions.Count, set.Rules.Count, set.Enabled);
        }

        return new RuleCatalog { RuleSets = ruleSets, Labels = labels, LegalRequirements = legalRequirements, Sieve = sieve };
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

    private static async Task<RuleSet?> LoadRuleSetAsync(string file, List<string> errors, CancellationToken ct)
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

    /// <summary>Each remark is a mapping with <c>names</c> (list of texts) and <c>note</c> (text).</summary>
    private static List<LabelNote> ReadLabelNotes(object? value, string fileName, List<string> errors)
    {
        var notes = new List<LabelNote>();
        if (value is not List<object> items)
        {
            errors.Add($"{fileName}: {LabelNotesKey} musí být seznam poznámek s poli names a note.");
            return notes;
        }

        foreach (var item in items)
        {
            if (item is Dictionary<object, object> map
                && map.Keys.All(k => k is "names" or "note")
                && map.TryGetValue("names", out var names) && Strings(names) is { Count: > 0 } nameList
                && map.TryGetValue("note", out var note) && note is string { Length: > 0 } text)
            {
                notes.Add(new LabelNote { Names = nameList, Note = text });
            }
            else
            {
                errors.Add($"{fileName}: každá položka {LabelNotesKey} potřebuje jen pole names (seznam názvů) a note (text).");
            }
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
