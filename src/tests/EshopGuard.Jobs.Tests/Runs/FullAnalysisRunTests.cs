using EshopGuard.Jobs.Runs;
using EshopGuard.Jobs.Tests.Runs.Support;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>A full analysis of the Slovak fixture e-shop through the workers (design of change 8, task 13.2).</summary>
public sealed class FullAnalysisRunTests(JobsTestDatabase database) : JobsTestBase(database)
{
    [Fact]
    public async Task FullAnalysis_GoesThroughAllStates_AndWritesPagesVersionsAndFindings()
    {
        var shop = await RunTests.CreateShopAsync();
        var jev = new DeterministicTestJevClient();
        var storage = Directory.CreateTempSubdirectory("eshopguard-runs-").FullName;
        var workers = new List<TestWorker>();
        for (var i = 0; i < 3; i++)
        {
            workers.Add(await Workers.StartAsync("w" + i, RunTests.WorkerSettings(storage), RunTests.Services(RunTests.SlovakSite(shop.BaseUrl), jev)));
        }

        var created = await RunTests.ServiceAsync(workers[0].Host.Services, shop.TenantId, s => s.CreateFullAnalysisAsync(shop.ShopId, null, Ct));
        Assert.True(created.Succeeded, created.ErrorCode);
        var runId = created.RunId!.Value;
        await RunTests.WaitForStatusAsync(Db, shop, runId, TimeSpan.FromSeconds(30), workers, "awaiting_payment");

        var approved = await RunTests.ServiceAsync(workers[0].Host.Services, shop.TenantId, s => s.ApproveWithoutPaymentAsync(runId, Guid.CreateVersion7(), "pilot", Ct));
        Assert.True(approved.Succeeded, approved.ErrorCode);
        var status = await RunTests.WaitForStatusAsync(Db, shop, runId, TimeSpan.FromSeconds(90), workers, RunTests.Final);

        Assert.True(status is "finished" or "partial", status + "\n" + await Db.DumpJobsAsync());
        Assert.Equal(
            ["discovering", "awaiting_payment", "crawling", "profiling", "segmenting", "evaluating", "ruling", "rewriting", status],
            await RunTests.StatusTrailAsync(Db, shop, runId));
        var pages = await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM content.pages WHERE shop_id = $1", shop.ShopId);
        var versions = await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM content.page_versions WHERE shop_id = $1 AND run_id = $2 AND is_current", shop.ShopId, runId);
        var findings = await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM checks.findings WHERE shop_id = $1 AND first_run_id = $2", shop.ShopId, runId);
        Assert.True(pages > 5, $"pages {pages}");
        Assert.True(versions > 5, $"versions {versions}");
        Assert.True(findings > 0, $"findings {findings}");
        Assert.True(jev.Calls > 0);
    }
}
