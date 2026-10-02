using EshopGuard.Jobs.Runs;
using EshopGuard.Jobs.Tests.Runs.Support;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>The free sample through the workers (design of change 8, tasks 3.4, 6.x, 8.1).</summary>
public sealed class FreeSampleRunTests(JobsTestDatabase database) : JobsTestBase(database)
{
    [Fact]
    public async Task FreeSample_RunsWithoutPayment_AndStoresItsSummaryAndScopeBasis()
    {
        var shop = await RunTests.CreateShopAsync();
        var storage = Directory.CreateTempSubdirectory("eshopguard-sample-").FullName;
        var workers = new List<TestWorker>
        {
            await Workers.StartAsync("w0", RunTests.WorkerSettings(storage), RunTests.Services(RunTests.SlovakSite(shop.BaseUrl), new DeterministicTestJevClient())),
        };

        var created = await RunTests.ServiceAsync(workers[0].Host.Services, shop.TenantId, s => s.CreateFreeSampleAsync(shop.ShopId, null, Ct));
        Assert.True(created.Succeeded, created.ErrorCode);
        var runId = created.RunId!.Value;
        var status = await RunTests.WaitForStatusAsync(Db, shop, runId, TimeSpan.FromSeconds(90), workers, RunTests.Final);

        Assert.True(status is "finished" or "partial", status);
        Assert.Equal(
            ["discovering", "crawling", "profiling", "segmenting", "evaluating", "ruling", "rewriting", status],
            await RunTests.StatusTrailAsync(Db, shop, runId));
        var stats = await RunTests.RunJsonAsync(Db, shop, runId, "stats");
        var sample = stats["sample"]!.AsObject();
        Assert.NotEmpty(sample["top_finding_ids"]!.AsArray());
        Assert.True(sample["findings_by_severity"]!.AsObject().Count > 0);
        Assert.True(sample["example_fix_proposal_id"] is not null || sample["example_fix_missing_reason"] is not null);
        var estimate = await RunTests.RunJsonAsync(Db, shop, runId, "estimate");
        Assert.Equal(runId.ToString("D"), (string)estimate["basis"]!["source_run_id"]!);
        Assert.NotNull(estimate["internal"]!["total_usd"]);
        var markets = await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM shop.shop_markets WHERE shop_id = $1 AND detection_run_id = $2", shop.ShopId, runId);
        var languages = await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM shop.shop_languages WHERE shop_id = $1 AND sample_run_id = $2", shop.ShopId, runId);
        Assert.True(languages > 0, "shop_languages");
        Assert.True(markets >= 0);
        var planned = await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM checks.run_urls WHERE run_id = $1 AND state = 'extracted'", runId);
        Assert.InRange(planned, 1, 101);
    }

    [Fact]
    public async Task SecondSample_ForTheSameDomain_IsRefused_WithoutRevealingTheOtherTenant()
    {
        var first = await RunTests.CreateShopAsync();
        var second = await RunTests.CreateShopAsync("www." + first.Domain);
        var storage = Directory.CreateTempSubdirectory("eshopguard-sample-").FullName;
        var worker = await Workers.StartAsync("w0", new Dictionary<string, string?>(RunTests.WorkerSettings(storage, slots: 0)),
            RunTests.Services(RunTests.SlovakSite(first.BaseUrl), new DeterministicTestJevClient()));

        var claimed = await RunTests.ServiceAsync(worker.Host.Services, first.TenantId, s => s.CreateFreeSampleAsync(first.ShopId, null, Ct));
        var refused = await RunTests.ServiceAsync(worker.Host.Services, second.TenantId, s => s.CreateFreeSampleAsync(second.ShopId, null, Ct));

        Assert.True(claimed.Succeeded);
        Assert.Equal(RunCodes.SampleAlreadyUsed, refused.ErrorCode);
        Assert.Null(refused.RunId);
        Assert.Equal(0L, await RunTests.ScalarAsync<long>(Db, second.TenantId, "SELECT count(*) FROM checks.runs WHERE shop_id = $1", second.ShopId));
        Assert.Equal(0L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE tenant_id = $1", second.TenantId));
    }
}
