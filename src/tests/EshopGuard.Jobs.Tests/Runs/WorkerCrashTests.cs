using EshopGuard.Core.Fix;
using EshopGuard.Core.Jev;
using EshopGuard.Jobs.Runs;
using EshopGuard.Jobs.Tests.Runs.Support;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>
/// A worker killed in the middle of a batch of downloads, of Jev or of rewrites (design of change 8, task 13.3): its lease
/// expires, another worker repeats the batch and the run ends with the same pages, findings and proposals as without the crash,
/// nothing written twice.
/// </summary>
public sealed class WorkerCrashTests(JobsTestDatabase database) : JobsTestBase(database)
{
    [Theory]
    [InlineData(RunJobKinds.Fetch)]
    [InlineData(RunJobKinds.Evaluate)]
    [InlineData(RunJobKinds.Rewrite)]
    public async Task CrashInABatch_TheRunEndsWithTheSameResultAsWithoutIt(string kind)
    {
        var reference = await RunAsync(crashIn: null);
        var crashed = await RunAsync(crashIn: kind);

        Assert.Equal(reference.Pages, crashed.Pages);
        Assert.Equal(reference.Versions, crashed.Versions);
        Assert.Equal(reference.Findings, crashed.Findings);
        Assert.Equal(reference.Occurrences, crashed.Occurrences);
        Assert.Equal(reference.Proposals, crashed.Proposals);
    }

    private async Task<(long Pages, long Versions, long Findings, long Occurrences, long Proposals)> RunAsync(string? crashIn)
    {
        var shop = await RunTests.CreateShopAsync();
        var storage = Directory.CreateTempSubdirectory("eshopguard-crash-").FullName;
        var fetcher = new SlowFetcher(RunTests.SlovakSite(shop.BaseUrl), TimeSpan.FromMilliseconds(100));
        var settings = RunTests.WorkerSettings(storage, slots: 1);
        settings["Runs:FetchBatchPages"] = "4";
        settings["Runs:EvaluateBatchSegments"] = "20";
        settings["Scheduler:Enabled"] = "true";
        Action<IServiceCollection> services = s =>
        {
            // Slow services keep every batch running long enough to be caught in the middle.
            s.AddSingleton<IRewriteClient>(new SlowRewriteClient(TimeSpan.FromMilliseconds(300)));
            RunTests.Services(fetcher, new SlowJevClient(TimeSpan.FromMilliseconds(20)))(s);
        };
        var workers = new List<TestWorker>
        {
            await Workers.StartAsync("a", settings, services),
            await Workers.StartAsync("b", settings, services),
        };
        var runId = (await RunTests.ServiceAsync(workers[0].Host.Services, shop.TenantId, s => s.CreateFullAnalysisAsync(shop.ShopId, null, Ct))).RunId!.Value;
        await RunTests.WaitForStatusAsync(Db, shop, runId, TimeSpan.FromSeconds(30), workers, "awaiting_payment");
        await RunTests.ServiceAsync(workers[0].Host.Services, shop.TenantId, s => s.ApproveWithoutPaymentAsync(runId, Guid.CreateVersion7(), "test", Ct));
        if (crashIn is not null)
        {
            string? owner = null;
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
            while (owner is null)
            {
                Assert.True(DateTime.UtcNow < deadline, $"no running {crashIn} job");
                owner = await Db.ScalarAsync<string>("SELECT lease_owner FROM ops.jobs WHERE run_id = $1 AND kind = $2 AND state = 'running' LIMIT 1", runId, crashIn);
                await Task.Delay(10, Ct);
            }

            await workers.Single(w => w.Id == owner).CrashAsync();
        }

        var status = await RunTests.WaitForStatusAsync(Db, shop, runId, TimeSpan.FromSeconds(120), workers, RunTests.Final);
        Assert.True(status is "finished" or "partial", status);
        foreach (var worker in workers)
        {
            await worker.StopAsync();
        }

        return (
            await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM content.pages WHERE shop_id = $1", shop.ShopId),
            await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM content.page_versions WHERE shop_id = $1", shop.ShopId),
            await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM checks.findings WHERE shop_id = $1", shop.ShopId),
            await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM checks.finding_occurrences WHERE shop_id = $1", shop.ShopId),
            await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM fixes.fix_proposals WHERE shop_id = $1", shop.ShopId));
    }
}

/// <summary>The Jev of the tests, each call after a delay.</summary>
internal sealed class SlowJevClient(TimeSpan delay) : IJevClient
{
    private readonly DeterministicTestJevClient _inner = new();

    public async Task<JevResult> EvaluateAsync(object state, IReadOnlyDictionary<string, JevQuestion> questions, CancellationToken ct)
    {
        await Task.Delay(delay, ct);
        return await _inner.EvaluateAsync(state, questions, ct);
    }
}

/// <summary>The mock of rewrites, each call after a delay.</summary>
internal sealed class SlowRewriteClient(TimeSpan delay) : IRewriteClient
{
    private readonly MockRewriteClient _inner = new();

    public async Task<RewriteResponse> RewriteAsync(RewriteRequest request, CancellationToken ct)
    {
        await Task.Delay(delay, ct);
        return await _inner.RewriteAsync(request, ct);
    }
}
