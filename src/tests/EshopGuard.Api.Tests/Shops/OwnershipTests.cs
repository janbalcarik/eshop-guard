using System.Net;
using EshopGuard.Jobs.Runs;
using EshopGuard.Data.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Api.Tests.Shops;

/// <summary>The verification of ownership through the API (change 10, task 9.7; specification „Ověření vlastnictví e-shopu“).</summary>
public sealed class OwnershipTests : ShopTestBase
{
    private static ApiFactory WithPolicy(string gate) => Factory(settings: new Dictionary<string, string?> { ["Shops:Ownership:RequiredBefore:0"] = gate });

    [Fact]
    public async Task PolicySample_RefusesTheSampleOfAnUnverifiedShop()
    {
        await using var factory = WithPolicy("sample");
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain();
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();

        using var response = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/shops/{shopId}/sample");

        var problem = await ApiClient.ProblemAsync(response);
        Assert.Equal((HttpStatusCode.Conflict, "shop.ownership_not_verified"), (problem.Status, problem.Code));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM shop.free_sample_claims WHERE domain = $1", domain));

        await AdminAsync("UPDATE shop.shops SET ownership_verified_at = now(), verification_method = 'dns' WHERE id = $1", shopId);
        using var verified = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/shops/{shopId}/sample");
        Assert.Equal(HttpStatusCode.Accepted, verified.StatusCode);
    }

    [Fact]
    public async Task PolicyFullAnalysis_BlocksTheOrder_AndTheRunServiceGivesTheSameCode()
    {
        await using var factory = WithPolicy("full_analysis");
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain();
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        await SeedBylinkovoAsync(owner.TenantId, shopId, domain);

        var readiness = await OnboardingStateTests.ReadinessAsync(factory, owner, shopId);
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(owner.TenantId, owner.UserId);
        var run = await scope.ServiceProvider.GetRequiredService<IRunService>().CreateFullAnalysisAsync(shopId, owner.UserId, Ct);

        Assert.Contains("shop.ownership_not_verified", readiness.Blocking);
        Assert.Equal("shop.ownership_not_verified", run.ErrorCode);
    }

    [Fact]
    public async Task Verification_HasATokenAndItsInstructions_AndTheCheckIsAJob()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain("bylinkovo");
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        var path = $"/api/t/{owner.TenantId}/shops/{shopId}/ownership";

        using var unknown = await owner.Browser.PostAsync($"{path}/verifications", new { method = "email" });
        using var created = await owner.Browser.PostAsync($"{path}/verifications", new { method = "dns" });
        var verification = await ApiClient.JsonAsync(created);
        var id = verification.GetProperty("id").GetGuid();
        using var check = await owner.Browser.PostAsync($"{path}/verifications/{id}/check");
        using var missing = await owner.Browser.PostAsync($"{path}/verifications/{Guid.NewGuid()}/check");

        Assert.Equal((HttpStatusCode.BadRequest, "ownership.method_unknown"), ((await ApiClient.ProblemAsync(unknown)).Status, (await ApiClient.ProblemAsync(unknown)).Code));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var token = verification.GetProperty("token").GetString()!;
        Assert.Equal(22, token.Length);
        Assert.Matches("^[A-Za-z0-9_-]{22}$", token);
        Assert.Equal($"_eshopguard.{domain}", verification.GetProperty("instructions").GetProperty("dnsName").GetString());
        Assert.Equal($"eshopguard-site-verification={token}", verification.GetProperty("instructions").GetProperty("dnsValue").GetString());
        Assert.Equal(HttpStatusCode.Accepted, check.StatusCode);
        Assert.Equal("pending", (await ApiClient.JsonAsync(check)).GetProperty("status").GetString());
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.jobs WHERE kind = 'shop.verify_ownership' AND priority = 0 AND payload->>'verification_id' = $1", id.ToString("D")));
        Assert.Equal("ownership.verification_not_found", (await ApiClient.ProblemAsync(missing)).Code);

        var ownership = await GetJsonAsync(owner, path);
        Assert.False(ownership.GetProperty("verified").GetBoolean());
        Assert.Equal(["full_analysis"], ownership.GetProperty("requiredBefore").EnumerateArray().Select(g => g.GetString()));
        Assert.Single(ownership.GetProperty("verifications").EnumerateArray());
    }

    [Fact]
    public async Task VerifiedShop_RefusesAnotherVerification()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        await AdminAsync("UPDATE shop.shops SET ownership_verified_at = now(), verification_method = 'meta' WHERE id = $1", shopId);

        using var response = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/shops/{shopId}/ownership/verifications", new { method = "meta" });

        Assert.Equal("ownership.already_verified", (await ApiClient.ProblemAsync(response)).Code);
    }

    [Fact]
    public async Task MissingPolicy_StopsTheStart()
    {
        await using var factory = Factory(settings: new Dictionary<string, string?> { ["Shops:Ownership:RequiredBefore:0"] = null });

        var error = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(error);
        Assert.Contains("Shops:Ownership:RequiredBefore", error.ToString(), StringComparison.Ordinal);
    }
}
