using System.Diagnostics;
using System.Net;

namespace EshopGuard.Api.Tests.Shops;

/// <summary>
/// The recognition of the platform through the API (change 10, task 2.8; specification „Rozpoznání platformy z adresy“).
/// No worker runs in these tests: the job is completed by hand, as the worker would complete it.
/// </summary>
public sealed class DetectionTests : ShopTestBase
{
    private const string ShoptetResult =
        """{"status":"done","platform":"shoptet","confidence":"certain","signals":["shoptet.cdn_host","shoptet.web_author"],"detected_at":"2026-10-02T12:00:00Z","platform_source":"detected"}""";

    [Fact]
    public async Task CreatedShop_WaitsForTheJob_AndAnswersWithTheResult()
    {
        await using var factory = Factory(settings: new Dictionary<string, string?> { ["Api:InteractiveWaitSeconds"] = "8" });
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain();
        var watch = Stopwatch.StartNew();

        var create = owner.Browser.PostAsync($"/api/t/{owner.TenantId}/shops", new { url = "https://" + domain });
        await CompleteJobAsync(owner.TenantId, domain, ShoptetResult, "shoptet");
        using var response = await create;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(8), watch.Elapsed.ToString());
        var detection = (await ApiClient.JsonAsync(response)).GetProperty("detection");
        Assert.Equal("done", detection.GetProperty("status").GetString());
        Assert.Equal("shoptet", detection.GetProperty("platform").GetString());
        Assert.Equal("certain", detection.GetProperty("confidence").GetString());
        Assert.False(detection.GetProperty("connector").GetProperty("available").GetBoolean());
        Assert.Equal("web", detection.GetProperty("recommendedSource").GetString());
    }

    [Fact]
    public async Task SlowJob_IsPending_AndALaterGetHasTheResult()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain();

        var shop = await CreateShopAsync(owner, "https://" + domain);
        Assert.Equal("pending", shop.GetProperty("detection").GetProperty("status").GetString());
        await CompleteJobAsync(owner.TenantId, domain, ShoptetResult, "shoptet");
        var detection = await GetJsonAsync(owner, $"/api/t/{owner.TenantId}/shops/{shop.GetProperty("id").GetGuid()}/detection");

        Assert.Equal("done", detection.GetProperty("status").GetString());
        Assert.Equal("shoptet", detection.GetProperty("platform").GetString());
    }

    [Fact]
    public async Task SecondRecognitionWhileOneRuns_Is409_AndAfterItEndsAnotherOneIsEnqueued()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain();
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        var path = $"/api/t/{owner.TenantId}/shops/{shopId}/detection";

        using var busy = await owner.Browser.PostAsync(path);
        await CompleteJobAsync(owner.TenantId, domain, ShoptetResult, "shoptet");
        using var again = await owner.Browser.PostAsync(path);

        Assert.Equal((HttpStatusCode.Conflict, "detection.in_progress"), ((await ApiClient.ProblemAsync(busy)).Status, (await ApiClient.ProblemAsync(busy)).Code));
        Assert.Equal(HttpStatusCode.Accepted, again.StatusCode);
        Assert.Equal("pending", (await ApiClient.JsonAsync(again)).GetProperty("status").GetString());
        Assert.Equal(2L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind = 'shop.detect_platform' AND shop_id = $1", shopId));
    }

    [Fact]
    public async Task JobThatFailedWithoutAResult_IsFailedFetch()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        await AdminAsync("UPDATE ops.jobs SET state = 'failed', finished_at = now() WHERE kind = 'shop.detect_platform' AND shop_id = $1", shopId);

        var detection = await GetJsonAsync(owner, $"/api/t/{owner.TenantId}/shops/{shopId}/detection");

        Assert.Equal("failed", detection.GetProperty("status").GetString());
        Assert.Equal("fetch_failed", detection.GetProperty("failureCode").GetString());
    }

    [Fact]
    public async Task PlatformChosenByTheClient_IsStored_AndAudited()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        var path = $"/api/t/{owner.TenantId}/shops/{shopId}/platform";

        using var unknown = await owner.Browser.PutAsync(path, new { platform = "magento" });
        using var chosen = await owner.Browser.PutAsync(path, new { platform = "woocommerce" });

        Assert.Equal((HttpStatusCode.BadRequest, "platform.unknown"), ((await ApiClient.ProblemAsync(unknown)).Status, (await ApiClient.ProblemAsync(unknown)).Code));
        var shop = await ApiClient.JsonAsync(chosen);
        Assert.Equal("woocommerce", shop.GetProperty("platform").GetString());
        Assert.Equal("user", shop.GetProperty("platformSource").GetString());
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'shop.platform_set' AND entity_id = $1", shopId.ToString("D")));
    }

    /// <summary>Waits for the open job of the domain's e-shop and completes it as the worker would.</summary>
    private static async Task CompleteJobAsync(Guid tenantId, string domain, string detection, string platform)
    {
        for (var i = 0; i < 200; i++)
        {
            var job = await AdminScalarAsync<long?>(
                "SELECT j.id FROM ops.jobs j JOIN shop.shops s ON s.id = j.shop_id WHERE s.tenant_id = $1 AND s.domain = $2 AND j.kind = 'shop.detect_platform' AND j.state = 'queued'",
                tenantId, domain);
            if (job is { } id)
            {
                await AdminAsync("UPDATE shop.shops SET detection = $1::jsonb, platform = $2 WHERE tenant_id = $3 AND domain = $4", detection, platform, tenantId, domain);
                await AdminAsync("UPDATE ops.jobs SET state = 'succeeded', finished_at = now() WHERE id = $1", id);
                return;
            }

            await Task.Delay(25, Ct);
        }

        throw new TimeoutException("no job of the recognition");
    }
}
