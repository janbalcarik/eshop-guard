using System.Net;
using System.Text.Json;
using EshopGuard.Application.Shops.Onboarding;
using EshopGuard.Data.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Api.Tests.Shops;

/// <summary>The state of the onboarding and the readiness for the order (change 10, task 8.4; specification „Připravenost k objednávce“).</summary>
public sealed class OnboardingStateTests : ShopTestBase
{
    [Fact]
    public async Task AfterCreation_Connect_AfterTheSample_Scope()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain();
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        var path = $"/api/t/{owner.TenantId}/shops/{shopId}/onboarding";

        var created = await GetJsonAsync(owner, path);
        await SeedBylinkovoAsync(owner.TenantId, shopId, domain);
        var sampled = await GetJsonAsync(owner, path);

        Assert.Equal("connect", created.GetProperty("step").GetString());
        Assert.Contains("scope.basis_missing", Codes(created));
        Assert.Equal("scope", sampled.GetProperty("step").GetString());
        Assert.Equal(["markets.not_confirmed", "shop.ownership_not_verified"], Codes(sampled));
        Assert.Equal("finished", sampled.GetProperty("sample").GetProperty("status").GetString());
        Assert.True(sampled.GetProperty("ownership").GetProperty("required").GetBoolean());
    }

    [Fact]
    public async Task VersionAwaitingConfirmation_BlocksTheOrder()
    {
        await using var factory = Factory(settings: new Dictionary<string, string?> { ["Shops:Ownership:RequiredBefore:0"] = "none" });
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain();
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        await SeedBylinkovoAsync(owner.TenantId, shopId, domain);
        await AdminAsync("UPDATE shop.shop_languages SET status = 'needs_confirmation', base_url = 'https://goodie.cz/' WHERE shop_id = $1 AND language = 'cs'", shopId);
        using (var markets = await owner.Browser.PutAsync($"/api/t/{owner.TenantId}/shops/{shopId}/markets", new { active = new[] { "sk", "cz" } }))
        {
            Assert.Equal(HttpStatusCode.OK, markets.StatusCode);
        }

        var readiness = await ReadinessAsync(factory, owner, shopId);
        var state = await GetJsonAsync(owner, $"/api/t/{owner.TenantId}/shops/{shopId}/onboarding");

        Assert.False(readiness.Ready);
        Assert.Equal(["languages.confirmation_pending"], readiness.Blocking);
        Assert.Equal(["cs"], state.GetProperty("languagesAwaitingConfirmation").EnumerateArray().Select(l => l.GetString()));
    }

    [Fact]
    public async Task OtherMarketsStoredAfterTheQuote_GiveAnotherScopeHash()
    {
        await using var factory = Factory(settings: new Dictionary<string, string?> { ["Shops:Ownership:RequiredBefore:0"] = "none" });
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain();
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        await SeedBylinkovoAsync(owner.TenantId, shopId, domain);
        using (var both = await owner.Browser.PutAsync($"/api/t/{owner.TenantId}/shops/{shopId}/markets", new { active = new[] { "sk", "cz" } }))
        {
            Assert.Equal(HttpStatusCode.OK, both.StatusCode);
        }

        var quoted = await ReadinessAsync(factory, owner, shopId);
        using (var sk = await owner.Browser.PutAsync($"/api/t/{owner.TenantId}/shops/{shopId}/markets", new { active = new[] { "sk" } }))
        {
            Assert.Equal(HttpStatusCode.OK, sk.StatusCode);
        }

        var ordered = await ReadinessAsync(factory, owner, shopId);

        Assert.True(quoted.Ready, string.Join(",", quoted.Blocking));
        Assert.True(ordered.Ready);
        Assert.NotEqual(quoted.Scope!.ScopeHash, ordered.Scope!.ScopeHash);
    }

    internal static async Task<ShopOrderReadinessResult> ReadinessAsync(ApiFactory factory, Person owner, Guid shopId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(owner.TenantId, owner.UserId);
        return await scope.ServiceProvider.GetRequiredService<IShopOrderReadiness>().CheckAsync(shopId, Ct);
    }

    private static List<string?> Codes(JsonElement state) => state.GetProperty("blocking").EnumerateArray().Select(b => b.GetString()).ToList();
}
