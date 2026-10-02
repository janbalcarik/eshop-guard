using System.Net;

namespace EshopGuard.Api.Tests.Shops;

/// <summary>The free sample (change 10, task 4.5; specification „Ukázka zdarma jednou na doménu“).</summary>
public sealed class SampleTests : ShopTestBase
{
    [Fact]
    public async Task FirstSample_IsQueued_ClaimsTheDomain_AndMovesTheShopToSample()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain("bylinkovo");
        var shopId = (await CreateShopAsync(owner, $"https://www.{domain}/")).GetProperty("id").GetGuid();

        using var response = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/shops/{shopId}/sample");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var sample = await ApiClient.JsonAsync(response);
        Assert.Equal("queued", sample.GetProperty("status").GetString());
        var runId = sample.GetProperty("runId").GetGuid();
        Assert.Equal("sample", await AdminScalarAsync<string>("SELECT status FROM shop.shops WHERE id = $1", shopId));
        Assert.Equal(shopId, await AdminScalarAsync<Guid>("SELECT shop_id FROM shop.free_sample_claims WHERE domain = $1", domain));
        Assert.Equal("free_sample", await AdminScalarAsync<string>("SELECT kind FROM checks.runs WHERE id = $1", runId));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE run_id = $1", runId));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'sample.started' AND entity_id = $1", shopId.ToString("D")));
    }

    [Fact]
    public async Task DomainUsedByAnotherTenant_Is409WithoutAnythingAboutIt_AndNoRunIsCreated()
    {
        await using var factory = Factory();
        using var a = await People.OwnerAsync(factory);
        using var b = await People.OwnerAsync(factory);
        var domain = NewDomain("vegis");
        var shopB = (await CreateShopAsync(b, "https://" + domain)).GetProperty("id").GetGuid();
        var shopA = (await CreateShopAsync(a, "https://" + domain)).GetProperty("id").GetGuid();
        using (var first = await b.Browser.PostAsync($"/api/t/{b.TenantId}/shops/{shopB}/sample"))
        {
            Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        }

        using var response = await a.Browser.PostAsync($"/api/t/{a.TenantId}/shops/{shopA}/sample");

        var problem = await ApiClient.ProblemAsync(response);
        Assert.Equal((HttpStatusCode.Conflict, "sample.already_used_for_domain"), (problem.Status, problem.Code));
        var body = problem.Body.GetRawText();
        Assert.DoesNotContain(b.TenantId.ToString("D"), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(shopB.ToString("D"), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(b.Email, body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM checks.runs WHERE shop_id = $1", shopA));
        Assert.Equal("draft", await AdminScalarAsync<string>("SELECT status FROM shop.shops WHERE id = $1", shopA));
    }

    [Fact]
    public async Task ShopInSample_Is409NotAllowedInStatus()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        using (var first = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/shops/{shopId}/sample"))
        {
            Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        }

        using var second = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/shops/{shopId}/sample");

        Assert.Equal("sample.not_allowed_in_status", (await ApiClient.ProblemAsync(second)).Code);
    }

    [Fact]
    public async Task SixthSampleOfTheDay_Is429_AndClaimsNothing()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        for (var i = 0; i < 5; i++)
        {
            var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
            using var started = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/shops/{shopId}/sample");
            Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        }

        var domain = NewDomain();
        var sixth = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        using var response = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/shops/{sixth}/sample");

        var problem = await ApiClient.ProblemAsync(response);
        Assert.Equal((HttpStatusCode.TooManyRequests, "rate_limited"), (problem.Status, problem.Code));
        Assert.Equal("tenant_daily", problem.Body.GetProperty("params").GetProperty("scope").GetString());
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM shop.free_sample_claims WHERE domain = $1", domain));
    }

    [Fact]
    public async Task WithoutASample_GetIs404NotStarted()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();

        using var response = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}/shops/{shopId}/sample");

        Assert.Equal((HttpStatusCode.NotFound, "sample.not_started"), ((await ApiClient.ProblemAsync(response)).Status, (await ApiClient.ProblemAsync(response)).Code));
    }
}
