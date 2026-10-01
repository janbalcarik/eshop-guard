using System.Globalization;
using System.Text.RegularExpressions;
using EshopGuard.Core.Rules;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace EshopGuard.Cli.Commands;

/// <summary>
/// Validation shared by commands that run rule modules. The options are checked for their form first; jurisdictions,
/// modules and the language of texts are then checked against the loaded rules (<see cref="LoadRulesAsync"/>), so a new
/// market needs no change here.
/// </summary>
internal static partial class SettingsValidation
{
    public static IReadOnlyList<string> ParseModules(string modules) => ParseList(modules);

    /// <summary>The jurisdictions of the run: <c>--jurisdictions</c>, or <c>--country</c> alone.</summary>
    public static IReadOnlyList<string> ParseJurisdictions(string country, string? jurisdictions) =>
        string.IsNullOrWhiteSpace(jurisdictions) ? [country.Trim().ToLowerInvariant()] : ParseList(jurisdictions).Distinct().ToList();

    /// <summary>The date of <c>--as-of</c>, or null.</summary>
    public static DateOnly? ParseAsOf(string? asOf) =>
        string.IsNullOrWhiteSpace(asOf) ? null : DateOnly.ParseExact(asOf, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Values of settings.yaml that must be positive, and keys that may not be set there; null when all is fine.</summary>
    public static string? ValidateSettings(SettingsFile settings)
    {
        var crawl = settings.Crawl;
        foreach (var (key, value) in new[]
        {
            ("crawl.extract_timeout_seconds", crawl.ExtractTimeoutSeconds),
            ("crawl.fetch_batch_max_pages", crawl.FetchBatchMaxPages),
            ("crawl.fetch_batch_max_seconds", crawl.FetchBatchMaxSeconds),
        })
        {
            if (value <= 0)
            {
                return $"{key} musí být kladné číslo (je {value}).";
            }
        }

        return crawl.AllowPrivateNetwork
            ? "crawl.allow_private_network nejde nastavit v settings.yaml; vnitřní síť povolí jen volba --allow-private-network u místního testovacího e-shopu."
            : null;
    }

    /// <summary>The form of the options; whether the jurisdictions, modules and language exist is checked against the rules later.</summary>
    public static ValidationResult Validate(string modules, string country, string questionLanguage, string? jurisdictions = null, string lang = "cs", string? asOf = null)
    {
        foreach (var code in ParseJurisdictions(country, jurisdictions).Append(lang.Trim()))
        {
            if (!Code().IsMatch(code))
            {
                return ValidationResult.Error($"„{code}“ není kód jurisdikce ani jazyka (malá písmena, např. sk, cz, cs).");
            }
        }

        if (questionLanguage is not ("en" or "cs"))
        {
            return ValidationResult.Error("--question-lang musí být en nebo cs.");
        }

        if (ParseList(modules).FirstOrDefault(m => !Code().IsMatch(m)) is { } module)
        {
            return ValidationResult.Error($"„{module}“ není název modulu.");
        }

        if (!string.IsNullOrWhiteSpace(asOf) && !DateOnly.TryParseExact(asOf, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return ValidationResult.Error("--as-of čeká datum ve tvaru rrrr-mm-dd, např. 2027-01-02.");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Loads the rules and checks the jurisdictions, modules and language of the command against them: a jurisdiction needs
    /// enabled rules, a module must exist, the texts of the tool must be complete in the language. Prints what is wrong and
    /// returns null; nothing is downloaded or paid before this.
    /// </summary>
    public static async Task<RuleCatalog?> LoadRulesAsync(IServiceProvider services, IReadOnlyList<string> jurisdictions, IReadOnlyList<string> modules, string locale, CancellationToken ct)
    {
        RuleCatalog catalog;
        try
        {
            catalog = await services.GetRequiredService<IRuleSetProvider>().LoadAsync(ct);
        }
        catch (RuleValidationException ex)
        {
            AnsiConsole.MarkupLine("[red]Pravidla nejsou platná, nic se nestahovalo:[/]");
            foreach (var error in ex.Errors)
            {
                AnsiConsole.MarkupLine($"  - {Markup.Escape(error)}");
            }

            return null;
        }

        var known = catalog.RuleSets.Where(s => s.Enabled).SelectMany(s => s.Jurisdictions).Distinct().Order(StringComparer.Ordinal).ToList();
        if (jurisdictions.Where(j => !known.Contains(j)).ToList() is { Count: > 0 } unknown)
        {
            AnsiConsole.MarkupLine(Markup.Escape($"Pro jurisdikci {string.Join(", ", unknown)} nejsou zapnutá pravidla. Známé jurisdikce: {string.Join(", ", known)} (config/jurisdictions.yaml a sady v rules/)."));
            return null;
        }

        var knownModules = catalog.RuleSets.Select(s => s.Module).Distinct().Order(StringComparer.Ordinal).ToList();
        if (modules.Where(m => !knownModules.Contains(m)).ToList() is { Count: > 0 } unknownModules)
        {
            AnsiConsole.MarkupLine(Markup.Escape($"Neznámé moduly: {string.Join(", ", unknownModules)}. Povolené jsou {string.Join(", ", knownModules)}."));
            return null;
        }

        if (!catalog.Texts.ToolLocales.Contains(locale))
        {
            AnsiConsole.MarkupLine(Markup.Escape($"Texty pravidel pro jazyk {locale} nejsou úplné nebo zkontrolované (eshopguard rules check-texts). Zprávu jde napsat v: {string.Join(", ", catalog.Texts.ToolLocales)}."));
            return null;
        }

        return catalog;
    }

    private static List<string> ParseList(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(m => m.ToLowerInvariant())
            .ToList();

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex Code();
}
