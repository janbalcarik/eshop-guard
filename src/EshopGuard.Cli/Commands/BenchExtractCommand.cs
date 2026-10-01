using System.ComponentModel;
using System.Globalization;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EshopGuard.Cli.Commands;

internal sealed class BenchExtractSettings : CommandSettings
{
    [CommandOption("--replay <DIR>")]
    [Description("Nahrávka webu (scan --record), nad jejímiž stránkami se měří.")]
    public string Replay { get; init; } = "";

    [CommandOption("--runs <N>")]
    [Description("Počet měřených průchodů po jednom zahřívacím (výchozí 3); výsledek je medián.")]
    [DefaultValue(3)]
    public int Runs { get; init; } = 3;

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Replay) || !File.Exists(Path.Combine(Replay, PageRecording.IndexFile)))
        {
            return ValidationResult.Error($"Zadejte --replay se složkou nahrávky ({PageRecording.IndexFile}).");
        }

        return Runs < 1 ? ValidationResult.Error("--runs musí být aspoň 1.") : ValidationResult.Success();
    }
}

/// <summary>
/// <c>eshopguard bench-extract --replay &lt;dir&gt;</c>: processor time of extraction, classification and profile
/// preparation per page of a recording. Meaningful in a Release build (<c>dotnet run -c Release</c>).
/// </summary>
internal sealed class BenchExtractCommand : AsyncCommand<BenchExtractSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, BenchExtractSettings settings, CancellationToken cancellationToken)
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

        var logFile = Path.Combine(Path.GetTempPath(), $"eshopguard-bench-{Environment.ProcessId}.log");
        await using var services = CliHost.BuildServices(configuration, logFile, useMock: true, noCache: true);
        var result = await ExtractionBenchmark.RunAsync(services, settings.Replay, settings.Runs, cancellationToken);
#if DEBUG
        AnsiConsole.MarkupLine("[yellow]Sestavení Debug: čísla nejsou srovnatelná, měřte s -c Release.[/]");
#endif
        AnsiConsole.MarkupLine(string.Create(CultureInfo.InvariantCulture,
            $"{result.Pages} stránek, {result.Runs} průchody: procesor [bold]{result.CpuMsPerPage:0.0} ms[/] na stránku (medián), čas {result.WallMsPerPage:0.0} ms na stránku."));
        return 0;
    }
}
