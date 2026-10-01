namespace EshopGuard.Cli.Tests;

/// <summary>
/// The CLI keeps no cache in local files (change 5b): a mock scan of the local fixture e-shop in an empty working folder
/// leaves only its outputs, no <c>cache/</c> folder and no SQLite file, and needs no database.
/// </summary>
public sealed class NoLocalFilesTests
{
    [Fact]
    public async Task MockScan_LeavesNoCacheFiles_AndNeedsNoDatabase()
    {
        var folder = CliProcess.NewWorkingFolder();
        var (url, requests, stop) = CliProcess.StartFixture();
        try
        {
            var (exitCode, output) = await CliProcess.RunAsync(folder,
                new Dictionary<string, string?> { ["ConnectionStrings__Cli"] = null },
                "scan", url, "--mock", "--allow-private-network", "--yes", "--rate", "1000", "--out", "out");

            Assert.True(exitCode == 0, output);
            Assert.NotEmpty(requests);
            Assert.True(File.Exists(Path.Combine(Assert.Single(Directory.GetDirectories(Path.Combine(folder, "out"))), "report.md")));
            Assert.False(Directory.Exists(Path.Combine(folder, "cache")));
            Assert.Empty(Directory.GetFiles(folder, "*.sqlite*", SearchOption.AllDirectories));
            Assert.Equal(["config", "out", "rules"], Directory.GetFileSystemEntries(folder).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        }
        finally
        {
            await stop.CancelAsync();
            Directory.Delete(folder, recursive: true);
        }
    }
}
