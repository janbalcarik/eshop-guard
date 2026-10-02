using System.ComponentModel;
using EshopGuard.Core.Languages;
using EshopGuard.Core.Markets;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EshopGuard.Cli.Commands;

internal sealed class MarketsSettings : CommandSettings
{
    [CommandArgument(0, "<url>")]
    [Description("Adresa e-shopu (úvodní stránka).")]
    public string Url { get; init; } = "";

    [CommandOption("--mock")]
    [Description("Falešný model, žádné volání OpenAI; místa prodeje se nezjistí, jen technické znaky a verze ze stavby webu.")]
    public bool Mock { get; init; }

    [CommandOption("--yes")]
    [Description("Nepotvrzovat odhad ceny nad limitem rewrite.max_usd_without_confirm.")]
    public bool Yes { get; init; }

    [CommandOption("--out <DIR>")]
    [Description("Složka pro výstupy (vznikne v ní podsložka webu s markets.json a markets.log).")]
    [DefaultValue("out")]
    public string Out { get; init; } = "out";

    [CommandOption("--markets <LIST>")]
    [Description("Zaškrtnuté trhy (kódy z config/jurisdictions.yaml, např. sk,cz); bez volby se vezmou předvyplněné z rozboru.")]
    public string? Markets { get; init; }

    [CommandOption("--profiles")]
    [Description("Porovnat jazykové verze jen na popisu produktu z profilu šablony (bez recenzí a textů šablony); chybějící profily vytvoří model (OpenAI, odhad ceny se potvrzuje) a uloží je do databáze, kontrola webu je pak použije. Bez --mock potřebuje databázi jako scan.")]
    public bool Profiles { get; init; }

    [CommandOption("--allow-private-network")]
    [Description("Povolí adresy ve vnitřní a místní síti a jiné porty než 80 a 443 (jen pro místní testovací e-shop, serve-fixture).")]
    public bool AllowPrivateNetwork { get; init; }

