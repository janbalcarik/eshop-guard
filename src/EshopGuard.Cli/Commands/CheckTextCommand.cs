using System.ComponentModel;
using System.Globalization;
using EshopGuard.Core;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Rules;
using EshopGuard.Core.Rules.Texts;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EshopGuard.Cli.Commands;

internal sealed class CheckTextSettings : CommandSettings
{
    [CommandArgument(0, "<text>")]
    [Description("Text k vyhodnocení (věta nebo odstavec; řádky oddělují bloky).")]
    public string Text { get; init; } = "";

    [CommandOption("--kind <KIND>")]
    [Description("sentence (marketingový text) nebo legal (text právní stránky).")]
    [DefaultValue("sentence")]
    public string Kind { get; init; } = "sentence";

    [CommandOption("--modules <LIST>")]
    [Description("Moduly pravidel oddělené čárkou (dnes eco, dur, lr, ucp, legal). Bez volby běží všechny, které mají pravidla pro zvolené země.")]
    [DefaultValue("")]
    public string Modules { get; init; } = "";

    [CommandOption("--country <CODE>")]
    [Description("Země, jejíž pravidla se použijí (kód z config/jurisdictions.yaml, dnes sk nebo cz).")]
    [DefaultValue("sk")]
    public string Country { get; init; } = "sk";

    [CommandOption("--jurisdictions <LIST>")]
    [Description("Víc zemí najednou, oddělené čárkou (např. sk,cz). Nahrazuje --country.")]
    public string? Jurisdictions { get; init; }

    [CommandOption("--lang <CODE>")]
    [Description("Jazyk textů pravidel a upozornění (výchozí cs).")]
    [DefaultValue("cs")]
    public string Lang { get; init; } = "cs";

    [CommandOption("--as-of <DATE>")]
    [Description("Datum vyhodnocení (rrrr-mm-dd) pro účinnost pravidel; výchozí dnešek.")]
    public string? AsOf { get; init; }

    [CommandOption("--category <TEXT>")]
    [Description("Kategorie výrobku (název produktu, drobečková navigace nebo kategorie e-shopu) pro seznam zákonných požadavků v modulu lr. Věta sama se za kategorii nepovažuje.")]
    public string? Category { get; init; }

    [CommandOption("--question-lang <LANG>")]
    [Description("Jazyk otázek pro Jev: en nebo cs.")]
    [DefaultValue("en")]
    public string QuestionLanguage { get; init; } = "en";

    [CommandOption("--mock")]
    [Description("Místo Jevu použije falešný klient, žádná volání API.")]
    public bool Mock { get; init; }

    [CommandOption("--no-cache")]
    [Description("Obejde cache odpovědí Jevu.")]
    public bool NoCache { get; init; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Text))
        {
            return ValidationResult.Error("Zadejte text k vyhodnocení.");
        }

        if (Kind is not ("sentence" or "legal"))
        {
            return ValidationResult.Error("--kind musí být sentence nebo legal.");
        }

        return SettingsValidation.Validate(Modules, Country, QuestionLanguage, Jurisdictions, Lang, AsOf);
    }
}

