using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Tests.Runs.Support;
using Npgsql;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>
/// Findings of the worker (design of change 8, task 6.5): verdicts of every jurisdiction in one finding, one finding with all
/// its occurrences for a sentence repeated on many pages, and the state the user gave a finding kept by a later run.
/// </summary>
public sealed class FindingsWriteTests(JobsTestDatabase database) : JobsTestBase(database)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    [Fact]
    public async Task ShopSellingToSlovakiaAndCzechia_GetsFindingsWithTheVerdictsOfBoth()
    {
        var shop = await RunTests.CreateShopAsync();
        await AddMarketAsync(shop, "SK", home: true);
        await AddMarketAsync(shop, "CZ", home: false);
        var worker = await StartAsync(RunTests.SlovakSite(shop.BaseUrl));

        var runId = await RunFullAsync(worker, shop);

        Assert.Equal(["sk", "cz"], (await RunTests.RowsAsync(Db, shop.TenantId, "SELECT unnest(jurisdictions) FROM checks.runs WHERE id = $1", runId)).Select(r => (string)r[0]!));
        var both = await RunTests.ScalarAsync<long>(Db, shop.TenantId,
            """SELECT count(*) FROM checks.findings WHERE shop_id = $1 AND verdicts @> '[{"jurisdiction": "sk"}]' AND verdicts @> '[{"jurisdiction": "cz"}]'""", shop.ShopId);
        Assert.True(both > 0, "findings with the verdicts of sk and cz");
        var unmatched = await RunTests.ScalarAsync<long>(Db, shop.TenantId,
            "SELECT count(*) FROM checks.findings f WHERE shop_id = $1 AND NOT EXISTS (SELECT 1 FROM jsonb_array_elements(f.verdicts) v WHERE v ->> 'band' = f.band)", shop.ShopId);
        Assert.Equal(0L, unmatched);
    }

    [Fact]
    public async Task SentenceOn38Pages_IsOneFinding_With38Occurrences()
    {
        const string shared = "Tento výrobok je ekologický a šetrný k prírode.";
        var shop = await RunTests.CreateShopAsync();
        var worker = await StartAsync(new SyntheticShopFetcher(shop.BaseUrl, products: 38, shared));

        await RunFullAsync(worker, shop);

        var rows = await RunTests.RowsAsync(Db, shop.TenantId,
            "SELECT f.rule_id, f.occurrences, (SELECT count(DISTINCT o.page_id) FROM checks.finding_occurrences o WHERE o.finding_id = f.id) FROM checks.findings f WHERE f.shop_id = $1 AND f.text = $2",
            shop.ShopId, shared);
        Assert.NotEmpty(rows);
        Assert.Equal(rows.Count, rows.Select(r => (string)r[0]!).Distinct().Count());
        Assert.All(rows, r => Assert.Equal(38, Convert.ToInt32(r[1])));
        Assert.All(rows, r => Assert.Equal(38L, (long)r[2]!));
    }

    [Fact]
    public async Task FindingApprovedAfterTheSample_KeepsItsStateAndFirstRun_AfterTheFullAnalysis()
    {
        var shop = await RunTests.CreateShopAsync();
        var worker = await StartAsync(RunTests.SlovakSite(shop.BaseUrl));
        var sampleId = (await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFreeSampleAsync(shop.ShopId, null, Ct))).RunId!.Value;
        await RunTests.WaitForStatusAsync(Db, shop, sampleId, Timeout, [worker], RunTests.Final);
        var findingId = await RunTests.ScalarAsync<Guid>(Db, shop.TenantId, "SELECT id FROM checks.findings WHERE shop_id = $1 AND first_run_id = $2 ORDER BY id LIMIT 1", shop.ShopId, sampleId);
        await ExecuteAsync(shop, "UPDATE checks.findings SET status = 'approved' WHERE id = $1", findingId);

        var fullId = await RunFullAsync(worker, shop);

        var row = (await RunTests.RowsAsync(Db, shop.TenantId, "SELECT status, first_run_id, last_seen_run_id FROM checks.findings WHERE id = $1", findingId)).Single();
        Assert.Equal(["approved", sampleId, fullId], row);
    }

    private async Task<TestWorker> StartAsync(Core.Crawl.IPageFetcher fetcher)
    {
        var storage = Directory.CreateTempSubdirectory("eshopguard-findings-").FullName;
        return await Workers.StartAsync("w0", RunTests.WorkerSettings(storage), RunTests.Services(fetcher, new DeterministicTestJevClient()));
    }

    private async Task<Guid> RunFullAsync(TestWorker worker, RunShop shop)
    {
        var runId = (await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFullAnalysisAsync(shop.ShopId, null, Ct))).RunId!.Value;
        await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], "awaiting_payment");
        await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.ApproveWithoutPaymentAsync(runId, Guid.CreateVersion7(), "test", Ct));
        var status = await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], RunTests.Final);
        Assert.True(status is "finished" or "partial", status);
        return runId;
    }

    private Task AddMarketAsync(RunShop shop, string country, bool home) =>
        ExecuteAsync(shop,
            "INSERT INTO shop.shop_markets (tenant_id, shop_id, country_code, is_home, status, source, created_at, updated_at) VALUES ($1, $2, $3, $4, 'active', 'user', now(), now())",
            shop.TenantId, shop.ShopId, country, home);

    private async Task ExecuteAsync(RunShop shop, string sql, params object[] parameters)
    {
        await using var connection = await Db.For("Owner").OpenConnectionAsync(Ct);
        await using var transaction = await TenantSql.BeginAsync(connection, shop.TenantId, ct: Ct);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = parameter });
        }

        await command.ExecuteNonQueryAsync(Ct);
        await transaction.CommitAsync(Ct);
    }
}
