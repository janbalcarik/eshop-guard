using System.ComponentModel;
using EshopGuard.Core;
using EshopGuard.Core.Rules;
using EshopGuard.Core.Rules.Texts;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EshopGuard.Cli.Commands;

/// <summary>Settings of <c>eshopguard rules check-texts</c>.</summary>
internal sealed class RulesCheckTextsSettings : CommandSettings
{
    [CommandOption("--update-hashes")]
    [Description("Zapíše do rules/question-set-hashes.json otisk otázek nových verzí sad (po kontrole změny otázek).")]
    public bool UpdateHashes { get; init; }
}

/// <summary>
/// <c>eshopguard rules check-texts</c>: loads the rules with their texts and prints, language by language, whether the texts
/// are complete and reviewed and what is missing. No paid call.
/// </summary>
internal sealed class RulesCheckTextsCommand : AsyncCommand<RulesCheckTextsSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, RulesCheckTextsSettings settings, CancellationToken cancellationToken)
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

        if (settings.UpdateHashes)
        {
            var (added, hashErrors) = await RuleMaintenance.UpdateQuestionSetHashesAsync(configuration.Settings.Rules, cancellationToken);
            foreach (var error in hashErrors)
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(error)}[/]");
            }

            AnsiConsole.MarkupLine(added.Count == 0 ? "Otisky otázek: nic nového." : $"Otisky otázek zapsány: {Markup.Escape(string.Join(", ", added))}.");
            if (hashErrors.Count > 0)
            {
                return 1;
            }
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEshopGuard(options => configuration.Apply(options, useMock: true, noCache: true));
        await using var provider = services.BuildServiceProvider();
        RuleCatalog catalog;
        try
        {
            catalog = await provider.GetRequiredService<IRuleSetProvider>().LoadAsync(cancellationToken);
        }
        catch (RuleValidationException ex)
        {
            AnsiConsole.MarkupLine("[red]Pravidla nebo texty jsou neplatné:[/]");
            foreach (var error in ex.Errors)
            {
                AnsiConsole.MarkupLine($"  - {Markup.Escape(error)}");
            }

            return 1;
        }

        var texts = catalog.Texts;
        var table = new Table().Border(TableBorder.Rounded).AddColumn("Jazyk").AddColumn("Stav").AddColumn(new TableColumn("Problémy").RightAligned());
        foreach (var locale in texts.Locales.Keys.Order(StringComparer.Ordinal))
        {
            var problems = texts.Problems.GetValueOrDefault(locale) ?? [];
            var state = texts.CompleteLocales.Contains(locale) ? "[green]úplný a zkontrolovaný[/]"
                : texts.ToolLocales.Contains(locale) ? "[yellow]texty nástroje úplné, některé sady v jiném jazyce[/]"
                : "[red]neúplný nebo nezkontrolovaný[/]";
            table.AddRow(Markup.Escape(locale), state, problems.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        AnsiConsole.Write(table);
        foreach (var (locale, problems) in texts.Problems.Where(p => p.Value.Count > 0).OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            AnsiConsole.MarkupLine($"[bold]{Markup.Escape(locale)}[/]:");
            foreach (var problem in problems)
            {
                AnsiConsole.MarkupLine($"  - {Markup.Escape(problem)}");
            }
        }

        // Rules whose finding depends on facts outside the web should ask the user (proposal of change 6, decision 13).
        var withoutQuestions = catalog.RuleSets.Where(s => s.Enabled)
            .SelectMany(s => s.Rules.Where(r => r.Checkability == "verify" && r.UserQuestions.Count == 0).Select(r => $"{s.Name}:{r.Id}"))
            .ToList();
        if (withoutQuestions.Count > 0)
        {
            AnsiConsole.MarkupLine($"[grey]Pravidla k ověření bez otázky pro uživatele (čekají na schválení znění): {Markup.Escape(string.Join(", ", withoutQuestions))}[/]");
        }

        AnsiConsole.MarkupLine($"Úplné jazyky: {(texts.CompleteLocales.Count == 0 ? "žádný" : string.Join(", ", texts.CompleteLocales))}; zpráva jde napsat v: {string.Join(", ", texts.ToolLocales)}.");
        return 0;
    }
}
