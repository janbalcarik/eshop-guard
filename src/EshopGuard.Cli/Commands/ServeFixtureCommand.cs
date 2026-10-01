using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EshopGuard.Cli.Commands;

internal sealed class ServeFixtureSettings : CommandSettings
{
    [CommandOption("--port <PORT>")]
    [Description("Port lokálního serveru.")]
    [DefaultValue(8000)]
    public int Port { get; init; } = 8000;

    [CommandOption("--root <DIR>")]
    [Description("Složka se soubory testovacího e-shopu.")]
    [DefaultValue("tests/EshopGuard.Core.Tests/Fixtures/site")]
    public string Root { get; init; } = "tests/EshopGuard.Core.Tests/Fixtures/site";

    public override ValidationResult Validate() =>
        Port is < 1 or > 65535 ? ValidationResult.Error("--port musí být 1–65535.") : ValidationResult.Success();
}

/// <summary>
/// <c>eshopguard serve-fixture</c>: serves the fixture e-shop on localhost until Ctrl+C.
/// </summary>
internal sealed class ServeFixtureCommand : AsyncCommand<ServeFixtureSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, ServeFixtureSettings settings, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(settings.Root);
        if (!Directory.Exists(root))
        {
            AnsiConsole.MarkupLine($"[red]Složka {Markup.Escape(root)} neexistuje.[/] Spusťte příkaz z kořene řešení nebo zadejte --root.");
            return 1;
        }

        var server = new FixtureServer(root, settings.Port);
        AnsiConsole.MarkupLine($"Testovací e-shop běží na [bold]{server.Prefix}[/] (ukončení Ctrl+C).");
        await server.RunAsync(line => AnsiConsole.WriteLine(line), cancellationToken);
        return 0;
    }
}
