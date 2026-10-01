using System.Diagnostics;
using EshopGuard.Core.Tests;

namespace EshopGuard.Cli.Tests;

/// <summary>
/// The CLI over local recordings of real e-shops (<c>src/snapshots/</c>, outside git): <c>eshopguard scan --replay
/// --mock</c> must give the same outputs as the code before change 5 (<c>src/baselines/</c>, outside git). Explicit:
/// <c>dotnet run --project src/tests/EshopGuard.Cli.Tests -- -explicit only -trait "Category=Snapshot"</c>.
/// </summary>
public sealed class SnapshotTests
{
    public static TheoryData<string, string> Sites => new()
    {
        { "vegis.sk", "https://vegis.sk/" },
        { "www.naturfyt.sk", "https://www.naturfyt.sk/" },
    };

    /// <summary>Writes <c>src/baselines/{snapshot}</c> with the current code (run once before change 5).</summary>
    [Theory(Explicit = true)]
    [MemberData(nameof(Sites))]
    [Trait("Category", "SnapshotBaseline")]
    public async Task DumpSnapshotBaseline(string snapshot, string url)
    {
        var outputs = await ScanReplayAsync(snapshot, url);
        var folder = Path.Combine(SourceRoot, "baselines", snapshot);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        Directory.CreateDirectory(folder);
        foreach (var (file, content) in outputs)
        {
            await File.WriteAllTextAsync(Path.Combine(folder, file), content, new System.Text.UTF8Encoding(false), TestContext.Current.CancellationToken);
        }
    }

    [Theory(Explicit = true)]
    [MemberData(nameof(Sites))]
    [Trait("Category", "Snapshot")]
    public async Task ReplayGivesTheBaselineOutputs(string snapshot, string url)
    {
        var baseline = Path.Combine(SourceRoot, "baselines", snapshot);
        if (!Directory.Exists(baseline))
        {
            Assert.Skip($"Chybí referenční výstupy {baseline} (DumpSnapshotBaseline).");
        }

        var expected = OutputNormalizer.Read(baseline);
        var actual = await ScanReplayAsync(snapshot, url);

        Assert.True(expected.SequenceEqual(actual), OutputNormalizer.Differences(expected, actual));
    }

    /// <summary>Runs the built CLI from <c>src/</c> (settings.yaml, rules) over the recording and returns its normalized outputs.</summary>
    private static async Task<SortedDictionary<string, string>> ScanReplayAsync(string snapshot, string url)
    {
        var recording = Path.Combine(SourceRoot, "snapshots", snapshot);
        if (!Directory.Exists(recording))
        {
            Assert.Skip($"Chybí nahrávka {recording} (eshopguard scan {url} --mock --record snapshots/{snapshot}).");
        }

        var output = Directory.CreateTempSubdirectory("eshopguard-snapshot-").FullName;
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = SourceRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { Path.Combine(AppContext.BaseDirectory, "eshopguard.dll"), "scan", url, "--mock", "--replay", recording, "--rate", "1000", "--out", output },
        };
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        Assert.True(process.ExitCode == 0, $"eshopguard skončil kódem {process.ExitCode}:\n{await stdout}\n{await stderr}");

        return OutputNormalizer.Read(Assert.Single(Directory.GetDirectories(output)));
    }

    /// <summary>The folder <c>src/</c> of the repository.</summary>
    private static string SourceRoot
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "EshopGuard.sln")))
                {
                    return dir.FullName;
                }
            }

            throw new DirectoryNotFoundException("EshopGuard.sln not found above the test output.");
        }
    }
}
