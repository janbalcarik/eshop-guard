using EshopGuard.Core.Fix;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Pipeline;
using EshopGuard.Core.Rules;
using EshopGuard.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// A batch of the Jev evaluation can be started again (task 7.6): every answer is in the cache as soon as it arrives, so a
/// batch broken off after 120 answers asks only the remaining 80; a fatal error stops the batch and the answers stay. The
/// estimate before it reads only the cache (task 7.4).
/// </summary>
public sealed class EvaluateBatchIdempotenceTests
{
    [Fact]
    public async Task BrokenOffBatch_StartedAgain_AsksOnlyTheRest()
    {
        var cache = new InMemoryJevCache();
        var client = new ScriptedJevClient { CancelAfter = 120 };
        await using var provider = Create(client, cache);
        var (input, ruleSets) = await BatchAsync(provider, 200);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GetRequiredService<EvaluateStep>().EvaluateAsync(input, ruleSets, null, client.Token));
        Assert.Equal(120, cache.Count);

        client.CancelAfter = null;
        client.Calls = 0;
        var again = await provider.GetRequiredService<EvaluateStep>().EvaluateAsync(input, ruleSets, null, TestContext.Current.CancellationToken);

        Assert.Equal(80, client.Calls);
        Assert.Equal(80, again.Calls);
        Assert.Equal(120, again.CacheHits);
        Assert.Equal(200, again.Probabilities.Count);
        Assert.Empty(again.NotEvaluated);
    }

    [Fact]
    public async Task FatalError_StopsTheBatch_AndKeepsTheAnswers()
    {
        var cache = new InMemoryJevCache();
        var client = new ScriptedJevClient { FatalAt = 50 };
        await using var provider = Create(client, cache);
        var (input, ruleSets) = await BatchAsync(provider, 200);

        await Assert.ThrowsAsync<JevApiException>(() =>
            provider.GetRequiredService<EvaluateStep>().EvaluateAsync(input, ruleSets, null, TestContext.Current.CancellationToken));

        Assert.Equal(49, cache.Count);
        Assert.Equal(50, client.Calls);
    }

    [Fact]
    public async Task FailedRequest_IsListedAsNotEvaluated()
    {
        var client = new ScriptedJevClient { FailAt = 3 };
        await using var provider = Create(client, new InMemoryJevCache());
        var (input, ruleSets) = await BatchAsync(provider, 5);

        var result = await provider.GetRequiredService<EvaluateStep>().EvaluateAsync(input, ruleSets, null, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Errors);
        Assert.Equal([input.Segments[2].Hash], result.NotEvaluated);
        Assert.Equal(4, result.Probabilities.Count);
    }

    [Fact]
    public async Task Estimate_SendsNoRequest()
    {
        var client = new ScriptedJevClient();
        var rewrite = new CountingRewriteClient();
        await using var provider = TestServices.Create(FileSystemPageFetcher.ForSlovakFixture(), o => o.Jev.UseMock = false, client,
            register: s => s.AddSingleton<IRewriteClient>(rewrite).AddSingleton<IJevCache>(new InMemoryJevCache()));
        var result = await provider.GetRequiredService<IEshopGuard>().ScanSiteAsync(FileSystemPageFetcher.DefaultBaseUrl,
            new ScanOptions { ConfirmJevCalls = (_, _) => Task.FromResult(false) }, ct: TestContext.Current.CancellationToken);

        Assert.True(result.EvaluationSkipped);
        Assert.Equal(0, client.Calls);
        Assert.Equal(0, rewrite.Calls);
    }

    private static ServiceProvider Create(IJevClient client, IJevCache cache) =>
        TestServices.Create(FileSystemPageFetcher.ForSlovakFixture(), o => o.Jev.Concurrency = 1, client, register: s => s.AddSingleton(cache));

    /// <summary>A batch of <paramref name="count"/> different sentences for the eco module of Slovakia.</summary>
    private static async Task<(EvaluateBatchInput Input, IReadOnlyList<RuleSet> RuleSets)> BatchAsync(IServiceProvider provider, int count)
    {
        var catalog = await provider.GetRequiredService<IRuleSetProvider>().LoadAsync(TestContext.Current.CancellationToken);
        var ruleSets = RuleSetSelector.Select(catalog, ["eco"], ["sk"]).RuleSets;
        var states = Enumerable.Range(1, count)
            .Select(i => new SegmentState($"sha256:{i:D4}", SegmentKind.Sentence, $"Výrobok číslo {i} je šetrný k prírode.", "", "", ["eco"]))
            .ToList();
        return (new EvaluateBatchInput(states, "en", Concurrency: 1), ruleSets);
    }

    /// <summary>Answers every question; can cancel after some answers, fail one request or fail fatally.</summary>
    private sealed class ScriptedJevClient : IJevClient
    {
        private readonly CancellationTokenSource _cancel = new();

        public int? CancelAfter { get; set; }

        public int? FatalAt { get; init; }

        public int? FailAt { get; init; }

        public int Calls { get; set; }

        public CancellationToken Token => _cancel.Token;

        public async Task<JevResult> EvaluateAsync(object state, IReadOnlyDictionary<string, JevQuestion> questions, CancellationToken ct)
        {
            Calls++;
            if (Calls == FatalAt)
            {
                throw new JevApiException("Kredit vyčerpaný.", 402, null, isFatal: true);
            }

            if (Calls == FailAt)
            {
                throw new JevApiException("Chyba.", 500, null, isFatal: false);
            }

            if (Calls > CancelAfter)
            {
                await _cancel.CancelAsync();
                ct.ThrowIfCancellationRequested();
            }

            return new JevResult
            {
                Model = "jev-1.13.0",
                Answers = questions.ToDictionary(q => q.Key, _ => new JevAnswer { Type = "noul", Noul = 0.2 }),
                Usage = new JevUsage { InputTokens = 100 },
            };
        }
    }

    private sealed class CountingRewriteClient : IRewriteClient
    {
        public int Calls { get; private set; }

        public Task<RewriteResponse> RewriteAsync(RewriteRequest request, CancellationToken ct)
        {
            Calls++;
            throw new InvalidOperationException("The estimate must not call the model.");
        }
    }
}
