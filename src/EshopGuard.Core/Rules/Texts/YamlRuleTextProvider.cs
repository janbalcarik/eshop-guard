using EshopGuard.Core.Options;
using Microsoft.Extensions.Options;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EshopGuard.Core.Rules.Texts;

/// <summary>
/// Loads the texts of the rules, the tool and the labels from <c>rules/texts/&lt;locale&gt;/</c>.
/// </summary>
public interface IRuleTextProvider
{
    /// <summary>
    /// Reads every language folder (or those of <c>rules.locales</c>). Errors of the files (invalid YAML, unknown fields,
    /// a language that does not match its folder) are added to <paramref name="errors"/>; completeness is checked later
    /// against the rule sets.
    /// </summary>
    Task<RuleTexts> LoadAsync(List<string> errors, CancellationToken ct = default);
}

/// <summary>
/// Default <see cref="IRuleTextProvider"/>: one folder per language, <c>_engine.yaml</c>, <c>_labels.yaml</c> and one file per
/// rule set. Unknown fields are errors, so a typo never hides a text.
/// </summary>
internal sealed class YamlRuleTextProvider(IOptions<EshopGuardOptions> options) : IRuleTextProvider
{
    /// <summary>File with the texts of the tool.</summary>
    public const string EngineFile = "_engine.yaml";

    /// <summary>File with the remarks on labels.</summary>
    public const string LabelsFile = "_labels.yaml";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    public async Task<RuleTexts> LoadAsync(List<string> errors, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var settings = options.Value.Rules;
        var root = settings.ResolvedTextsDirectory;
        if (!Directory.Exists(root))
        {
            errors.Add($"Složka s texty pravidel „{Path.GetFullPath(root)}“ neexistuje.");
            return RuleTexts.Empty;
        }

        var folders = Directory.GetDirectories(root).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal).ToList();
        if (settings.Locales.Count > 0)
        {
            foreach (var missing in settings.Locales.Where(l => !folders.Contains(l)))
            {
                errors.Add($"Texty pravidel pro jazyk {missing} (rules.locales) chybí: složka „{Path.Combine(root, missing)}“ neexistuje.");
            }

            folders = folders.Where(settings.Locales.Contains).ToList();
        }

        var locales = new Dictionary<string, LocaleTexts>(StringComparer.Ordinal);
        foreach (var locale in folders)
        {
            locales[locale] = await LoadLocaleAsync(root, locale, errors, ct);
        }

        return new RuleTexts { Locales = locales };
    }

    private static async Task<LocaleTexts> LoadLocaleAsync(string root, string locale, List<string> errors, CancellationToken ct)
    {
        EngineTextFile? engine = null;
        LabelTextFile? labels = null;
        var sets = new Dictionary<string, RuleSetTextFile>(StringComparer.Ordinal);
        foreach (var path in Directory.GetFiles(Path.Combine(root, locale), "*.yaml").Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(path);
            var display = $"{locale}/{name}";
            var yaml = await File.ReadAllTextAsync(path, ct);
            if (name == EngineFile)
            {
                engine = Read<EngineTextFile>(yaml, display, errors);
                if (engine is not null)
                {
                    engine.File = display;
                    CheckLocale(engine.Locale, locale, display, errors);
                }
            }
            else if (name == LabelsFile)
            {
                labels = Read<LabelTextFile>(yaml, display, errors);
                if (labels is not null)
                {
                    labels.File = display;
                    CheckLocale(labels.Locale, locale, display, errors);
                }
            }
            else if (name.StartsWith('_'))
            {
                errors.Add($"{display}: neznámý soubor textů nástroje (povolené jsou {EngineFile} a {LabelsFile}).");
            }
            else if (Read<RuleSetTextFile>(yaml, display, errors) is { } set)
            {
                set.File = display;
                CheckLocale(set.Locale, locale, display, errors);
                var expected = Path.GetFileNameWithoutExtension(name);
                if (set.RuleSet != expected)
                {
                    errors.Add($"{display}: rule_set „{set.RuleSet}“ neodpovídá názvu souboru (čekáno „{expected}“).");
                }

                sets[expected] = set;
            }
        }

        return new LocaleTexts { Locale = locale, Engine = engine, Labels = labels, Sets = sets };
    }

    private static T? Read<T>(string yaml, string display, List<string> errors)
        where T : class
    {
        try
        {
            var value = Deserializer.Deserialize<T?>(yaml);
            if (value is null)
            {
                errors.Add($"{display}: soubor je prázdný.");
            }

            return value;
        }
        catch (YamlException ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            errors.Add($"{display}, řádek {ex.Start.Line}: {(message.Contains("not found on type", StringComparison.Ordinal) ? $"neznámé pole ({message})" : message)}");
            return null;
        }
    }

    private static void CheckLocale(string declared, string folder, string display, List<string> errors)
    {
        if (declared != folder)
        {
            errors.Add($"{display}: locale „{declared}“ neodpovídá složce {folder}.");
        }
    }
}
