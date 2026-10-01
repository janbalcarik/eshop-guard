using EshopGuard.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// The Jev cache keys of the scans are the same as before change 5 (<c>Baselines/jev-legacy-keys.txt</c>), so the stored
/// answers of the CLI stay valid and a real run pays nothing more.
/// </summary>
public sealed class JevCacheKeyCompatibilityTests
{
    public static TheoryData<string> Scenarios => new(BaselineScenarios.All.Select(s => s.Name));

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task LegacyKeys_AreTheSameAsBeforeChange5(string name)
    {
        var (_, fetcher, options) = BaselineScenarios.All.Single(s => s.Name == name);
        var expected = File.ReadLines(Path.Combine(BaselineScenarios.OutputBaselines, "jev-legacy-keys.txt"))
            .Select(l => l.Split('\t'))
            .Where(p => p[0] == name)
            .Select(p => p[1])
            .ToList();

        var (_, keys) = await BaselineScenarios.RunAsync(name, fetcher, options);

        Assert.NotEmpty(expected);
        Assert.Equal(expected, keys);
    }

    [Fact]
    public async Task SieveAndDetailKeys_HaveTheirKind_AndShareQuestionSets()
    {
        var cache = new BaselineScenarios.KeyRecordingJevCache();
        await using var provider = TestServices.Create(FileSystemPageFetcher.ForSlovakFixture(), register: s => s.AddSingleton<IJevCache>(cache));
        await provider.GetRequiredService<IEshopGuard>().ScanSiteAsync(
            FileSystemPageFetcher.DefaultBaseUrl, new Options.ScanOptions { Country = "sk" }, ct: TestContext.Current.CancellationToken);

        var keys = cache.StructuredKeys;
        var sieve = keys.Where(k => k.Kind == JevCacheKind.Sieve).ToList();
        var detail = keys.Where(k => k.Kind == JevCacheKind.Detail).ToList();
        Assert.NotEmpty(sieve);
        Assert.NotEmpty(detail);

        // One question set per sieve definition and per rule set version, many states each.
        Assert.Single(sieve.Select(k => k.QuestionSetHash).Distinct());
        Assert.InRange(detail.Select(k => k.QuestionSetHash).Distinct().Count(), 2, 10);
        Assert.Equal(keys.Count, keys.Select(k => k.LegacyKey).Distinct().Count());
        Assert.Empty(sieve.Select(k => k.QuestionSetHash).Intersect(detail.Select(k => k.QuestionSetHash)));
    }
}
