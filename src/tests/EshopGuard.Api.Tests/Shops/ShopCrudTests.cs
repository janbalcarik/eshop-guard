using System.Net;
using System.Text.Json;

namespace EshopGuard.Api.Tests.Shops;

/// <summary>Creating, listing, renaming and deleting e-shops (change 10, task 1.7; specification „Založení e-shopu z adresy“).</summary>
public sealed class ShopCrudTests : ShopTestBase
{
    [Fact]
    public async Task NewShop_IsDraftOnTheWeb_WithItsRecognitionJobInTheSameTransaction()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain("bylinkovo");

        var shop = await CreateShopAsync(owner, $"https://www.{domain.ToUpperInvariant()}/");

        var shopId = shop.GetProperty("id").GetGuid();
        Assert.Equal(domain, shop.GetProperty("domain").GetString());
        Assert.Equal("/", shop.GetProperty("basePath").GetString());
        Assert.Equal($"https://www.{domain}/", shop.GetProperty("baseUrl").GetString());
        Assert.Equal("draft", shop.GetProperty("status").GetString());
        Assert.Equal("web", shop.GetProperty("sourceMode").GetString());
        Assert.Equal("unknown", shop.GetProperty("platform").GetString());
        Assert.Equal("pending", shop.GetProperty("detection").GetProperty("status").GetString());
        Assert.NotEmpty(shop.GetProperty("modules").EnumerateArray());

        var job = (await AdminRowsAsync("SELECT id, priority, resource_class, payload->>'shop_id', tenant_id FROM ops.jobs WHERE kind = 'shop.detect_platform' AND shop_id = $1", shopId)).Single();
        Assert.Equal([(short)0, "fetch", shopId.ToString("D"), owner.TenantId], job[1..]);
        Assert.Equal(job[0], await AdminScalarAsync<long>("SELECT (detection->>'job_id')::bigint FROM shop.shops WHERE id = $1", shopId));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'shop.created' AND entity_id = $1", shopId.ToString("D")));
    }

    [Fact]
    public async Task InternalAddress_IsRefused_AndNothingIsCreated()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);

        using var response = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/shops", new { url = "http://169.254.169.254/latest/meta-data" });

        var problem = await ApiClient.ProblemAsync(response);
        Assert.Equal((HttpStatusCode.BadRequest, "shop.url_not_allowed"), (problem.Status, problem.Code));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM shop.shops WHERE tenant_id = $1", owner.TenantId));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE tenant_id = $1 AND kind = 'shop.detect_platform'", owner.TenantId));
    }

    [Fact]
    public async Task Duplicate_Is409WithTheShop_AndTheSameDomainOfAnotherTenantIs201()
    {
        await using var factory = Factory();
        using var a = await People.OwnerAsync(factory);
        using var b = await People.OwnerAsync(factory);
        var domain = NewDomain("vegis");
        var first = await CreateShopAsync(a, $"https://{domain}");

        using var again = await a.Browser.PostAsync($"/api/t/{a.TenantId}/shops", new { url = $"http://www.{domain}/index.html" });
        using var other = await b.Browser.PostAsync($"/api/t/{b.TenantId}/shops", new { url = $"https://{domain}" });

        var problem = await ApiClient.ProblemAsync(again);
        Assert.Equal((HttpStatusCode.Conflict, "shop.already_exists"), (problem.Status, problem.Code));
        Assert.Equal(first.GetProperty("id").GetGuid(), problem.Body.GetProperty("params").GetProperty("shopId").GetGuid());
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);
        Assert.NotEqual(first.GetProperty("id").GetGuid(), (await ApiClient.JsonAsync(other)).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task List_HasOnlyTheShopsOfTheTenant()
    {
        await using var factory = Factory();
        using var a = await People.OwnerAsync(factory);
        using var b = await People.OwnerAsync(factory);
        for (var i = 0; i < 2; i++)
        {
            await CreateShopAsync(a);
        }

        for (var i = 0; i < 3; i++)
        {
            await CreateShopAsync(b);
        }

        var list = await GetJsonAsync(a, $"/api/t/{a.TenantId}/shops");

        Assert.Equal(2, list.GetArrayLength());
        Assert.All(list.EnumerateArray(), s => Assert.Equal("draft", s.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task Rename_OverAnOldVersion_Is409_AndTheFirstNameStays()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shop = await CreateShopAsync(owner);
        var path = $"/api/t/{owner.TenantId}/shops/{shop.GetProperty("id").GetGuid()}";
        var version = shop.GetProperty("version").GetUInt32();

        using var first = await owner.Browser.PatchAsync(path, new { name = "Bylinkovo", version });
        using var second = await owner.Browser.PatchAsync(path, new { name = "Iné meno", version });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("concurrency.conflict", (await ApiClient.ProblemAsync(second)).Code);
        Assert.Equal("Bylinkovo", (await GetJsonAsync(owner, path)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Delete_WithAnActiveSubscription_Is409_AndTheShopStays()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        await SeedSubscriptionAsync(owner.TenantId, shopId, "active");

        using var response = await owner.Browser.DeleteAsync($"/api/t/{owner.TenantId}/shops/{shopId}");

        Assert.Equal((HttpStatusCode.Conflict, "shop.subscription_active"), ((await ApiClient.ProblemAsync(response)).Status, (await ApiClient.ProblemAsync(response)).Code));
        Assert.Null(await AdminScalarAsync<DateTime?>("SELECT deleted_at FROM shop.shops WHERE id = $1", shopId));
    }

    [Fact]
    public async Task Delete_WithARunningRun_Is409()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        await SeedRunAsync(owner.TenantId, shopId, "free_sample", "crawling");

        using var response = await owner.Browser.DeleteAsync($"/api/t/{owner.TenantId}/shops/{shopId}");

        Assert.Equal("shop.run_in_progress", (await ApiClient.ProblemAsync(response)).Code);
    }

    [Fact]
    public async Task AfterDelete_TheSameAddressCanBeAddedAgain()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var url = "https://" + NewDomain();
        var shopId = (await CreateShopAsync(owner, url)).GetProperty("id").GetGuid();

        using var deleted = await owner.Browser.DeleteAsync($"/api/t/{owner.TenantId}/shops/{shopId}");
        using var gone = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}/shops/{shopId}");
        var again = await CreateShopAsync(owner, url);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal("shop.not_found", (await ApiClient.ProblemAsync(gone)).Code);
        Assert.NotEqual(shopId, again.GetProperty("id").GetGuid());
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'shop.deleted' AND entity_id = $1", shopId.ToString("D")));
    }

    [Fact]
    public async Task ManyCreations_AreLimitedPerTenant()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        for (var i = 0; i < 20; i++)
        {
            await CreateShopAsync(owner);
        }

        using var response = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/shops", new { url = "https://" + NewDomain() });

        var problem = await ApiClient.ProblemAsync(response);
        Assert.Equal((HttpStatusCode.TooManyRequests, "rate_limited"), (problem.Status, problem.Code));
        Assert.Equal("tenant_hourly", problem.Body.GetProperty("params").GetProperty("scope").GetString());
    }
}
