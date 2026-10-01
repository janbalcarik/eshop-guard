using System.ComponentModel;
using EshopGuard.Core.Fix;
using EshopGuard.Core.Jev;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EshopGuard.Cli.Commands;

internal sealed class RewriteSettings : CommandSettings
{
    [CommandArgument(0, "<run>")]
    [Description("Složka s výsledky skenu (findings.json a pages.jsonl); rewrite.md a rewrite.json vzniknou v ní.")]
    public string RunDirectory { get; init; } = "";

    [CommandOption("--country <CODE>")]
    [Description("Sada pravidel pro kontrolu přepsaných pasáží: cz nebo sk.")]
    [DefaultValue("sk")]
    public string Country { get; init; } = "sk";

    [CommandOption("--limit <N>")]
    [Description("Přepíše nejvýš N stránek (podle URL), na zkoušku.")]
    public int? Limit { get; init; }

    [CommandOption("--mock")]
    [Description("Falešný model i falešný Jev, žádná volání API.")]
    public bool Mock { get; init; }

    [CommandOption("--no-cache")]
    [Description("Obejde mezipaměť přepisů i odpovědí Jevu.")]
    public bool NoCache { get; init; }

    [CommandOption("--yes")]
    [Description("Nepotvrzovat cenu nad limitem rewrite.max_usd_without_confirm.")]
    public bool Yes { get; init; }

    public override ValidationResult Validate() =>
        string.IsNullOrWhiteSpace(RunDirectory) ? ValidationResult.Error("Zadejte složku s výsledky skenu.")
        : Limit is <= 0 ? ValidationResult.Error("--limit musí být kladné číslo.")
        : SettingsValidation.Validate("", Country, "en");
}

/// <summary>
/// <c>eshopguard rewrite &lt;run&gt;</c>: rewrites the problematic passages of a finished scan and writes rewrite.md and rewrite.json.
/// A thin wrapper for testing; the work is done by <see cref="ITextRewriter"/> in the library.
/// </summary>
internal sealed class RewriteCommand : AsyncCommand<RewriteSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, RewriteSettings settings, CancellationToken cancellationToken)
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

        var missing = new[] { configuration.MissingKeyMessage(settings.Mock), configuration.MissingOpenAiKeyMessage(settings.Mock), configuration.MissingDatabaseMessage(settings.Mock) }
            .FirstOrDefault(m => m is not null);
        if (missing is not null)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(missing)}[/]");
            return 1;
        }

        RewriteInput input;
        try
        {
            input = ScanOutputReader.Read(settings.RunDirectory, settings.Country, settings.Limit);
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        await using var services = CliHost.BuildServices(configuration, Path.Combine(settings.RunDirectory, "rewrite.log"), settings.Mock, settings.NoCache);

        if (await CliDatabase.CheckAsync(services, cancellationToken) is { } databaseError)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(databaseError)}[/]");
            return 1;
        }
        var rewriter = services.GetRequiredService<ITextRewriter>();
        try
        {
            var estimate = await rewriter.EstimateAsync(input, cancellationToken);
            if (estimate.Pages == 0)
            {
                AnsiConsole.MarkupLine("Sken nemá nálezy ve skupinách porušení a k posouzení, není co přepisovat.");
                return 0;
            }

            var price = estimate.IsMock ? "falešný model, nic se neplatí" : $"odhad {estimate.EstimatedCostUsd:0.0000} USD";
            AnsiConsole.MarkupLine(Markup.Escape(
                $"Přepis: {estimate.Pages} stránek, {estimate.Findings} nálezů, z mezipaměti {estimate.CachedPages}. Model {estimate.Model}, " +
                $"asi {estimate.EstimatedInputTokens:N0} vstupních tokenů (z mezipaměti OpenAI {estimate.EstimatedCachedTokens:N0}) " +
                $"a {estimate.EstimatedOutputTokens:N0} výstupních ({price})."));
            if (estimate.RequiresConfirmation && !settings.Yes
                && !AnsiConsole.Confirm("Odhad překračuje limit rewrite.max_usd_without_confirm. Pokračovat?", defaultValue: false))
            {
                return 0;
            }

            var result = await AnsiConsole.Status().StartAsync("Přepisuji…", async status =>
                await rewriter.RewriteAsync(input, new Progress<RewriteProgress>(p => status.Status($"Přepisuji… {p.Done}/{p.Total}")), cancellationToken));
            await RewriteReportWriter.WriteAsync(result, settings.RunDirectory, cancellationToken);

            var findings = result.Pages.SelectMany(p => p.Findings).ToList();
            var table = new Table().Border(TableBorder.Rounded).AddColumn("Ukazatel").AddColumn(new TableColumn("Hodnota").RightAligned());
            table.AddRow("Stránek / z mezipaměti / chyb", $"{result.Stats.Pages} / {result.Stats.FromCache} / {result.Stats.Errors}");
            foreach (var status in Enum.GetValues<RewriteStatus>())
            {
                table.AddRow($"Nálezy: {RewriteReportWriter.Status(status)}", findings.Count(f => f.Status == status).ToString());
            }

            table.AddRow("Vstupní tokeny (z mezipaměti)", $"{result.Stats.InputTokens:N0} ({result.Stats.CachedTokens:N0})");
            table.AddRow("Výstupní tokeny (přemýšlení)", $"{result.Stats.OutputTokens:N0} ({result.Stats.ReasoningTokens:N0})");
            table.AddRow("Cena modelu / kontroly Jevem", $"{result.Stats.CostUsd:0.0000} / {result.Stats.CheckCostUsd:0.0000} USD");
            table.AddRow("Doba", $"{result.Stats.Duration.TotalSeconds:0} s");
            AnsiConsole.Write(table);
            foreach (var warning in result.Warnings)
            {
                AnsiConsole.MarkupLine($"[yellow]Upozornění:[/] {Markup.Escape(warning)}");
            }

            AnsiConsole.MarkupLine(Markup.Escape($"Zpráva: {Path.GetFullPath(Path.Combine(settings.RunDirectory, RewriteReportWriter.MarkdownFile))}"));
            return 0;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Přerušeno.[/]");
            return 130;
        }
        catch (Exception ex) when (ex is RewriteApiException or JevApiException or InvalidOperationException)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }
}
