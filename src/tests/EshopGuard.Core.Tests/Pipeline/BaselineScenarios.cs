using System.Collections.Concurrent;
using EshopGuard.Core.Cache;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Options;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Scans of the fixture e-shops whose outputs the code before change 5 wrote into <c>Baselines/</c>
/// (<see cref="BaselineDumpTests"/>); the pipeline must give the same outputs (<c>PipelineEquivalenceTests</c>).
/// </summary>
internal static class BaselineScenarios
{
    /// <summary>Name, fetcher and options of every scenario.</summary>
    public static IReadOnlyList<(string Name, Func<FileSystemPageFetcher> Fetcher, Func<ScanOptions> Options)> All { get; } =
    [
        ("site", FileSystemPageFetcher.ForFixture, () => new ScanOptions { Country = "cz" }),
        ("site-limits", FileSystemPageFetcher.ForFixture, () => new ScanOptions { Country = "cz", MaxPages = 5, SampleProducts = 2 }),
        ("site-sk", FileSystemPageFetcher.ForSlovakFixture, () => new ScanOptions { Country = "sk" }),
        ("site-sk-nosieve", FileSystemPageFetcher.ForSlovakFixture, () => new ScanOptions { Country = "sk", UseSieve = false }),
    ];

    /// <summary>Folder <c>Baselines</c> in the test output.</summary>
    public static string OutputBaselines => Path.Combine(AppContext.BaseDirectory, "Baselines");

    /// <summary>Folder <c>Baselines</c> in the source tree (written by the dump).</summary>
    public static string SourceBaselines
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "EshopGuard.Core.Tests.csproj")))
                {
                    return Path.Combine(dir.FullName, "Baselines");
                }
            }

            throw new DirectoryNotFoundException("EshopGuard.Core.Tests.csproj not found above the test output.");
        }
    }

    /// <summary>Runs the scenario; returns its normalized outputs and the Jev cache keys the scan asked for.</summary>
    public static async Task<(SortedDictionary<string, string> Outputs, IReadOnlyCollection<string> CacheKeys)> RunAsync(
        string name, Func<FileSystemPageFetcher> fetcher, Func<ScanOptions> options)
    {
        var keys = new KeyRecordingJevCache();
        await using var provider = TestServices.Create(fetcher(), register: services => services.AddSingleton<IJevCache>(keys));
        var result = await provider.GetRequiredService<IEshopGuard>().ScanSiteAsync(
            FileSystemPageFetcher.DefaultBaseUrl, options(), ct: TestContext.Current.CancellationToken);
        return (await OutputNormalizer.WriteAsync(provider, result), keys.Keys);
    }

    /// <summary>Empty cache that records every key it is asked for.</summary>
    private sealed class KeyRecordingJevCache : IJevCache
    {
        private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> Keys => [.. _keys.Keys.Order(StringComparer.Ordinal)];

        public Task<JevResult?> GetAsync(string key, CancellationToken ct = default)
        {
            _keys.TryAdd(key, 0);
            return Task.FromResult<JevResult?>(null);
        }

        public Task SetAsync(string key, JevResult result, CancellationToken ct = default)
        {
            _keys.TryAdd(key, 0);
            return Task.CompletedTask;
        }
    }
}

/// <summary>
/// Writes the reference outputs of the code before change 5 (tasks 1.6 and 1.7). Explicit: runs only on request
/// (<c>dotnet run --project src/tests/EshopGuard.Core.Tests -- -explicit only -trait "Category=Baseline"</c>).
/// </summary>
public sealed class BaselineDumpTests
{
    [Fact(Explicit = true)]
    [Trait("Category", "Baseline")]
    public async Task DumpBaselines()
    {
        var root = BaselineScenarios.SourceBaselines;
        var keyLines = new List<string>();
        foreach (var (name, fetcher, options) in BaselineScenarios.All)
        {
            var (outputs, keys) = await BaselineScenarios.RunAsync(name, fetcher, options);
            var folder = Path.Combine(root, name);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }

            Directory.CreateDirectory(folder);
            foreach (var (file, content) in outputs)
            {
                await File.WriteAllTextAsync(Path.Combine(folder, file), content, new System.Text.UTF8Encoding(false), TestContext.Current.CancellationToken);
            }

            keyLines.AddRange(keys.Select(k => $"{name}\t{k}"));
        }

        await File.WriteAllLinesAsync(Path.Combine(root, "jev-legacy-keys.txt"), keyLines, TestContext.Current.CancellationToken);
    }
}
