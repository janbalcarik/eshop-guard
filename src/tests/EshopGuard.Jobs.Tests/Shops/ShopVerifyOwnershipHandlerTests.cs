using EshopGuard.Core.Crawl;
using EshopGuard.Core.Options;
using EshopGuard.Data;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Shops;
using EshopGuard.Jobs.Tests.Runs.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Jobs.Tests.Shops;

/// <summary>The job <c>shop.verify_ownership</c> (change 10, task 9.6; AD 9).</summary>
public sealed class ShopVerifyOwnershipHandlerTests(JobsTestDatabase database) : JobsTestBase(database)
{
    private const string Token = "AbCdEfGhIjKlMnOpQrStUv";

    [Fact]
    public async Task MetaTagWithTheToken_VerifiesTheShop_AndIsAudited()
    {
        var shop = await RunTests.CreateShopAsync();
        var fetcher = new ShopPageFetcher().Html(shop.BaseUrl.AbsoluteUri, Page(OwnershipTokens.MetaTag(Token)));

        var (status, failure) = await CheckAsync(shop, "meta", fetcher, new FakeDns());

        Assert.Equal(("verified", (string?)null), (status, failure));
        Assert.Equal("meta", await RunTests.ScalarAsync<string>(Db, shop.TenantId, "SELECT verification_method FROM shop.shops WHERE id = $1", shop.ShopId));
        Assert.Equal(1L, await RunTests.ScalarAsync<long>(Db, shop.TenantId,
            "SELECT count(*) FROM ops.audit_log WHERE action = 'ownership.verified' AND entity_id = $1", shop.ShopId.ToString("D")));
        Assert.All(Workers.All.SelectMany(w => w.Logs.Logs), l =>
        {
            Assert.DoesNotContain(Token, l.AllText, StringComparison.Ordinal);
            Assert.DoesNotContain("<meta", l.AllText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task MissingMetaTag_FailsWithMetaNotFound()
    {
        var shop = await RunTests.CreateShopAsync();
        var fetcher = new ShopPageFetcher().Html(shop.BaseUrl.AbsoluteUri, Page("") + $"<p>{OwnershipTokens.MetaName} {Token}</p>");

        var (status, failure) = await CheckAsync(shop, "meta", fetcher, new FakeDns());

        Assert.Equal(("failed", "meta_not_found"), (status, failure));
        Assert.Null(await RunTests.ScalarAsync<DateTime?>(Db, shop.TenantId, "SELECT ownership_verified_at FROM shop.shops WHERE id = $1", shop.ShopId));
    }

    [Fact]
    public async Task MetaTagWithAnotherToken_FailsWithTokenMismatch()
    {
        var shop = await RunTests.CreateShopAsync();
        var fetcher = new ShopPageFetcher().Html(shop.BaseUrl.AbsoluteUri, Page(OwnershipTokens.MetaTag("another-token-000000000")));

        var (status, failure) = await CheckAsync(shop, "meta", fetcher, new FakeDns());

        Assert.Equal(("failed", "token_mismatch"), (status, failure));
    }

    [Fact]
    public async Task TxtRecordWithTheToken_VerifiesTheShop()
    {
        var shop = await RunTests.CreateShopAsync();
        var dns = new FakeDns { [OwnershipTokens.DnsName(shop.Domain)] = ["v=spf1 -all", OwnershipTokens.DnsValue(Token)] };

        var (status, failure) = await CheckAsync(shop, "dns", new ShopPageFetcher(), dns);

        Assert.Equal(("verified", (string?)null), (status, failure));
        Assert.Equal("dns", await RunTests.ScalarAsync<string>(Db, shop.TenantId, "SELECT verification_method FROM shop.shops WHERE id = $1", shop.ShopId));
    }

    [Fact]
    public async Task NameWithoutARecord_FailsWithDnsRecordNotFound()
    {
        var shop = await RunTests.CreateShopAsync();

        var (status, failure) = await CheckAsync(shop, "dns", new ShopPageFetcher(), new FakeDns());

        Assert.Equal(("failed", "dns_record_not_found"), (status, failure));
    }

    private static string Page(string head) => $"<!DOCTYPE html><html><head><title>Obchod</title>{head}</head><body><h1>Obchod</h1></body></html>";

    private async Task<(string Status, string? Failure)> CheckAsync(RunShop shop, string method, IPageFetcher fetcher, IDnsTxtResolver dns)
    {
        var verificationId = Guid.CreateVersion7();
        await RunTests.ScalarAsync<int>(Db, shop.TenantId,
            "WITH v AS (INSERT INTO shop.shop_verifications (id, tenant_id, shop_id, method, token, status, created_at, updated_at) VALUES ($1, $2, $3, $4, $5, 'pending', now(), now()) RETURNING 1) SELECT count(*)::int FROM v",
            verificationId, shop.TenantId, shop.ShopId, method, Token);
        var worker = await Workers.StartAsync("verify", new Dictionary<string, string?> { ["Worker:Slots:Fetch"] = "2", ["Shops:Detection:TimeoutSeconds"] = "2" }, services =>
        {
            services.AddSingleton(fetcher);
            services.AddSingleton(dns);
            services.AddOptions<EshopGuardOptions>();
            services.AddShopJobs();
        });

        await using (var scope = worker.Host.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(shop.TenantId);
            var db = scope.ServiceProvider.GetRequiredService<EshopGuardDb>();
            var queue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
            await db.ExecuteInTenantTransactionAsync(() => queue.EnqueueAsync(ShopJobs.VerifyOwnership(shop.TenantId, shop.ShopId, verificationId),
                (Npgsql.NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction(), Ct), Ct);
        }

        for (var i = 0; i < 300; i++)
        {
            var rows = await RunTests.RowsAsync(Db, shop.TenantId, "SELECT status, failure_code FROM shop.shop_verifications WHERE id = $1", verificationId);
            if ((string)rows[0][0]! != "pending")
            {
                return ((string)rows[0][0]!, (string?)rows[0][1]);
            }

            await Task.Delay(100, Ct);
        }

        throw new TimeoutException(await Db.DumpJobsAsync());
    }

    private sealed class FakeDns : Dictionary<string, string[]>, IDnsTxtResolver
    {
        public Task<IReadOnlyList<string>> TxtAsync(string name, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>(TryGetValue(name, out var records) ? records : []);
    }
}
