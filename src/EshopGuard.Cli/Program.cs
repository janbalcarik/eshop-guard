using System.Text;
using EshopGuard.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

Console.OutputEncoding = Encoding.UTF8;

// Redirected output (a pipe, a test, CI) has no console width, and the default then cuts every line to "…".
if (Console.IsOutputRedirected)
{
    AnsiConsole.Profile.Width = 200;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("eshopguard");
    config.AddCommand<ScanCommand>("scan")
        .WithDescription("Projde web e-shopu, vyhodnotí jeho texty a vytvoří zprávu.")
        .WithExample("scan", "http://localhost:8000", "--mock");
    config.AddCommand<CheckTextCommand>("check-text")
        .WithDescription("Vyhodnotí jeden text a vypíše pravděpodobnosti otázek a výsledky pravidel.")
        .WithExample("check-text", "\"Tento šampon je ekologický a šetrný k přírodě.\"", "--mock");
    config.AddCommand<RewriteCommand>("rewrite")
        .WithDescription("Navrhne přepis problematických pasáží hotového skenu modelem OpenAI a znovu je zkontroluje.")
        .WithExample("rewrite", "out/shop.sk-20260930-0919", "--limit", "5");
    config.AddCommand<MarketsCommand>("markets")
        .WithDescription("Zjistí, kde e-shop prodává a jaké má jazykové verze (místa prodeje s ověřenými citacemi, plán verzí), a zapíše markets.json.")
        .WithExample("markets", "https://www.example.sk", "--yes");
    config.AddBranch("rules", rules =>
    {
        rules.SetDescription("Údržba pravidel a jejich textů v rules/ a rules/texts/.");
        rules.AddCommand<RulesCheckTextsCommand>("check-texts")
            .WithDescription("Zkontroluje texty pravidel po jazycích (úplnost, kontrola člověkem) a otisky otázek.");
        rules.AddCommand<RulesExtractTextsCommand>("extract-texts")
            .WithDescription("Přesune texty zapnutých sad z rules/*.yaml do rules/texts/<jazyk>/, nebo s --skeleton vytvoří kostru překladu.")
            .WithExample("rules", "extract-texts", "--skeleton", "de");
    });
    config.AddBranch("cache", cache =>
    {
        cache.SetDescription("Cache odpovědí Jevu, přepisů a profilů šablon v PostgreSQL.");
        cache.AddCommand<CacheInitCommand>("init")
            .WithDescription("Založí v databázi cache tenanta cli (jednou) a vypíše, co cache obsahuje.");
    });
    config.AddCommand<BenchExtractCommand>("bench-extract")
        .WithDescription("Změří čas procesoru extrakce stránek nad nahrávkou webu (scan --record).")
        .WithExample("bench-extract", "--replay", "snapshots/vegis.sk");
    config.AddCommand<ServeFixtureCommand>("serve-fixture")
        .WithDescription("Spustí lokální testovací e-shop.")
        .WithExample("serve-fixture", "--port", "8000");
});

return await app.RunAsync(args, cancellation.Token);
