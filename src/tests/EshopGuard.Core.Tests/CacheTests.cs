using EshopGuard.Core.Cache;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Options;
using EshopGuard.Core.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EshopGuard.Core.Tests;

/// <summary>
/// SQLite cache of Jev responses: the second run of the same scan is served from the cache and costs nothing.
/// </summary>
public class CacheTests
{
    [Fact]
    public async Task Sqlite_StoresAndReturnsResponses()
    {
        var directory = Directory.CreateTempSubdirectory("EshopGuard-cache-").FullName;
        try
        {
            var options = new EshopGuardOptions();
            options.Cache.Path = Path.Combine(directory, "nested", "cache.sqlite");
            var cache = new SqliteJevCache(Microsoft.Extensions.Options.Options.Create(options), NullLogger<SqliteJevCache>.Instance);
            var result = new JevResult
            {
                Model = "jev-1.13.0",
                Answers = new Dictionary<string, JevAnswer> { ["eco_claim"] = new() { Type = "noul", Noul = 0.97 } },
                Usage = new JevUsage { InputTokens = 180, OutputTokens = 10 },
            };

            var key = new JevCacheKey(JevCacheKind.Detail, "q", "s", "key");
            Assert.Null(await cache.GetAsync(key with { LegacyKey = "missing" }, TestContext.Current.CancellationToken));
            await cache.SetAsync(key, result, TestContext.Current.CancellationToken);
            var loaded = await cache.GetAsync(key, TestContext.Current.CancellationToken);

            Assert.NotNull(loaded);
            Assert.Equal(0.97, loaded.Answers["eco_claim"].Noul);
            Assert.Equal(180, loaded.Usage.InputTokens);

            // The SQLite file is keyed by the legacy key only, as before change 5.
            var many = await cache.GetManyAsync([key, key with { LegacyKey = "missing" }, key with { Kind = JevCacheKind.Sieve }], TestContext.Current.CancellationToken);
            Assert.Equal(2, many.Count);
            Assert.Equal(0.97, many[key].Answers["eco_claim"].Noul);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Key_DependsOnModelVersionLanguageQuestionsAndContext()
    {
        var questions = new Dictionary<string, JevQuestion> { ["q"] = new() { Type = "noul", Instructions = "Q?" } };
        var state = new Rules.SentenceState("Věta.", "Před.", "Po.");
        JevCacheKey Key(string model = "jev-1.13.0", string version = "eco-1", string language = "en", object? s = null) =>
            JevCacheKeys.Create(JevCacheKind.Detail, model, version, language, questions, s ?? state);
        var key = Key();

        Assert.Equal(key, Key(s: new Rules.SentenceState("Věta.", "Před.", "Po.")));
        Assert.NotEqual(key.LegacyKey, Key(model: "jev-1.14.0").LegacyKey);
        Assert.NotEqual(key.LegacyKey, Key(version: "eco-2").LegacyKey);
        Assert.NotEqual(key.LegacyKey, Key(language: "cs").LegacyKey);
        Assert.NotEqual(key.LegacyKey, Key(s: new Rules.SentenceState("Věta.", "Jiný kontext.", "Po.")).LegacyKey);

        // The question set hash does not depend on the sentence; the state hash does not depend on the questions.
        Assert.Equal(key.QuestionSetHash, Key(s: new Rules.SentenceState("Jiná věta.", "Před.", "Po.")).QuestionSetHash);
        Assert.NotEqual(key.QuestionSetHash, Key(version: "eco-2").QuestionSetHash);
        Assert.Equal(key.StateHash, Key(version: "eco-2").StateHash);
        Assert.NotEqual(key.StateHash, Key(s: "Věta.").StateHash);
    }

    [Fact]
    public async Task SecondScan_IsServedFromCacheAndCostsNothing()
    {
        var directory = Directory.CreateTempSubdirectory("EshopGuard-cache-").FullName;
        var client = new CountingJevClient();
        try
        {
            var first = await ScanAsync(client, Path.Combine(directory, "cache.sqlite"));
            var callsAfterFirst = client.Calls;
            var second = await ScanAsync(client, Path.Combine(directory, "cache.sqlite"));

            Assert.True(first.Stats.JevCalls > 0);
            Assert.Equal(0, first.Stats.JevCacheHits);
            Assert.Equal(callsAfterFirst, first.Stats.JevCalls);

            Assert.Equal(0, second.Stats.JevCalls);
            Assert.Equal(first.Stats.JevCalls, second.Stats.JevCacheHits);
            Assert.Equal(0, second.Stats.InputTokens);
            Assert.Equal(0m, second.Stats.EstimatedCostUsd);
            Assert.Equal(callsAfterFirst, client.Calls);
            Assert.Equal(first.Findings.Select(f => f.RuleId).Order(), second.Findings.Select(f => f.RuleId).Order());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Requests_AskAllModulesOfASentenceAtOnceAndCacheEachModuleSeparately()
    {
        var directory = Directory.CreateTempSubdirectory("EshopGuard-cache-").FullName;
        var client = new CountingJevClient();
        try
        {
            var cachePath = Path.Combine(directory, "cache.sqlite");
            var ecoOnly = await ScanAsync(client, cachePath, "sk", ["eco"]);
            var askedBefore = client.Requests.Count;
            var all = await ScanAsync(client, cachePath, "sk", []);

            // eco answers come from the cache; dur and ucp questions of one sentence go in one request.
            var sentenceRequests = client.Requests.Skip(askedBefore).Where(q => q.Any(id => id.StartsWith("dur_", StringComparison.Ordinal))).ToList();
            Assert.NotEmpty(sentenceRequests);
            Assert.All(sentenceRequests, q =>
            {
                Assert.DoesNotContain(q, id => id.StartsWith("eco_", StringComparison.Ordinal));
                Assert.Contains(q, id => id.StartsWith("ucp_", StringComparison.Ordinal));
            });
            Assert.Equal(ecoOnly.Stats.JevCalls, all.Stats.JevCacheHits);
            Assert.Equal(all.Stats.JevCalls, client.Requests.Count - askedBefore);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<Models.ScanResult> ScanAsync(IJevClient client, string cachePath, string country = "cz", IReadOnlyList<string>? modules = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(client);
        services.AddSingleton<Crawl.IPageFetcher>(country == "sk" ? FileSystemPageFetcher.ForSlovakFixture() : FileSystemPageFetcher.ForFixture());
        services.AddEshopGuard(options =>
        {
            options.Crawl.RequestsPerSecond = 0;
            options.Jev.ApiKey = "not-used";
            options.Cache.Path = cachePath;
            options.Rules.Directory = TestServices.RulesDirectory;
            options.Rules.LabelsFile = TestServices.LabelsFile;
            options.Rules.LegalRequirementsFile = TestServices.LegalRequirementsFile;
            options.Rules.SieveFile = TestServices.SieveFile;
        });
        await using var provider = services.BuildServiceProvider();
        return await provider.GetRequiredService<IEshopGuard>()
            .ScanSiteAsync(FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions { Country = country, Modules = modules ?? [], UseSieve = false }, ct: TestContext.Current.CancellationToken);
    }

    /// <summary>Answers every question with a low probability, counts requests and keeps the question ids of each.</summary>
    private sealed class CountingJevClient : IJevClient
    {
        private int _calls;

        public int Calls => _calls;

        public System.Collections.Concurrent.ConcurrentQueue<string[]> Requests { get; } = new();

        public Task<JevResult> EvaluateAsync(object state, IReadOnlyDictionary<string, JevQuestion> questions, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);
            Requests.Enqueue([.. questions.Keys]);
            return Task.FromResult(new JevResult
            {
                Model = "jev-1.13.0",
                Answers = questions.ToDictionary(q => q.Key, q => new JevAnswer { Type = "noul", Noul = 0.1 }),
                Usage = new JevUsage { InputTokens = 100, OutputTokens = questions.Count },
            });
        }
    }
}
