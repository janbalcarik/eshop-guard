using System.Collections.Concurrent;
using EshopGuard.Core;
using EshopGuard.Core.Fix;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Options;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Stores;
using EshopGuard.Data.Tenancy;
using EshopGuard.Core.Tests;
using EshopGuard.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EshopGuard.Cli.Tests;

/// <summary>
/// The CLI keeps the answers of a run in PostgreSQL under the tenant <c>cli</c> (task 4.6): a scan of the fixture e-shop
/// with a fake Jev client writes them, the same scan again sends no request and gives the same outputs. A unique model
/// name per run keeps the keys fresh in the shared test database.
/// </summary>
[Trait("Category", "Db")]
public sealed class CliPostgresCacheTests
{
    [Fact]
    public async Task SecondScan_IsServedFromPostgres_AndGivesTheSameOutputs()
    {
        var ct = TestContext.Current.CancellationToken;
        var model = "jev-test-" + Guid.NewGuid().ToString("N")[..12];
        var client = new CountingJevClient(model);
        var (url, _, stop) = CliProcess.StartFixture();
        var settings = new SettingsFile();
        settings.Rules.Directory = Path.Combine(CliProcess.SourceRoot, "rules");
        settings.Rules.LabelsFile = Path.Combine(CliProcess.SourceRoot, "config", "labels.yaml");
        settings.Rules.LegalRequirementsFile = Path.Combine(CliProcess.SourceRoot, "config", "legal_requirements.yaml");
        settings.Rules.SieveFile = Path.Combine(CliProcess.SourceRoot, "config", "sieve.yaml");
        settings.Rewrite.PromptFile = Path.Combine(CliProcess.SourceRoot, "config", "rewrite.yaml");
        var configuration = new CliConfiguration
        {
            Settings = settings,
            CacheConnectionString = TestConfiguration.ConnectionString("Worker"),
            ApiKey = "not-used",
            Model = model,
            AllowPrivateNetwork = true,
        };
        var log = Path.Combine(Directory.CreateTempSubdirectory("eshopguard-pg-").FullName, "run.log");
        try
        {
            await using var provider = CliHost.BuildServices(configuration, log, useMock: false, noCache: false,
                register: s => s.AddSingleton<IJevClient>(client).AddSingleton<IRewriteClient>(new RefusingRewriteClient()));
            await CliTenant.EnsureAsync(provider.GetRequiredService<EshopGuardDataSource>(), ct);
            Assert.Null(await CliDatabase.CheckAsync(provider, ct));
            var guard = provider.GetRequiredService<IEshopGuard>();
            var options = new ScanOptions { Country = "cz", RequestsPerSecond = 1000 };

            var first = await guard.ScanSiteAsync(new Uri(url), options, ct: ct);
            var callsAfterFirst = client.Calls;
            var second = await guard.ScanSiteAsync(new Uri(url), options, ct: ct);

            Assert.True(first.Stats.JevCalls > 0 && first.Stats.SieveCalls > 0, $"{first.Stats.JevCalls} / {first.Stats.SieveCalls}");
            Assert.Equal(first.Stats.JevCalls + first.Stats.SieveCalls, callsAfterFirst);
            Assert.True(await CountAsync(provider, "checks.jev_answers", model, ct) > 0);
            Assert.Equal(first.Stats.SieveCalls, await CountAsync(provider, "checks.sieve_answers", model, ct));

            Assert.Equal(callsAfterFirst, client.Calls);
            Assert.Equal(0, second.Stats.JevCalls);
            Assert.Equal(0, second.Stats.SieveCalls);
            Assert.Equal(first.Stats.JevCalls, second.Stats.JevCacheHits);
            // The same findings, pages, segments and probabilities; only the counts of calls and cache hits differ.
            var differences = OutputNormalizer.Differences(
                Comparable(await OutputNormalizer.WriteAsync(provider, first)), Comparable(await OutputNormalizer.WriteAsync(provider, second)));
            Assert.True(differences.Length == 0, differences);
        }
        finally
        {
            await stop.CancelAsync();
        }
    }

    /// <summary>The outputs without the report (its statistics count calls and cache hits) and with the sieve status of a cached chunk as "ok".</summary>
    private static SortedDictionary<string, string> Comparable(SortedDictionary<string, string> outputs)
    {
        outputs.Remove("report.md");
        outputs["sieve.csv"] = outputs["sieve.csv"].Replace(",cache,", ",ok,", StringComparison.Ordinal);
        return outputs;
    }

    private static async Task<long> CountAsync(IServiceProvider provider, string table, string model, CancellationToken ct)
    {
        await using var connection = await provider.GetRequiredService<EshopGuardDataSource>().Source.OpenConnectionAsync(ct);
        await using var transaction = await TenantSql.BeginAsync(connection, CliTenant.Id, ct: ct);
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {table} WHERE tenant_id = $1 AND model = $2", connection, transaction)
        {
            Parameters = { new NpgsqlParameter { Value = CliTenant.Id }, new NpgsqlParameter { Value = model } },
        };
        return (long)(await command.ExecuteScalarAsync(ct))!;
    }

    /// <summary>Answers every question with 0.2 under the model of the run and counts requests.</summary>
    private sealed class CountingJevClient(string model) : IJevClient
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public ConcurrentQueue<object> States { get; } = new();

        public Task<JevResult> EvaluateAsync(object state, IReadOnlyDictionary<string, JevQuestion> questions, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(new JevResult
            {
                Model = model,
                Answers = questions.ToDictionary(q => q.Key, _ => new JevAnswer { Type = "noul", Noul = 0.2 }),
                Usage = new JevUsage { InputTokens = 100 },
            });
        }
    }

    /// <summary>A scan never rewrites; profiles are unavailable without an OpenAI key.</summary>
    private sealed class RefusingRewriteClient : IRewriteClient
    {
        public Task<RewriteResponse> RewriteAsync(RewriteRequest request, CancellationToken ct) =>
            throw new InvalidOperationException("No OpenAI call in this test.");
    }
}
