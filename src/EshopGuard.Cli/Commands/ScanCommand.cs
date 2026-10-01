using System.ComponentModel;
using EshopGuard.Core;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Report;
using EshopGuard.Core.Rules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EshopGuard.Cli.Commands;

internal sealed class ScanSettings : CommandSettings
{
    [CommandArgument(0, "<url>")]
    [Description("Adresa e-shopu, obvykle úvodní stránka.")]
    public string Url { get; init; } = "";

    [CommandOption("--max-pages <N>")]
    [Description("Maximální počet stažených stránek (výchozí ze settings.yaml, 200).")]
    public int? MaxPages { get; init; }

    [CommandOption("--sample-products <N>")]
    [Description("Maximální počet produktových stránek (výchozí 100).")]
    public int? SampleProducts { get; init; }

    [CommandOption("--modules <LIST>")]
    [Description("Moduly pravidel oddělené čárkou (eco, dur, lr, ucp, legal). Bez volby běží všechny, které mají pravidla pro zvolenou zemi.")]
    [DefaultValue("")]
    public string Modules { get; init; } = "";

    [CommandOption("--country <CODE>")]
    [Description("Sada právních pravidel a jazyk zprávy: cz nebo sk.")]
    [DefaultValue("sk")]
    public string Country { get; init; } = "sk";

    [CommandOption("--question-lang <LANG>")]
    [Description("Jazyk otázek pro Jev: en nebo cs.")]
    [DefaultValue("en")]
    public string QuestionLanguage { get; init; } = "en";

    [CommandOption("--rate <N>")]
    [Description("Pevné tempo stahování v požadavcích za sekundu. Bez volby se tempo přizpůsobuje serveru: začne na 1 za sekundu a při rychlých odpovědích bez chyb zrychlí až na 3 (max_requests_per_second).")]
    public double? Rate { get; init; }

    [CommandOption("--concurrency <N>")]
    [Description("Paralelní požadavky na Jev (výchozí 8).")]
    public int? Concurrency { get; init; }

    [CommandOption("--include <REGEX>")]
    [Description("Stahovat jen URL odpovídající výrazu (lze opakovat).")]
    public string[] Include { get; init; } = [];

    [CommandOption("--exclude <REGEX>")]
    [Description("Nestahovat URL odpovídající výrazu (lze opakovat).")]
    public string[] Exclude { get; init; } = [];

    [CommandOption("--out <DIR>")]
    [Description("Složka pro výstupy.")]
    [DefaultValue("out")]
    public string Out { get; init; } = "out";

    [CommandOption("--mock")]
    [Description("Místo Jevu použije falešný klient, žádná volání API.")]
    public bool Mock { get; init; }

    [CommandOption("--no-cache")]
    [Description("Obejde cache odpovědí Jevu.")]
    public bool NoCache { get; init; }

    [CommandOption("--no-sieve")]
    [Description("Vypne síto po odstavcích: všechny věty projdou podrobnými otázkami všech modulů (dražší a pomalejší).")]
    public bool NoSieve { get; init; }

    [CommandOption("--yes")]
    [Description("Nepožadovat potvrzení nad limitem volání.")]
    public bool Yes { get; init; }

    public override ValidationResult Validate()
    {
        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return ValidationResult.Error("Adresa musí být úplná URL začínající http:// nebo https://.");
        }

        if (MaxPages is < 1 || SampleProducts is < 0 || Concurrency is < 1 || Rate is <= 0)
        {
            return ValidationResult.Error("Limity musí být kladná čísla.");
        }

        return SettingsValidation.Validate(Modules, Country, QuestionLanguage);
    }
}

