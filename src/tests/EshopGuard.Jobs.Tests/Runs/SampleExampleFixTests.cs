using EshopGuard.Jobs.Runs;
using EshopGuard.Jobs.Tests.Runs.Support;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>
/// The example fix of the free sample (design of change 8, task 7.3), with the fake rewrite model that leaves out generic
/// environmental words: a claim it can fix gives one proposal checked again as fixed; findings with no text to rewrite
/// (missing information of the site, claims waiting for the user's evidence) never call the rewrite model; a claim that stays
/// a finding after the rewrite gives no example and says so.
/// </summary>
public sealed class SampleExampleFixTests(JobsTestDatabase database) : JobsTestBase(database)
{
    [Fact]
    public async Task ClaimTheRewriteFixes_GivesOneExampleFix_CheckedAgainAsFixed()
    {
        var (shop, runId) = await SampleAsync("Tento výrobok je ekologický a šetrný k prírode.");

        var sample = (await RunTests.RunJsonAsync(Db, shop, runId, "stats"))["sample"]!.AsObject();
        Assert.Null(sample["example_fix_missing_reason"]);
        var proposalId = Guid.Parse((string)sample["example_fix_proposal_id"]!);
        var row = (await RunTests.RowsAsync(Db, shop.TenantId, "SELECT recheck_status, proposed_text FROM fixes.fix_proposals WHERE id = $1", proposalId)).Single();
        Assert.Equal("ok", row[0]);
        Assert.DoesNotContain("ekologick", (string)row[1]!, StringComparison.Ordinal);
        Assert.Equal(1L, await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM fixes.fix_proposals WHERE created_run_id = $1", runId));
    }

    [Fact]
    public async Task OnlyFindingsWithoutATextToRewrite_CallNoRewrite_AndSaySo()
    {
        var (shop, runId) = await SampleAsync(null);

        Assert.True(await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM checks.findings WHERE first_run_id = $1", runId) > 0, "findings of missing information");
        var sample = (await RunTests.RunJsonAsync(Db, shop, runId, "stats"))["sample"]!.AsObject();
        Assert.Null(sample["example_fix_proposal_id"]);
        Assert.Equal(RunCodes.NoRewritableFinding, (string?)sample["example_fix_missing_reason"]);
        Assert.Equal(0L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE run_id = $1 AND kind = 'run.rewrite'", runId));
        Assert.Equal(0L, await RunTests.ScalarAsync<long>(Db, shop.TenantId,
            "SELECT coalesce(sum(calls), 0)::bigint FROM usage.usage_records WHERE run_id = $1 AND operation = 'rewrite'", runId));
    }

    [Fact]
    public async Task ClaimThatStaysAFinding_GivesNoExample_AndSaysStillFinding()
    {
        // The fake model leaves out „ekologický“, „zelený“ stays: the check of the rewritten text finds the claim again.
        var (shop, runId) = await SampleAsync("Tento výrobok je ekologický a zelený.");

        var sample = (await RunTests.RunJsonAsync(Db, shop, runId, "stats"))["sample"]!.AsObject();
        Assert.Null(sample["example_fix_proposal_id"]);
        Assert.Equal(RunCodes.StillFinding, (string?)sample["example_fix_missing_reason"]);
        Assert.Equal(0L, await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM fixes.fix_proposals WHERE created_run_id = $1", runId));
    }

    private async Task<(RunShop Shop, Guid RunId)> SampleAsync(string? sharedSentence)
    {
        var shop = await RunTests.CreateShopAsync();
        var storage = Directory.CreateTempSubdirectory("eshopguard-example-fix-").FullName;
        var worker = await Workers.StartAsync("w0", RunTests.WorkerSettings(storage),
            RunTests.Services(new SyntheticShopFetcher(shop.BaseUrl, products: 30, sharedSentence), new DeterministicTestJevClient()));
        var runId = (await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFreeSampleAsync(shop.ShopId, null, Ct))).RunId!.Value;
        var status = await RunTests.WaitForStatusAsync(Db, shop, runId, TimeSpan.FromSeconds(120), [worker], RunTests.Final);
        Assert.True(status is "finished" or "partial", status);
        return (shop, runId);
    }
}
