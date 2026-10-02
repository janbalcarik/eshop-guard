using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Runs;
using EshopGuard.Jobs.Tests.Runs.Support;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>Payment, approval, ownership and cancellation of runs (design of change 8, tasks 2.3, 2.5, 2.10, 11.4, 11.8).</summary>
public sealed class RunServiceTests(JobsTestDatabase database) : JobsTestBase(database)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task PaidOrder_MovesTheRunToCrawling_AndASecondCallChangesNothing()
    {
        var (shop, worker, runId) = await AwaitingPaymentAsync();
        var order = await CreateOrderAsync(shop, runId, OrderStatus.Paid);

        var first = await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.MarkOrderPaidAsync(order, Ct));
        var second = await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.MarkOrderPaidAsync(order, Ct));

        Assert.True(first.Succeeded && second.Succeeded, first.ErrorCode ?? second.ErrorCode);
        await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], RunTests.Final);
        var trail = await RunTests.StatusTrailAsync(Db, shop, runId);
        Assert.Single(trail, s => s == "crawling");
        Assert.Equal(1L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE run_id = $1 AND kind = 'run.fetch' AND dedupe_key LIKE '%:fetch:0:1'", runId));
    }

    [Fact]
    public async Task UnpaidOrder_IsRefused()
    {
        var (shop, worker, runId) = await AwaitingPaymentAsync();
        var order = await CreateOrderAsync(shop, runId, OrderStatus.CheckoutOpen);

        var result = await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.MarkOrderPaidAsync(order, Ct));

        Assert.Equal(RunCodes.OrderNotPaid, result.ErrorCode);
        Assert.Equal("awaiting_payment", await RunTests.ScalarAsync<string>(Db, shop.TenantId, "SELECT status FROM checks.runs WHERE id = $1", runId));
    }

    [Fact]
    public async Task ApprovalBeforeTheEndOfDiscovery_IsNotLost_AndIsAudited()
    {
        var shop = await RunTests.CreateShopAsync();
        var storage = Directory.CreateTempSubdirectory("eshopguard-pay-").FullName;
        var idle = await Workers.StartAsync("idle", RunTests.WorkerSettings(storage, slots: 0), RunTests.Services(RunTests.SlovakSite(shop.BaseUrl), new DeterministicTestJevClient()));
        var runId = (await RunTests.ServiceAsync(idle.Host.Services, shop.TenantId, s => s.CreateFullAnalysisAsync(shop.ShopId, null, Ct))).RunId!.Value;
        var admin = Guid.CreateVersion7();

        var approved = await RunTests.ServiceAsync(idle.Host.Services, shop.TenantId, s => s.ApproveWithoutPaymentAsync(runId, admin, "pilot", Ct));
        var worker = await Workers.StartAsync("w0", RunTests.WorkerSettings(storage), RunTests.Services(RunTests.SlovakSite(shop.BaseUrl), new DeterministicTestJevClient()));
        var status = await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], RunTests.Final);

        Assert.True(approved.Succeeded, approved.ErrorCode);
        Assert.True(status is "finished" or "partial", status);
        Assert.Equal(["discovering", "awaiting_payment", "crawling"], (await RunTests.StatusTrailAsync(Db, shop, runId)).Take(3));
        Assert.Equal(1L, await RunTests.ScalarAsync<long>(Db, shop.TenantId,
            "SELECT count(*) FROM ops.audit_log WHERE action = 'run.approved_without_payment' AND entity_id = $1 AND actor_user_id = $2", runId.ToString("D"), admin));
    }

    [Fact]
    public async Task FullAnalysis_WithoutVerifiedOwnership_CreatesNothing()
    {
        var shop = await RunTests.CreateShopAsync();
        var storage = Directory.CreateTempSubdirectory("eshopguard-owner-").FullName;
        var worker = await Workers.StartAsync("w0", RunTests.WorkerSettings(storage, slots: 0), services =>
        {
            RunTests.Services(RunTests.SlovakSite(shop.BaseUrl), new DeterministicTestJevClient())(services);
            services.AddSingleton<IShopOwnershipPolicy, DenyAllOwnershipPolicy>();
        });

        var result = await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFullAnalysisAsync(shop.ShopId, null, Ct));

        Assert.Equal(RunCodes.OwnershipNotVerified, result.ErrorCode);
        Assert.Equal(0L, await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM checks.runs WHERE shop_id = $1", shop.ShopId));
    }

    [Fact]
    public async Task SecondFullAnalysis_WhileOneIsActive_IsRefused()
    {
        var (shop, worker, _) = await AwaitingPaymentAsync();

        var second = await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFullAnalysisAsync(shop.ShopId, null, Ct));

        Assert.Equal(RunCodes.RunAlreadyActive, second.ErrorCode);
    }

    [Fact]
    public async Task Cancel_OfAWaitingRun_EndsItAtOnce()
    {
        var (shop, worker, runId) = await AwaitingPaymentAsync();

        var canceled = await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.RequestCancelAsync(runId, null, Ct));
        var again = await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.RequestCancelAsync(runId, null, Ct));

        Assert.True(canceled.Succeeded, canceled.ErrorCode);
        Assert.Equal(RunCodes.AlreadyFinished, again.ErrorCode);
        Assert.Equal("canceled", await RunTests.ScalarAsync<string>(Db, shop.TenantId, "SELECT status FROM checks.runs WHERE id = $1", runId));
        Assert.Equal(1L, await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM checks.run_events WHERE run_id = $1 AND code = 'run.canceled'", runId));
    }

    [Fact]
    public async Task Cancel_DuringTheCrawl_StopsTheDownloads_AndKeepsWhatWasDownloaded()
    {
        var shop = await RunTests.CreateShopAsync();
        var storage = Directory.CreateTempSubdirectory("eshopguard-cancel-").FullName;
        var fetcher = new SlowFetcher(RunTests.SlovakSite(shop.BaseUrl), TimeSpan.FromMilliseconds(150));
        var settings = RunTests.WorkerSettings(storage);
        settings["Runs:FetchBatchPages"] = "2";
        var worker = await Workers.StartAsync("w0", settings, RunTests.Services(fetcher, new DeterministicTestJevClient()));
        var runId = (await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFullAnalysisAsync(shop.ShopId, null, Ct))).RunId!.Value;
        await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], "awaiting_payment");
        await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.ApproveWithoutPaymentAsync(runId, Guid.CreateVersion7(), "pilot", Ct));
        await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], "crawling");
        while (await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM checks.run_urls WHERE run_id = $1", runId) == 0)
        {
            await Task.Delay(50, Ct);
        }

        await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.RequestCancelAsync(runId, null, Ct));
        await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], RunTests.Final);
        var requests = fetcher.Requests;
        await Task.Delay(1_000, Ct);

        Assert.Equal("canceled", await RunTests.ScalarAsync<string>(Db, shop.TenantId, "SELECT status FROM checks.runs WHERE id = $1", runId));
        Assert.Equal(requests, fetcher.Requests);
        Assert.True(await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM content.pages WHERE shop_id = $1", shop.ShopId) > 0);
        Assert.Equal(0L, await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM checks.findings WHERE shop_id = $1", shop.ShopId));
        Assert.Equal(0L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE run_id = $1 AND state IN ('queued', 'running')", runId));
    }

    /// <summary>A full analysis that waits for its payment, with one worker running.</summary>
    private async Task<(RunShop Shop, TestWorker Worker, Guid RunId)> AwaitingPaymentAsync()
    {
        var shop = await RunTests.CreateShopAsync();
        var storage = Directory.CreateTempSubdirectory("eshopguard-pay-").FullName;
        var worker = await Workers.StartAsync("w0", RunTests.WorkerSettings(storage), RunTests.Services(RunTests.SlovakSite(shop.BaseUrl), new DeterministicTestJevClient()));
        var created = await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFullAnalysisAsync(shop.ShopId, null, Ct));
        Assert.True(created.Succeeded, created.ErrorCode);
        await RunTests.WaitForStatusAsync(Db, shop, created.RunId!.Value, Timeout, [worker], "awaiting_payment");
        return (shop, worker, created.RunId.Value);
    }

    private async Task<Guid> CreateOrderAsync(RunShop shop, Guid runId, OrderStatus status)
    {
        var priceList = Guid.CreateVersion7();
        await Db.ExecuteAsync("Owner", "INSERT INTO billing.price_lists (id, name, market_code, currency, valid_from, status, notice_days, created_at, updated_at) VALUES ($1, 'test', 'sk', 'EUR', now(), 'published', 30, now(), now())", priceList);
        var context = new TenantContext();
        context.Set(shop.TenantId);
        await using var db = JobsTestDatabase.CreateDb("App", context);
        var order = new Order
        {
            ShopId = shop.ShopId, Kind = OrderKind.AnalysisWithTrial, PriceListId = priceList, AmountNet = 100m, VatRate = 23m, VatAmount = 23m, AmountGross = 123m,
            Currency = "EUR", Status = status, RunId = runId,
        };
        db.Add(order);
        await db.ExecuteInTenantTransactionAsync(() => db.SaveChangesAsync(Ct), Ct);
        return order.Id;
    }
}

/// <summary>A fetcher that answers slowly and counts the requests.</summary>
internal sealed class SlowFetcher(Core.Crawl.IPageFetcher inner, TimeSpan delay) : Core.Crawl.IPageFetcher
{
    private int _requests;

    public int Requests => Volatile.Read(ref _requests);

    public async Task<Core.Crawl.FetchResponse> FetchAsync(Uri url, CancellationToken ct)
    {
        Interlocked.Increment(ref _requests);
        await Task.Delay(delay, ct);
        return await inner.FetchAsync(url, ct);
    }

    public async Task<Core.Crawl.FetchResponse> FetchAsync(Core.Crawl.FetchRequest request, CancellationToken ct)
    {
        Interlocked.Increment(ref _requests);
        await Task.Delay(delay, ct);
        return await inner.FetchAsync(request, ct);
    }
}
