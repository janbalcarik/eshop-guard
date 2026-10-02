using EshopGuard.Core.Profiles;
using EshopGuard.Core.Storage;
using EshopGuard.Data.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Cli.Tests;

/// <summary>
/// A run that may pay keeps its cache in PostgreSQL and checks the database before it downloads anything (tasks 4.2–4.5);
/// a mock run never touches the database.
/// </summary>
public sealed class DatabaseCheckTests
{
    private const string Unreachable = "Host=127.0.0.1;Port=1;Database=eshopguard;Username=eshopguard_worker;Password=x;Timeout=2";

    [Fact]
    public async Task UnreachableDatabase_StopsTheScanBeforeAnyDownload()
    {
        var folder = CliProcess.NewWorkingFolder();
        var (url, requests, stop) = CliProcess.StartFixture();
        try
        {
            var (exitCode, output) = await CliProcess.RunAsync(folder,
                new Dictionary<string, string?> { ["ConnectionStrings__Cli"] = Unreachable, ["JEV_API_KEY"] = "not-used" },
                "scan", url, "--allow-private-network", "--yes", "--out", "out");

            Assert.Equal(1, exitCode);
            Assert.Contains("Databáze cache není dostupná", output.ReplaceLineEndings(" "), StringComparison.Ordinal);
            Assert.Empty(requests);
        }
        finally
        {
            await stop.CancelAsync();
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task MarketsWithProfiles_ChecksTheDatabaseBeforeAnyDownload()
    {
        var folder = CliProcess.NewWorkingFolder();
        var (url, requests, stop) = CliProcess.StartFixture(Path.Combine("versions", "path-shop"));
        try
        {
            var (exitCode, output) = await CliProcess.RunAsync(folder,
                new Dictionary<string, string?> { ["ConnectionStrings__Cli"] = Unreachable, ["OPENAI_API_KEY"] = null },
                "markets", url, "--profiles", "--allow-private-network", "--yes", "--out", "out");

            Assert.Equal(1, exitCode);
            Assert.Contains("Databáze cache není dostupná", output.ReplaceLineEndings(" "), StringComparison.Ordinal);
            Assert.Empty(requests);
        }
        finally
        {
            await stop.CancelAsync();
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task MissingConnection_StopsARunThatMayPay()
    {
        var folder = CliProcess.NewWorkingFolder();
        try
        {
            var (exitCode, output) = await CliProcess.RunAsync(folder,
                new Dictionary<string, string?> { ["ConnectionStrings__Cli"] = null, ["JEV_API_KEY"] = "not-used", ["HOME"] = folder, ["APPDATA"] = folder },
                "check-text", "Tento šampon je ekologický.");

            Assert.Equal(1, exitCode);
            Assert.Contains("ConnectionStrings__Cli", output.ReplaceLineEndings(" "), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void RunThatMayPay_GetsThePostgresStores_TheMockNone()
    {
        var log = Path.Combine(Directory.CreateTempSubdirectory("eshopguard-log-").FullName, "run.log");
        var configuration = new CliConfiguration { Settings = new SettingsFile(), CacheConnectionString = Unreachable, ApiKey = "not-used" };

        using (var real = CliHost.BuildServices(configuration, log, useMock: false, noCache: false))
        {
            Assert.IsType<PgJevCache>(real.GetRequiredService<IJevCache>());
            Assert.IsType<PgRewriteCache>(real.GetRequiredService<IRewriteCache>());
            Assert.IsType<PgPageProfileStore>(real.GetRequiredService<IPageProfileStore>());
            Assert.Equal(CliTenant.Id, real.GetRequiredService<IStoreTenant>().TenantId);
        }

        using (var noCache = CliHost.BuildServices(configuration, log, useMock: false, noCache: true))
        {
            Assert.Equal("NullJevCache", noCache.GetRequiredService<IJevCache>().GetType().Name);
            Assert.Equal("NullRewriteCache", noCache.GetRequiredService<IRewriteCache>().GetType().Name);
            Assert.IsType<PgPageProfileStore>(noCache.GetRequiredService<IPageProfileStore>());
        }

        using var mock = CliHost.BuildServices(configuration, log, useMock: true, noCache: false);
        Assert.Equal("NullJevCache", mock.GetRequiredService<IJevCache>().GetType().Name);
        Assert.Equal("InMemoryPageProfileStore", mock.GetRequiredService<IPageProfileStore>().GetType().Name);
        Assert.Null(mock.GetService<EshopGuard.Data.Connections.DatabaseInspector>());
    }
}
