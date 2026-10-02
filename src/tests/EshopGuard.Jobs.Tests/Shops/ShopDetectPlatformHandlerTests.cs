using System.Text.Json;
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

/// <summary>The job <c>shop.detect_platform</c> (change 10, task 2.7; AD 2).</summary>
public sealed class ShopDetectPlatformHandlerTests(JobsTestDatabase database) : JobsTestBase(database)
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Shoptet_IsRecognizedCertainly_AndTheFinalAddressIsStored()
    {
        var shop = await RunTests.CreateShopAsync();
        var home = shop.BaseUrl.AbsoluteUri;
        var fetcher = new ShopPageFetcher()
            .Redirect(home, home.Replace("http://", "http://www.", StringComparison.Ordinal))
            .Html(home.Replace("http://", "http://www.", StringComparison.Ordinal), Fixture("shoptet.html"));

        var detection = await DetectAsync(shop, fetcher);

        Assert.Equal("done", detection.GetProperty("status").GetString());
        Assert.Equal("shoptet", detection.GetProperty("platform").GetString());
        Assert.Equal("certain", detection.GetProperty("confidence").GetString());
        Assert.Equal(2, detection.GetProperty("signals").GetArrayLength());
        Assert.Equal("shoptet", await ShopAsync<string>(shop, "platform"));
        Assert.Equal(home.Replace("http://", "http://www.", StringComparison.Ordinal), await ShopAsync<string>(shop, "base_url"));
    }

    [Fact]
    public async Task SiteThatDoesNotAnswer_FailsWithTimeout()
    {
        var shop = await RunTests.CreateShopAsync();
        var fetcher = new ShopPageFetcher().Hang(shop.BaseUrl.AbsoluteUri);

        var detection = await DetectAsync(shop, fetcher);

        Assert.Equal("failed", detection.GetProperty("status").GetString());
        Assert.Equal("timeout", detection.GetProperty("failure_code").GetString());
        Assert.Equal("unknown", await ShopAsync<string>(shop, "platform"));
    }

    [Fact]
    public async Task RobotsForbiddingTheRoot_FailsWithRobotsBlocked_WithoutDownloadingTheHomePage()
    {
        var shop = await RunTests.CreateShopAsync();
        var fetcher = new ShopPageFetcher()
            .Text(new Uri(shop.BaseUrl, "/robots.txt").AbsoluteUri, "User-agent: *\nDisallow: /\n")
            .Html(shop.BaseUrl.AbsoluteUri, Fixture("shoptet.html"));

        var detection = await DetectAsync(shop, fetcher);

        Assert.Equal("failed", detection.GetProperty("status").GetString());
        Assert.Equal("robots_blocked", detection.GetProperty("failure_code").GetString());
        Assert.DoesNotContain(shop.BaseUrl.AbsoluteUri, fetcher.Requested);
    }

    [Fact]
    public async Task RedirectToAnotherDomain_IsOnlyReported_AndNotFollowed()
    {
        var shop = await RunTests.CreateShopAsync("bylinkovo-" + Guid.NewGuid().ToString("N")[..6] + ".cz");
        var other = $"http://{shop.Domain.Replace(".cz", ".sk", StringComparison.Ordinal)}/";
        var fetcher = new ShopPageFetcher().Redirect(shop.BaseUrl.AbsoluteUri, other).Html(other, Fixture("shoptet.html"));

        var detection = await DetectAsync(shop, fetcher);

        Assert.Equal("done", detection.GetProperty("status").GetString());
        Assert.Equal(new Uri(other).Host, detection.GetProperty("redirected_to").GetString());
        Assert.Equal("unknown", detection.GetProperty("platform").GetString());
        Assert.Equal(shop.BaseUrl.AbsoluteUri, await ShopAsync<string>(shop, "base_url"));
        Assert.DoesNotContain(other, fetcher.Requested);
    }

    [Fact]
    public async Task PlatformChosenByTheUser_IsKept()
    {
        var shop = await RunTests.CreateShopAsync();
        await RunTests.ScalarAsync<int>(Db, shop.TenantId,
            "WITH u AS (UPDATE shop.shops SET platform = 'woocommerce', detection = '{\"status\":\"done\",\"platform_source\":\"user\"}' WHERE id = $1 RETURNING 1) SELECT count(*)::int FROM u",
            shop.ShopId);
        var fetcher = new ShopPageFetcher().Html(shop.BaseUrl.AbsoluteUri, Fixture("shoptet.html"));

        var detection = await DetectAsync(shop, fetcher);

        Assert.Equal("shoptet", detection.GetProperty("platform").GetString());
        Assert.Equal("user", detection.GetProperty("platform_source").GetString());
        Assert.Equal("woocommerce", await ShopAsync<string>(shop, "platform"));
    }

    private async Task<JsonElement> DetectAsync(RunShop shop, IPageFetcher fetcher)
    {
        var worker = await Workers.StartAsync("detect", new Dictionary<string, string?>
        {
            ["Worker:Slots:Fetch"] = "2",
            ["Shops:Detection:TimeoutSeconds"] = "1",
        }, services =>
        {
            services.AddSingleton(fetcher);
            services.AddOptions<EshopGuardOptions>().Configure(o => o.Rules.PlatformsFile = Path.Combine(AppContext.BaseDirectory, "config", "platforms.yaml"));
            services.AddShopJobs();
        });

        await using (var scope = worker.Host.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(shop.TenantId);
            var db = scope.ServiceProvider.GetRequiredService<EshopGuardDb>();
            var queue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
            await db.ExecuteInTenantTransactionAsync(() =>
                queue.EnqueueAsync(ShopJobs.DetectPlatform(shop.TenantId, shop.ShopId), (Npgsql.NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction(), Ct), Ct);
        }

        var deadline = DateTime.UtcNow + Wait;
        while (DateTime.UtcNow < deadline)
        {
            var json = await ShopAsync<string?>(shop, "detection::text");
            if (json is not null && json.Contains("detected_at", StringComparison.Ordinal))
            {
                return JsonDocument.Parse(json).RootElement.Clone();
            }

            await Task.Delay(100, Ct);
        }

        throw new TimeoutException("the recognition did not finish: " + await Db.DumpJobsAsync());
    }

    private Task<T> ShopAsync<T>(RunShop shop, string column) =>
        RunTests.ScalarAsync<T>(Db, shop.TenantId, $"SELECT {column} FROM shop.shops WHERE id = $1", shop.ShopId);

    private static string Fixture(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "platforms", file));
}
