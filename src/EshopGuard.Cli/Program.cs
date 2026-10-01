using System.Text;
using EshopGuard.Cli.Commands;
using Spectre.Console.Cli;

Console.OutputEncoding = Encoding.UTF8;

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
    config.AddCommand<ServeFixtureCommand>("serve-fixture")
        .WithDescription("Spustí lokální testovací e-shop.")
        .WithExample("serve-fixture", "--port", "8000");
});

return await app.RunAsync(args, cancellation.Token);
