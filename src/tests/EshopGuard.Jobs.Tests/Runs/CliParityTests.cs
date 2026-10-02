using EshopGuard.Core;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Pipeline;
using EshopGuard.Jobs.Runs.Handlers;
using EshopGuard.Jobs.Tests.Runs.Support;
using EshopGuard.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>
/// The worker and the CLI's runner (<c>InMemoryPipelineRunner</c>) over the same fixture e-shop with the same Jev give the same
/// findings as sets (design of change 8, decision 10, task 13.4; a difference names the rule, the text and the pages) and the
/// usage of the run equals the statistics of the CLI (task 9.3).
/// </summary>
public sealed class CliParityTests(JobsTestDatabase database) : JobsTestBase(database)
{
    [Theory]
    [InlineData("site-sk")]
    [InlineData("site")]
    public async Task WorkerAndCli_FindTheSame(string fixture)
    {
        var shop = await RunTests.CreateShopAsync(homeCountry: fixture == "site" ? "cz" : "sk");
        var storage = Directory.CreateTempSubdirectory("eshopguard-parity-").FullName;
        var fetcher = fixture == "site" ? RunTests.CzechSite(shop.BaseUrl) : RunTests.SlovakSite(shop.BaseUrl);
        var workers = new List<TestWorker>
        {
            await Workers.StartAsync("w0", RunTests.WorkerSettings(storage), RunTests.Services(fetcher, new DeterministicTestJevClient())),
            await Workers.StartAsync("w1", RunTests.WorkerSettings(storage), RunTests.Services(fetcher, new DeterministicTestJevClient())),
        };
        var created = await RunTests.ServiceAsync(workers[0].Host.Services, shop.TenantId, s => s.CreateFullAnalysisAsync(shop.ShopId, null, Ct));
        var runId = created.RunId!.Value;
        await RunTests.WaitForStatusAsync(Db, shop, runId, TimeSpan.FromSeconds(30), workers, "awaiting_payment");
        await RunTests.ServiceAsync(workers[0].Host.Services, shop.TenantId, s => s.ApproveWithoutPaymentAsync(runId, Guid.CreateVersion7(), "parity", Ct));
        await RunTests.WaitForStatusAsync(Db, shop, runId, TimeSpan.FromSeconds(90), workers, RunTests.Final);
        var worker = await ReadResultAsync(storage, shop, runId);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Core.Crawl.IPageFetcher>(fixture == "site" ? RunTests.CzechSite(shop.BaseUrl) : RunTests.SlovakSite(shop.BaseUrl));
        services.AddSingleton<Core.Jev.IJevClient>(new DeterministicTestJevClient());
        services.AddEshopGuard(RunTests.Configure);
        await using var provider = services.BuildServiceProvider();
        var cli = await provider.GetRequiredService<IEshopGuard>().ScanSiteAsync(shop.BaseUrl,
            new ScanOptions { MaxPages = 200, SampleProducts = 100, Country = fixture == "site" ? "cz" : "sk" }, null, Ct);

        Assert.True(cli.Findings.Count > 0);
        Assert.Equal(Describe(cli.Findings), Describe(worker.Findings));
        Assert.Equal(cli.Stats.PagesFetched, worker.Stats.PagesFetched);
        Assert.Equal(cli.Stats.UniqueSegments, worker.Stats.UniqueSegments);
        Assert.Equal(cli.Stats.JevCalls + cli.Stats.JevCacheHits, worker.Stats.JevCalls + worker.Stats.JevCacheHits);
        Assert.Equal(cli.Warnings.Select(w => w.Code).Order(), worker.Warnings.Select(w => w.Code).Order());

        // Usage of the run (task 9.3): the calls and tokens of Jev equal the statistics of the CLI exactly.
        var usage = (await RunTests.RowsAsync(Db, shop.TenantId,
                "SELECT operation, sum(calls)::bigint, sum(input_tokens)::bigint FROM usage.usage_records WHERE run_id = $1 AND provider = 'jev' GROUP BY operation", runId))
            .ToDictionary(r => (string)r[0]!, r => (Calls: (long)r[1]!, Tokens: (long)r[2]!));
        Assert.Equal(cli.Stats.JevCalls, usage.GetValueOrDefault("sentence_eval").Calls);
        Assert.Equal(cli.Stats.SieveCalls, usage.GetValueOrDefault("sieve").Calls);
        Assert.Equal(cli.Stats.InputTokens, usage.GetValueOrDefault("sentence_eval").Tokens + usage.GetValueOrDefault("sieve").Tokens);
    }

    /// <summary>The result of the run as the worker stored it.</summary>
    private static async Task<ScanResult> ReadResultAsync(string storage, RunShop shop, Guid runId)
    {
        var blobs = new FileSystemBlobStore(storage);
        var scope = new Jobs.Runs.RunAmbientScope(shop.TenantId, shop.ShopId, runId, 0);
        var text = await Jobs.Runs.Storage.RunFiles.GetTextAsync(blobs, Jobs.Runs.Storage.RunFiles.Key(scope, RulesHandler.ResultFolder, RulesHandler.ResultFile + ".json.gz"), Ct);
        Assert.NotNull(text);
        return System.Text.Json.JsonSerializer.Deserialize<ScanResult>(text, PipelineJson.Options)!;
    }

    /// <summary>Findings as comparable lines: rule, scope, text, pages and the verdicts by jurisdiction.</summary>
    private static List<string> Describe(IEnumerable<Finding> findings) =>
        findings.Select(f => string.Join(" | ",
                f.RuleId, f.Scope, f.Text ?? "", string.Join(",", f.Urls.Order(StringComparer.Ordinal)),
                string.Join(",", f.Verdicts.Select(v => $"{v.Jurisdiction}:{v.Checkability}:{v.Severity}:{v.Band}:{v.Score:0.0000}"))))
            .Order(StringComparer.Ordinal)
            .ToList();
}
