using System.Net;
using EshopGuard.Api.Tests.Fakes;
using EshopGuard.Application.Shops.Pricing;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Api.Tests.Shops;

/// <summary>The scope and the quote (change 10, task 7.7; specification „Rozsah kontroly a dynamický výpočet ceny“).</summary>
public sealed class QuoteTests : ShopTestBase
{
    private static ApiFactory PricedFactory(FakePriceQuoteService prices) => Factory(services: s => s.AddSingleton<IPriceQuoteService>(prices));

    [Fact]
    public async Task UntickingCz_RecalculatesTheProducts_AndStoresNothing()
    {
        var prices = new FakePriceQuoteService();
        await using var factory = PricedFactory(prices);
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain("bylinkovo");
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        await SeedBylinkovoAsync(owner.TenantId, shopId, domain);
        var markets = await AdminRowsAsync("SELECT country_code, status, confirmed_at FROM shop.shop_markets WHERE shop_id = $1 ORDER BY 1", shopId);
        var path = $"/api/t/{owner.TenantId}/shops/{shopId}/quote";

        using var both = await owner.Browser.PostAsync(path, new { activeMarkets = new[] { "sk", "cz" } });
        using var sk = await owner.Browser.PostAsync(path, new { activeMarkets = new[] { "sk" } });

        var first = await ApiClient.JsonAsync(both);
        var second = await ApiClient.JsonAsync(sk);
        Assert.Equal(11668, first.GetProperty("scope").GetProperty("productTotal").GetInt32());
        Assert.Equal(2, first.GetProperty("scope").GetProperty("checkedVersions").GetArrayLength());
        Assert.Equal("up_to_20000", first.GetProperty("price").GetProperty("tierCode").GetString());
        Assert.Equal(5834, second.GetProperty("scope").GetProperty("productTotal").GetInt32());
        Assert.Equal(["sk"], second.GetProperty("scope").GetProperty("checkedVersions").EnumerateArray().Select(v => v.GetProperty("language").GetString()));
        Assert.Equal("up_to_20000", second.GetProperty("price").GetProperty("tierCode").GetString());
        Assert.NotEqual(first.GetProperty("scope").GetProperty("scopeHash").GetString(), second.GetProperty("scope").GetProperty("scopeHash").GetString());
        Assert.Equal(markets, await AdminRowsAsync("SELECT country_code, status, confirmed_at FROM shop.shop_markets WHERE shop_id = $1 ORDER BY 1", shopId));
        Assert.Equal(2, prices.Requests.Count);
    }

    [Fact]
    public async Task OneVersionForTwoMarkets_CountsForEach()
    {
        var prices = new FakePriceQuoteService();
        await using var factory = PricedFactory(prices);
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain();
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        await SeedBylinkovoAsync(owner.TenantId, shopId, domain);
        await AdminAsync("DELETE FROM shop.shop_languages WHERE shop_id = $1 AND language = 'cs'", shopId);

        using var response = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/shops/{shopId}/quote", new { activeMarkets = new[] { "sk", "cz" } });

        var scope = (await ApiClient.JsonAsync(response)).GetProperty("scope");
        var sk = scope.GetProperty("checkedVersions").EnumerateArray().Single();
        Assert.Equal(["cz", "sk"], sk.GetProperty("jurisdictions").EnumerateArray().Select(j => j.GetString()));
        Assert.Equal(11668, scope.GetProperty("productTotal").GetInt32());
    }

    [Fact]
    public async Task SampleNotFinished_Is409_AndWithoutASampleTheBasisIsMissing()
    {
        await using var factory = PricedFactory(new FakePriceQuoteService());
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain();
        var running = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        await SeedBylinkovoAsync(owner.TenantId, running, domain, runStatus: "evaluating");
        var empty = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();

        using var notFinished = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/shops/{running}/quote", new { activeMarkets = new[] { "sk" } });
        using var missing = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/shops/{empty}/quote", new { activeMarkets = new[] { "sk" } });
        using var scope = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}/shops/{empty}/scope");

        Assert.Equal((HttpStatusCode.Conflict, "quote.sample_not_finished"), ((await ApiClient.ProblemAsync(notFinished)).Status, (await ApiClient.ProblemAsync(notFinished)).Code));
        Assert.Equal("quote.basis_missing", (await ApiClient.ProblemAsync(missing)).Code);
        Assert.Equal("scope.basis_missing", (await ApiClient.ProblemAsync(scope)).Code);
    }

    [Fact]
    public async Task UnknownProductCount_GivesNoPrice()
    {
        await using var factory = PricedFactory(new FakePriceQuoteService());
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain();
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        await SeedBylinkovoAsync(owner.TenantId, shopId, domain, csProducts: null);

        using var response = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/shops/{shopId}/quote", new { activeMarkets = new[] { "sk", "cz" } });
        var scope = await GetJsonAsync(owner, $"/api/t/{owner.TenantId}/shops/{shopId}/scope");

        Assert.Equal((HttpStatusCode.Conflict, "scope.product_count_unknown"), ((await ApiClient.ProblemAsync(response)).Status, (await ApiClient.ProblemAsync(response)).Code));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, scope.GetProperty("productTotal").ValueKind);
        Assert.Contains("scope.product_count_unknown", scope.GetProperty("issues").EnumerateArray().Select(i => i.GetString()));
    }

    [Fact]
    public async Task WithoutPayments_QuoteIs503_AndTheScopeStillAnswers()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain();
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        await SeedBylinkovoAsync(owner.TenantId, shopId, domain);

        using var quote = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/shops/{shopId}/quote", new { activeMarkets = new[] { "sk" } });
        var scope = await GetJsonAsync(owner, $"/api/t/{owner.TenantId}/shops/{shopId}/scope");

        Assert.Equal((HttpStatusCode.ServiceUnavailable, "billing.unavailable"), ((await ApiClient.ProblemAsync(quote)).Status, (await ApiClient.ProblemAsync(quote)).Code));
        Assert.Equal(11668, scope.GetProperty("productTotal").GetInt32());
        Assert.Equal(76, scope.GetProperty("otherPagesTotal").GetInt32());
        Assert.Equal(64, scope.GetProperty("scopeHash").GetString()!.Length);
    }

    [Fact]
    public async Task Viewer_CannotAskForAQuote()
    {
        await using var factory = PricedFactory(new FakePriceQuoteService());
        using var owner = await People.OwnerAsync(factory);
        using var viewer = await People.MemberAsync(factory, owner, "viewer");
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();

        using var response = await viewer.Browser.PostAsync($"/api/t/{owner.TenantId}/shops/{shopId}/quote", new { activeMarkets = new[] { "sk" } });

        var problem = await ApiClient.ProblemAsync(response);
        Assert.Equal((HttpStatusCode.Forbidden, "auth.forbidden_role"), (problem.Status, problem.Code));
    }
}
