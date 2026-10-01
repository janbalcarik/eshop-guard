using Spectre.Console;

namespace EshopGuard.Cli.Commands;

/// <summary>
/// Validation shared by commands that run rule modules.
/// </summary>
internal static class SettingsValidation
{
    private static readonly string[] KnownModules = ["eco", "dur", "lr", "ucp", "legal"];

    public static IReadOnlyList<string> ParseModules(string modules) =>
        modules.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(m => m.ToLowerInvariant())
            .ToList();

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

    public static ValidationResult Validate(string modules, string country, string questionLanguage)
    {
        if (country is not ("cz" or "sk"))
        {
            return ValidationResult.Error("--country musí být cz nebo sk.");
        }

        if (questionLanguage is not ("en" or "cs"))
        {
            return ValidationResult.Error("--question-lang musí být en nebo cs.");
        }

        var list = ParseModules(modules);
        var unknown = list.Except(KnownModules).ToList();
        if (unknown.Count > 0)
        {
            return ValidationResult.Error($"Neznámé moduly: {string.Join(", ", unknown)}. Povolené jsou eco, dur, lr, ucp a legal.");
        }

        return ValidationResult.Success();
    }
}
