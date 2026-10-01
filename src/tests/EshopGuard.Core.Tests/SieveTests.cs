using EshopGuard.Core.Extract;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Segmentation;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// The block sieve: chunks of the main text, planted findings kept, and nothing left out when the sieve fails.
/// </summary>
public class SieveTests
{
    [Fact]
    public void Chunker_KeepsBlocksWholeAndChunksShort()
    {
        TextBlock[] blocks = [new("Prvý odsek."), new("Druhý odsek."), new(new string('x', 30)), new("Posledný.")];

        var chunks = SieveChunker.Chunk(blocks, maxChars: 25);

        Assert.Equal(["Prvý odsek.\nDruhý odsek.", new string('x', 30), "Posledný."], chunks.Select(c => c.Text));
        Assert.Equal([0, 0, 1, 2], SieveChunker.ChunkOfBlock(chunks, blocks.Length));
    }

    [Fact]
    public async Task Scan_KeepsEveryPlantedFindingAndLeavesOutChunksWithoutTopic()
    {
        var withSieve = await ScanAsync(useSieve: true);
        var without = await ScanAsync(useSieve: false);

        Assert.True(withSieve.Stats.SieveEnabled);
        Assert.True(withSieve.Stats.SieveChunks > 0);
        Assert.True(withSieve.Stats.SieveSkippedPairs > 0);
        Assert.True(withSieve.Stats.JevCalls < without.Stats.JevCalls);
        Assert.Equal(
            without.Findings.Select(f => (f.RuleId, f.Text)).Order(),
            withSieve.Findings.Select(f => (f.RuleId, f.Text)).Order());
        Assert.False(without.Stats.SieveEnabled);
        Assert.Equal(0, without.Stats.SieveSkippedPairs);
        Assert.Contains(withSieve.Findings, f => f.RuleId == "dur_lifetime_claim");
    }

    [Fact]
    public async Task FailingSieve_LeavesNothingOut()
    {
        var result = await ScanAsync(useSieve: true, new FailingSieveClient());

        Assert.True(result.Stats.SieveErrors > 0);
        Assert.Equal(0, result.Stats.SieveSkippedPairs);
        Assert.Contains(result.Warnings, w => w.Contains("Síto nevyhodnotilo", StringComparison.Ordinal));
        Assert.All(result.SieveChunks, c => Assert.Equal("error", c.Status));
    }

    private static async Task<ScanResult> ScanAsync(bool useSieve, IJevClient? client = null)
    {
        await using var provider = TestServices.Create(FileSystemPageFetcher.ForSlovakFixture(), configure: null, client);
        return await provider.GetRequiredService<IEshopGuard>().ScanSiteAsync(
            FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions { Country = "sk", UseSieve = useSieve }, ct: TestContext.Current.CancellationToken);
    }

    /// <summary>The mock client, except that every sieve request fails.</summary>
    private sealed class FailingSieveClient : IJevClient
    {
        private readonly MockJevClient _mock = new();

        public Task<JevResult> EvaluateAsync(object state, IReadOnlyDictionary<string, JevQuestion> questions, CancellationToken ct) =>
            questions.Keys.Any(k => k.StartsWith("sieve_", StringComparison.Ordinal))
                ? throw new JevApiException("Jev vrátil HTTP 500.", 500, null, isFatal: false)
                : _mock.EvaluateAsync(state, questions, ct);
    }
}