    public override ValidationResult Validate() =>
        !Uri.TryCreate(Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            ? ValidationResult.Error("Adresa musí být úplná URL začínající http:// nebo https://.")
            : ValidationResult.Success();
}

/// <summary>
/// <c>eshopguard markets &lt;url&gt;</c>: the places of sale and the language versions of a shop (change 7) through the library;
/// writes markets.json. A thin wrapper: the work is done by <see cref="IMarketsAnalyzer"/>. Needs no database, except with
/// <c>--profiles</c>: the profiles of page templates are kept in PostgreSQL like those of <c>scan</c>. The OpenAI key is only
/// read from the environment and never written anywhere.
/// </summary>
internal sealed class MarketsCommand : AsyncCommand<MarketsSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, MarketsSettings settings, CancellationToken cancellationToken)
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

        if (!siteUrl.IsLoopback && configuration.Settings.Crawl.UserAgent.Contains("doplnte", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[yellow]V config/settings.yaml doplňte do user_agent skutečný kontakt, než budete skenovat cizí web.[/]");
        }

        // Profiles are stored in PostgreSQL (tenant cli) like those of scan; a mock run never touches the database.
        var useDatabase = settings.Profiles && !settings.Mock;
        if (useDatabase && configuration.MissingDatabaseMessage(settings.Mock) is { } missingDatabase)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(missingDatabase)}[/]");
            return 1;
        }

        configuration.AllowPrivateNetwork = settings.AllowPrivateNetwork;
        var host = siteUrl.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? siteUrl.Host[4..] : siteUrl.Host;
        var name = Path.Combine(settings.Out, $"{(siteUrl.IsDefaultPort ? host : $"{host}-{siteUrl.Port}")}-markets-{DateTime.Now:yyyyMMdd-HHmm}");
        var folder = name;
        for (var suffix = 2; Directory.Exists(folder); suffix++)
        {
            folder = $"{name}-{suffix}";
        }

        Directory.CreateDirectory(folder);
        await using var services = CliHost.BuildServices(configuration, Path.Combine(folder, "markets.log"), settings.Mock, noCache: true, useDatabase: useDatabase);
        if (useDatabase && await CliDatabase.CheckAsync(services, cancellationToken) is { } databaseError)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(databaseError)}[/]");
            return 1;
        }

        var limit = configuration.Settings.Rewrite.MaxUsdWithoutConfirm;
        MarketEstimateConfirmation confirm = (estimate, _) =>
        {
            AnsiConsole.MarkupLine(Markup.Escape(
                $"Odhad rozboru modelem: nejvýš {estimate.Calls} volání, asi {estimate.InputTokens:N0} vstupních a {estimate.OutputTokens:N0} výstupních tokenů, {estimate.CostUsd:0.0000} USD"
                + (estimate.ProfilesUsd > 0 ? $", z toho nové profily šablon {estimate.ProfilesUsd:0.0000} USD." : ".")));
            return Task.FromResult(settings.Yes || estimate.CostUsd <= limit
                || AnsiConsole.Confirm("Odhad překračuje limit rewrite.max_usd_without_confirm. Pokračovat?", defaultValue: false));
        };

        var request = new MarketsAnalysisRequest(siteUrl)
        {
            ActiveMarkets = settings.Markets?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Profiles = settings.Profiles ? VersionProfileMode.Create : VersionProfileMode.None,
        };
        MarketsAnalysisResult result;
        try
        {
            result = await AnsiConsole.Status().StartAsync("Rozbor míst prodeje a verzí…", async status =>
                await services.GetRequiredService<IMarketsAnalyzer>().AnalyzeAsync(request, confirm,
                    new SynchronousProgress<string>(stage => status.Status($"Rozbor míst prodeje a verzí: {stage}…")), cancellationToken));
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Přerušeno.[/]");
            return 130;
        }

        var file = Path.Combine(folder, "markets.json");
        await File.WriteAllTextAsync(file, result.ToJson(), new System.Text.UTF8Encoding(false), cancellationToken);
        Print(result);
        AnsiConsole.MarkupLine(Markup.Escape($"Výstup: {Path.GetFullPath(file)}"));
        return 0;
    }

    private static void Print(MarketsAnalysisResult result)
    {
        var markets = new Table().Border(TableBorder.Rounded).AddColumn("Země").AddColumn("Síla důkazu").AddColumn("Stav").AddColumn("Předvyplněno").AddColumn("Důkaz");
        foreach (var row in result.Markets)
        {
            markets.AddRow(
                Markup.Escape(row.CountryCode + (row.IsHome ? " (domovská" + (row.HomeNeedsConfirmation ? ", k potvrzení)" : ")") : "")),
                row.EvidenceLevel, row.Status, row.Preselected ? "ano" : "ne",
                Markup.Escape(row.Evidence.Quotes.FirstOrDefault() is { } q ? $"{Shorten(q.Quote)} ({q.Source})" : row.Evidence.RaisedBy ?? ""));
        }

        if (result.Markets.Count > 0)
        {
            AnsiConsole.Write(markets);
        }
        else
        {
            AnsiConsole.MarkupLine("Místa prodeje: žádná země nemá ověřený důkaz ani doménu trhu.");
        }

        var versions = new Table().Border(TableBorder.Rounded).AddColumn("Verze").AddColumn("Adresa").AddColumn("Přepnutí").AddColumn("Stav")
            .AddColumn(new TableColumn("Produktů").RightAligned()).AddColumn("Popisy v jazyku verze").AddColumn("Kontroluje se pro").AddColumn("Kódy");
        foreach (var row in result.Versions)
        {
            var checkedFor = result.Plan?.Checked.FirstOrDefault(v => v.BaseUrl == row.BaseUrl && v.Language == (row.Language ?? "und"))?.Markets ?? [];
            versions.AddRow(Markup.Escape(row.Language ?? "?"), Markup.Escape(row.BaseUrl), row.SwitchMethod, row.Status,
                row.ProductCount?.ToString() ?? (row.ProductCountAtLeast is { } least ? $"≥ {least}" : ""),
                row.DescriptionLanguages is { LabeledProducts: > 0 } d ? $"{d.TranslatedProducts} z {d.LabeledProducts}" : "",
                string.Join(", ", checkedFor).ToUpperInvariant(),
                Markup.Escape(string.Join(", ", row.Codes.Concat(row.Warnings))));
        }

        AnsiConsole.Write(versions);
        foreach (var row in result.Versions.Where(v => v.DescriptionLanguages is not null))
        {
            AnsiConsole.MarkupLine(Markup.Escape($"Verze {row.Language ?? "?"}: {Described(row.DescriptionLanguages!)}"));
        }

        if (result.Summary is { } summary)
        {
            AnsiConsole.MarkupLine(Markup.Escape($"Věta pro 3c: {summary.Code} {Describe(summary.Params)}"));
        }

        foreach (var notice in result.Notices)
        {
            AnsiConsole.MarkupLine(Markup.Escape($"Upozornění pro 3c: {notice.Code} {Describe(notice.Params)}"));
        }

        if (result.Plan is { } plan)
        {
            AnsiConsole.MarkupLine(Markup.Escape(
                $"Plán: {string.Join("; ", plan.Checked.Select(v => $"{v.Language} podle {string.Join(", ", v.Jurisdictions)}"))}."));
            var byMarket = plan.ByMarket.Select(m => $"{m.Market.ToUpperInvariant()} {m.ProductCount?.ToString() ?? "?"} ({m.Language})");
            AnsiConsole.MarkupLine(Markup.Escape(
                $"Produkty do ceny za každou zemi: {string.Join(" + ", byMarket)}"
                + (plan.ProductCountUnknown ? "; součet neznámý (počet produktů některé verze neznámý, dodá ho konektor)." : $" = {plan.CountedProducts}.")));
        }

        if (result.Codes.Count > 0)
        {
            AnsiConsole.MarkupLine(Markup.Escape($"Kódy rozboru: {string.Join(", ", result.Codes)}"));
        }

        AnsiConsole.MarkupLine(Markup.Escape(
            $"Model: {result.Model ?? "nevolán"}, volání {result.Usage.Calls}, vstupní tokeny {result.Usage.InputTokens:N0}, výstupní {result.Usage.OutputTokens:N0}, cena {result.Usage.CostUsd:0.0000} USD; požadavků na web {result.Usage.Requests}."));
        if (result.ProfilesCreated.Count > 0 || result.ProfileUsage.Calls > 0)
        {
            AnsiConsole.MarkupLine(Markup.Escape(
                $"Nové profily šablon: {string.Join(", ", result.ProfilesCreated)}; volání {result.ProfileUsage.Calls}, vstupní tokeny {result.ProfileUsage.InputTokens:N0}, výstupní {result.ProfileUsage.OutputTokens:N0}, cena {result.ProfileUsage.CostUsd:0.0000} USD."));
        }

        if (result.Codes.Contains("model_mock"))
        {
            AnsiConsole.MarkupLine("[yellow]Falešný model: místa prodeje nejsou zjištěná, výsledek ukazuje jen technické znaky a verze ze stavby webu.[/]");
        }
    }

    /// <summary>What the language of the descriptions of a version rests on and the products in another language, in one line.</summary>
    private static string Described(DescriptionLanguages descriptions)
    {
        var basis = descriptions.Basis switch
        {
            SentenceSources.Description => $"jazyk z popisu produktu z profilu ({descriptions.DescriptionPages} stránek)",
            SentenceSources.Mixed => $"jazyk z popisu z profilu u {descriptions.DescriptionPages} stránek, u ostatních z celého hlavního textu",
            _ => "jazyk z celého hlavního textu stránek (bez profilu, i s recenzemi a texty šablony)",
        };
        var parts = new List<string> { basis };
        if (descriptions.LabeledProducts > 0)
        {
            parts.Add($"popisy v jiném jazyce {descriptions.ForeignTextProducts} z {descriptions.LabeledProducts}{(descriptions.ForeignTextLanguage is { } language ? $" ({language})" : "")}");
        }

        return string.Join("; ", parts) + ".";
    }

    private static string Describe(IReadOnlyDictionary<string, object> parameters) =>
        string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value switch
        {
            string[] list => string.Join("+", list),
            IReadOnlyDictionary<string, string> map => string.Join("+", map.Select(m => $"{m.Key}:{m.Value}")),
            _ => p.Value,
        }}"));

    private static string Shorten(string text) => text.Length <= 60 ? text : text[..57] + "…";
}
