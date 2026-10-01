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