/// <summary>
/// <c>eshopguard check-text "&lt;text&gt;"</c>: evaluates one text and prints question probabilities and the result of every rule.
/// </summary>
internal sealed class CheckTextCommand : AsyncCommand<CheckTextSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CheckTextSettings settings, CancellationToken cancellationToken)
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

        if ((configuration.MissingKeyMessage(settings.Mock) ?? configuration.MissingDatabaseMessage(settings.Mock)) is { } missingKey)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(missingKey)}[/]");
            return 1;
        }

        Directory.CreateDirectory("out");
        await using var services = CliHost.BuildServices(configuration, Path.Combine("out", "check-text.log"), settings.Mock, settings.NoCache);

        if (await CliDatabase.CheckAsync(services, cancellationToken) is { } databaseError)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(databaseError)}[/]");
            return 1;
        }
        var jurisdictions = SettingsValidation.ParseJurisdictions(settings.Country, settings.Jurisdictions);
        var modules = SettingsValidation.ParseModules(settings.Modules);
        if (await SettingsValidation.LoadRulesAsync(services, jurisdictions, modules, settings.Lang, cancellationToken) is not { } catalog)
        {
            return 1;
        }

        var texts = new RuleTextRenderer(catalog);
        var guard = services.GetRequiredService<IEshopGuard>();

        AnalysisResult result;
        try
        {
            result = await guard.AnalyzeTextsAsync(
                [new TextInput { Text = settings.Text, Kind = settings.Kind == "legal" ? TextKind.Legal : TextKind.Sentence, Category = settings.Category }],
                new AnalyzeOptions
                {
                    Modules = modules,
                    Country = jurisdictions[0],
                    Jurisdictions = jurisdictions,
                    AsOf = SettingsValidation.ParseAsOf(settings.AsOf),
                    QuestionLanguage = settings.QuestionLanguage,
                },
                cancellationToken);
        }
        catch (RuleValidationException ex)
        {
            AnsiConsole.MarkupLine("[red]Pravidla nejsou platná:[/]");
            foreach (var error in ex.Errors)
            {
                AnsiConsole.MarkupLine($"  - {Markup.Escape(error)}");
            }

            return 1;
        }
        catch (JevApiException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        if (result.Segments.Count == 0)
        {
            var segmentation = configuration.Settings.Segmentation;
            AnsiConsole.MarkupLine($"Text je příliš krátký: segment musí mít aspoň {segmentation.MinSegmentLength} znaků a jedno slovo s {segmentation.MinWordLetters} písmeny.");
            return 0;
        }

        var several = jurisdictions.Count > 1;
        foreach (var segment in result.Segments)
        {
            PrintSegment(segment, result.RuleResults.Where(r => r.SegmentHash == segment.Hash).ToList(), several);
        }

        foreach (var siteResult in result.RuleResults.Where(r => r.SegmentHash is null))
        {
            AnsiConsole.MarkupLine($"{Markup.Escape(RuleName(siteResult, several))}: {Outcome(siteResult)}");
        }

        AnsiConsole.MarkupLine($"Nálezy: [bold]{result.Findings.Count}[/]. Model: {Markup.Escape(result.JevModel ?? "nevolán")}, volání {result.Stats.JevCalls}, z cache {result.Stats.JevCacheHits}, vstupní tokeny {result.Stats.InputTokens:N0} ({result.Stats.EstimatedCostUsd:0.000000} USD).");
        if (result.JevModel == "mock")
        {
            AnsiConsole.MarkupLine("[grey]Falešný klient: pravděpodobnosti jsou z klíčových slov, ne z Jevu.[/]");
        }

        foreach (var warning in result.Warnings)
        {
            AnsiConsole.MarkupLine($"[yellow]Upozornění:[/] {Markup.Escape(texts.Warning(warning, settings.Lang))}");
        }

        return 0;
    }

    /// <summary>The rule, with its jurisdiction when the run has several.</summary>
    private static string RuleName(RuleResult result, bool several) => several ? $"{result.RuleId} ({result.Jurisdiction.ToUpperInvariant()})" : result.RuleId;

    private static void PrintSegment(Segment segment, List<RuleResult> results, bool several)
    {
        var kind = segment.Kind == SegmentKind.Sentence ? "Věta" : "Právní odstavec";
        AnsiConsole.Write(new Rule($"[bold]{kind}[/]").LeftJustified());
        AnsiConsole.MarkupLine(Markup.Escape(segment.Text));
        if (segment.ContextBefore.Length > 0)
        {
            AnsiConsole.MarkupLine($"[grey]Před: {Markup.Escape(segment.ContextBefore)}[/]");
        }

        if (segment.ContextAfter.Length > 0)
        {
            AnsiConsole.MarkupLine($"[grey]Po: {Markup.Escape(segment.ContextAfter)}[/]");
        }

        var questions = new Table().Border(TableBorder.Rounded).AddColumn("ID otázky").AddColumn(new TableColumn("Pravděpodobnost").RightAligned());
        // Question ids alone, unless rule sets of several jurisdictions ask the same id.
        var ambiguous = segment.Probabilities.Keys.GroupBy(QuestionKey.QuestionId).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        foreach (var (key, probability) in segment.Probabilities)
        {
            var id = QuestionKey.QuestionId(key);
            questions.AddRow(Markup.Escape(ambiguous.Contains(id) ? key : id), probability.ToString("0.000", CultureInfo.InvariantCulture));
        }

        var rules = new Table().Border(TableBorder.Rounded).AddColumn("Pravidlo").AddColumn("Výsledek").AddColumn(new TableColumn("Skóre").RightAligned());
        foreach (var result in results)
        {
            rules.AddRow(Markup.Escape(RuleName(result, several)), Outcome(result), result.Score?.ToString("0.000", CultureInfo.InvariantCulture) ?? "");
        }

        AnsiConsole.Write(new Columns(questions, rules));
    }

    private static string Outcome(RuleResult result) => result.Outcome switch
    {
        RuleOutcome.Finding => result.Band == FindingBand.High ? "[red]nález, vysoká jistota[/]" : "[yellow]nález, k ověření[/]",
        RuleOutcome.ConditionsNotMet => "podmínky nesplněny",
        RuleOutcome.BelowThreshold => "pod prahem",
        RuleOutcome.ExcludedByAllowlist => "vyloučeno seznamem značek",
        RuleOutcome.NotInList => "seznam zákonných požadavků to nepotvrdil",
        RuleOutcome.Duplicate => "stejný text už je nálezem na stránce",
        RuleOutcome.Present => "[green]informace je uvedena[/]",
        _ => "nevyhodnoceno (chyba Jevu)",
    };
}
