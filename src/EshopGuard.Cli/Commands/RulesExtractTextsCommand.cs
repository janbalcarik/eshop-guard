using System.ComponentModel;
using EshopGuard.Core.Rules.Texts;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EshopGuard.Cli.Commands;

/// <summary>Settings of <c>eshopguard rules extract-texts</c>.</summary>
internal sealed class RulesExtractTextsSettings : CommandSettings
{
    [CommandOption("--locale <CODE>")]
    [Description("Jazyk, ve kterém jsou texty zapnutých sad napsané (výchozí cs).")]
    [DefaultValue("cs")]
    public string Locale { get; init; } = "cs";

    [CommandOption("--set-locale <SET=CODE>")]
    [Description("Jiný jazyk textů jedné sady, např. legal_sk=sk; volbu lze opakovat.")]
    public string[] SetLocales { get; init; } = [];

    [CommandOption("--skeleton <CODE>")]
    [Description("Místo přesunu vytvoří kostru překladu do jazyka: soubory se stejnými klíči a prázdnými texty.")]
    public string? Skeleton { get; init; }

    public override ValidationResult Validate()
    {
        foreach (var pair in SetLocales)
        {
            if (pair.Split('=') is not [{ Length: > 0 }, { Length: > 0 }])
            {
                return ValidationResult.Error($"--set-locale čeká sada=jazyk, je „{pair}“.");
            }
        }

        return ValidationResult.Success();
    }
}

/// <summary>
/// <c>eshopguard rules extract-texts</c>: moves the texts of the enabled rule sets from <c>rules/*.yaml</c> to
/// <c>rules/texts/&lt;locale&gt;/</c> unchanged (once), or with <c>--skeleton</c> writes empty files of a new language for a
/// translator. No paid call.
/// </summary>
internal sealed class RulesExtractTextsCommand : AsyncCommand<RulesExtractTextsSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, RulesExtractTextsSettings settings, CancellationToken cancellationToken)
    {
        CliConfiguration configuration;
        try
        {
            configuration = CliConfiguration.Load();
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        var rules = configuration.Settings.Rules;
        if (settings.Skeleton is { } locale)
        {
            var skeleton = await RuleMaintenance.WriteSkeletonAsync(rules, locale, cancellationToken);
            AnsiConsole.MarkupLine(skeleton.Count == 0 ? "Kostra nic nepřidala, soubory už existují." : $"Kostra jazyka {Markup.Escape(locale)}: {skeleton.Count} souborů.");
            foreach (var file in skeleton)
            {
                AnsiConsole.MarkupLine($"  {Markup.Escape(file)}");
            }

            return 0;
        }

        var setLocales = settings.SetLocales.Select(p => p.Split('=')).ToDictionary(p => p[0], p => p[1], StringComparer.Ordinal);
        var (written, errors) = await RuleMaintenance.ExtractTextsAsync(rules, settings.Locale, setLocales, cancellationToken);
        foreach (var file in written)
        {
            AnsiConsole.MarkupLine($"  {Markup.Escape(file)}");
        }

        foreach (var error in errors)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(error)}[/]");
        }

        AnsiConsole.MarkupLine(written.Count == 0 && errors.Count == 0 ? "Nic k přesunu, texty už jsou v rules/texts." : $"Zapsáno souborů: {written.Count}.");
        return errors.Count == 0 ? 0 : 1;
    }
}