/// <summary>
/// <c>eshopguard scan &lt;url&gt;</c>: crawls and evaluates the site through the library and writes the outputs.
/// </summary>
internal sealed class ScanCommand : AsyncCommand<ScanSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, ScanSettings settings, CancellationToken cancellationToken)
    {
        var siteUrl = new Uri(settings.Url);
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

        if (configuration.MissingKeyMessage(settings.Mock) is { } missingKey)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(missingKey)}[/]");
            return 1;
        }

        foreach (var note in configuration.Notes)
        {
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(note)}[/]");
        }

        if (!siteUrl.IsLoopback && configuration.Settings.Crawl.UserAgent.Contains("doplnte", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[yellow]V config/settings.yaml doplňte do user_agent skutečný kontakt, než budete skenovat cizí web.[/]");
        }

        var outputDirectory = UniqueDirectory(Path.Combine(settings.Out, $"{SiteFolderName(siteUrl)}-{DateTime.Now:yyyyMMdd-HHmm}"));
        Directory.CreateDirectory(outputDirectory);
        var logFile = Path.Combine(outputDirectory, "run.log");

        await using var services = CliHost.BuildServices(configuration, logFile, settings.Mock, settings.NoCache);
        var logger = services.GetRequiredService<ILogger<ScanCommand>>();
        var checker = services.GetRequiredService<IEshopGuard>();
        logger.LogInformation("Jev: {Mode}, key from {Source}, cache {Cache}",
            settings.Mock ? "mock" : "API", settings.Mock ? "-" : configuration.ApiKeySource, settings.Mock || settings.NoCache ? "off" : configuration.Settings.Cache.Path);

        // The library asks for confirmation in the middle of the scan; the live progress display cannot show a prompt,
        // so the scan runs in two displays: crawling until the estimate arrives, then the evaluation.
        var estimateArrived = new TaskCompletionSource<JevCallEstimate>(TaskCreationOptions.RunContinuationsAsynchronously);
        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ProgressTask? currentTask = null;
        var progress = new SynchronousProgress<ScanProgress>(p =>
        {
            var task = currentTask;
            if (task is null)
            {
                return;
            }

            task.MaxValue = Math.Max(1, p.Total);
            task.Value = p.Completed;
            task.Description = p.Stage switch
            {
                ScanStage.Discovery => "Čtení robots.txt a sitemap",
                ScanStage.Pages => $"Stahování stránek {p.Completed}/{p.Total}",
                ScanStage.Segmentation => "Dělení textů na segmenty",
                ScanStage.Sieve => $"Síto {p.Completed}/{p.Total}",
                _ => $"Vyhodnocení {p.Completed}/{p.Total}",
            };
        });

        var options = new ScanOptions
        {
            MaxPages = settings.MaxPages,
            SampleProducts = settings.SampleProducts,
            Modules = SettingsValidation.ParseModules(settings.Modules),
            Country = settings.Country,
            QuestionLanguage = settings.QuestionLanguage,
            RequestsPerSecond = settings.Rate,
            Concurrency = settings.Concurrency,
            Include = settings.Include,
            Exclude = settings.Exclude,
            UseSieve = !settings.NoSieve,
            ConfirmJevCalls = async (estimate, ct) =>
            {
                estimateArrived.TrySetResult(estimate);
                return await answer.Task.WaitAsync(ct);
            },
        };

        ScanResult result;
        try
        {
            var scan = checker.ScanSiteAsync(siteUrl, options, progress, cancellationToken);
            await RunWithProgressAsync("Stahování stránek", t => currentTask = t, Task.WhenAny(scan, estimateArrived.Task));
            currentTask = null;

            if (estimateArrived.Task.IsCompleted)
            {
                var proceed = Confirm(estimateArrived.Task.Result, settings.Yes);
                answer.SetResult(proceed);
                if (proceed)
                {
                    await RunWithProgressAsync("Vyhodnocení", t => currentTask = t, scan);
                    currentTask = null;
                }
            }

            result = await scan;
            foreach (var writer in services.GetServices<IReportWriter>())
            {
                await writer.WriteAsync(result, outputDirectory, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Přerušeno.[/]");
            return 130;
        }
        catch (RuleValidationException ex)
        {
            AnsiConsole.MarkupLine("[red]Pravidla nejsou platná, nic se nestahovalo:[/]");
            foreach (var error in ex.Errors)
            {
                AnsiConsole.MarkupLine($"  - {Markup.Escape(error)}");
            }

            return 1;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scan failed");
            AnsiConsole.MarkupLine($"[red]Scan selhal:[/] {Markup.Escape(ex.Message)}");
            AnsiConsole.MarkupLine($"Podrobnosti jsou v {Markup.Escape(logFile)}.");
            return 1;
        }

        PrintSummary(result, outputDirectory);
        return 0;
    }

    private static async Task RunWithProgressAsync(string description, Action<ProgressTask> setTask, Task work)
    {
        await AnsiConsole.Progress()
            .AutoClear(true)
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new SpinnerColumn())
            .StartAsync(async context =>
            {
                setTask(context.AddTask(description, maxValue: 1));
                try
                {
                    await work;
                }
                catch
                {
                    // Errors are reported after the display closes.
                }
            });
    }

    private static bool Confirm(JevCallEstimate estimate, bool yes)
    {
        var price = estimate.IsMock ? "falešný klient, nic se neplatí" : $"odhad {estimate.EstimatedCostUsd:0.000000} USD";
        var cached = estimate.CachedCalls > 0 ? $" a {estimate.CachedCalls} odpovědí z cache zdarma" : "";
        var bound = estimate.UpperBound ? $" nejvýš (z toho {estimate.SieveCalls} volání síta; síto část vět z podrobné kontroly vyřadí)" : "";
        AnsiConsole.MarkupLine($"Odhad: [bold]{estimate.Calls}[/] volání Jevu{Markup.Escape(bound)}{cached}, asi {estimate.EstimatedInputTokens:N0} vstupních tokenů ({Markup.Escape(price)}).");
        if (estimate.ProfileTemplates > 0)
        {
            var profiles = estimate.ProfilesWillRun
                ? $"odhad {estimate.ProfileCostUsd:0.0000} USD za OpenAI; nový profil jen ubírá text, Jev bude nejvýš podle odhadu výše"
                : $"nevytvoří se: {estimate.ProfilesUnavailableReason}; při ostrém běhu asi {estimate.ProfileCostUsd:0.0000} USD";
            AnsiConsole.MarkupLine($"Profily šablon stránek: nejvýš [bold]{estimate.ProfileTemplates}[/] nových ({Markup.Escape(profiles)}).");
        }

        if (!estimate.RequiresConfirmation || yes)
        {
            return true;
        }

        return AnsiConsole.Confirm($"Odhad překračuje limit (max_calls_without_confirm nebo profiles.max_usd_without_confirm). Pokračovat?", defaultValue: false);
    }

    private static void PrintSummary(ScanResult result, string outputDirectory)
    {
        var stats = result.Stats;
        var table = new Table().Border(TableBorder.Rounded).AddColumn("Ukazatel").AddColumn(new TableColumn("Hodnota").RightAligned());
        table.AddRow("Stažené stránky", stats.PagesFetched.ToString());
        foreach (var (type, label) in new[] { (PageType.Home, "  úvodní"), (PageType.Product, "  produktové"), (PageType.Legal, "  právní"), (PageType.Content, "  ostatní") })
        {
            table.AddRow(label, stats.PagesByType.GetValueOrDefault(type).ToString());
        }

        table.AddRow("Zakázané v robots.txt", stats.PagesBlockedByRobots.ToString());
        table.AddRow("Vyřazené filtrem URL", stats.PagesExcludedByFilter.ToString());
        table.AddRow("Nestažené kvůli limitu", stats.PagesOverLimit.ToString());
        table.AddRow("Produkty nad limit vzorku", stats.ProductPagesOverLimit.ToString());
        table.AddRow("Chyby stahování", stats.PagesFailed.ToString());
        table.AddRow("Text se nenačetl (JavaScript), nezkontrolováno", stats.PagesTextNotLoaded.ToString());
        if (stats.VisibleTextChars > 0)
        {
            table.AddRow("Viditelný text: zkontrolováno / navigace / výpisy produktů",
                $"{(double)stats.CheckedTextChars / stats.VisibleTextChars:0 %} / {(double)stats.NavigationTextChars / stats.VisibleTextChars:0 %} / {(double)stats.ListingTextChars / stats.VisibleTextChars:0 %}");
            table.AddRow("Stránky s nezkontrolovaným textem", stats.PagesWithUncheckedText.ToString());
        }
        table.AddRow("Stahování: čas / tempo na konci / zpomalení serverem",
            $"{stats.CrawlSeconds:0.0} s / {stats.CrawlFinalRate:0.0} za s / {stats.CrawlThrottled}×");
        table.AddRow("Extrakce SmartReader / záložní", $"{stats.PagesWithReadability} / {stats.PagesWithFallback}");
        if (stats.ProfilesEnabled && (stats.ProfilesUsed > 0 || stats.ProfilesPlanned > 0))
        {
            table.AddRow("Profily šablon: použité / nové / stránky s profilem / bez profilu",
                $"{stats.ProfilesUsed} / {stats.ProfilesCreated} / {stats.PagesWithProfile} / {stats.PagesWithoutProfile}");
            if (stats.VisibleTextChars > 0)
            {
                table.AddRow("Vynecháno podle profilu (ovládací prvky)", $"{(double)stats.ProfileSkippedTextChars / stats.VisibleTextChars:0 %} viditelného textu");
            }

            if (stats.ProfileCalls > 0)
            {
                table.AddRow("Profily: volání OpenAI / cena", $"{stats.ProfileCalls} / {stats.ProfileCostUsd:0.0000} USD");
            }
        }
        table.AddRow("Unikátní segmenty", $"{stats.UniqueSegments} ({stats.SentenceSegments} vět, {stats.LegalParagraphSegments} odstavců, {stats.BoilerplateSegments} šablonových)");
        table.AddRow("Volání Jevu / z cache / chyby", $"{stats.JevCalls} / {stats.JevCacheHits} / {stats.JevErrors}");
        table.AddRow("Síto: úseky / volání / vynechané dvojice", stats.SieveEnabled ? $"{stats.SieveChunks} / {stats.SieveCalls} / {stats.SieveSkippedPairs} z {stats.SievePairs}" : "vypnuté");
        table.AddRow("Vstupní tokeny", stats.InputTokens.ToString("N0"));
        table.AddRow("Odhad ceny", $"{stats.EstimatedCostUsd:0.000000} USD{(result.JevModel == "mock" ? " (mock)" : "")}");
        table.AddRow("Nezkontrolované právní PDF", result.UncheckedDocuments.Count.ToString());
        table.AddRow("Doba běhu", $"{stats.DurationSeconds:0.0} s");
        AnsiConsole.Write(table);

        if (result.Findings.Count > 0)
        {
            var findings = new Table().Border(TableBorder.Rounded)
                .AddColumn("Pravidlo").AddColumn("Závažnost")
                .AddColumn(new TableColumn("Vysoká jistota").RightAligned())
                .AddColumn(new TableColumn("Nižší jistota").RightAligned());
            foreach (var rule in result.Findings.GroupBy(f => f.RuleId).OrderBy(g => SeverityRank(g.First().Severity)))
            {
                findings.AddRow(
                    Markup.Escape(rule.First().Title),
                    rule.First().Severity,
                    rule.Count(f => f.Band == FindingBand.High).ToString(),
                    rule.Count(f => f.Band == FindingBand.Review).ToString());
            }

            AnsiConsole.Write(findings);
        }
        else
        {
            AnsiConsole.MarkupLine(result.EvaluationSkipped ? "Vyhodnocení neproběhlo." : "Žádné nálezy.");
        }

        if (result.JevModel == "mock")
        {
            AnsiConsole.MarkupLine("[grey]Běh s falešným klientem: nálezy jen ověřují průchod aplikací, neříkají nic o skutečném webu.[/]");
        }

        foreach (var warning in result.Warnings)
        {
            AnsiConsole.MarkupLine($"[yellow]Upozornění:[/] {Markup.Escape(warning)}");
        }

        AnsiConsole.MarkupLine($"Výstupy: [bold]{Markup.Escape(Path.GetFullPath(outputDirectory))}[/]");
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "high" => 0,
        "medium" => 1,
        _ => 2,
    };

    /// <summary>Two runs within the same minute get "-2", "-3"… instead of overwriting each other.</summary>
    private static string UniqueDirectory(string path)
    {
        var candidate = path;
        for (var suffix = 2; Directory.Exists(candidate); suffix++)
        {
            candidate = $"{path}-{suffix}";
        }

        return candidate;
    }

    private static string SiteFolderName(Uri siteUrl)
    {
        var host = siteUrl.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? siteUrl.Host[4..] : siteUrl.Host;
        return siteUrl.IsDefaultPort ? host : $"{host}-{siteUrl.Port}";
    }
}

/// <summary>Reports on the calling thread, so the progress display never shows stale values after the work ends.</summary>
internal sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
