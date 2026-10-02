using System.Net;

namespace EshopGuard.Api.Tests.Shops;

/// <summary>The places of sale (change 10, task 5.4; specification „Místa prodeje jen podporovaná a s důvodem“).</summary>
public sealed class MarketsTests : ShopTestBase
{
    [Fact]
    public async Task SkAndCz_ArePreselectedWithTheirReasons_PolandIsNotShown()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain("bylinkovo");
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        await SeedBylinkovoAsync(owner.TenantId, shopId, domain);

        var markets = (await GetJsonAsync(owner, $"/api/t/{owner.TenantId}/shops/{shopId}/markets")).GetProperty("markets").EnumerateArray().ToList();

        Assert.Equal(["sk", "cz"], markets.Select(m => m.GetProperty("marketCode").GetString()));
        Assert.All(markets, m => Assert.True(m.GetProperty("preselected").GetBoolean()));
        var sk = markets[0];
        Assert.True(sk.GetProperty("isHome").GetBoolean());
        Assert.Equal("full", sk.GetProperty("checksStatus").GetString());
        var skSignals = sk.GetProperty("evidence").EnumerateArray().Where(e => e.GetProperty("kind").GetString() == "signal").Select(e => e.GetProperty("code").GetString()).ToList();
        Assert.Equal(["seat", "tld", "currency"], skSignals);
        Assert.Equal("EUR", sk.GetProperty("evidence").EnumerateArray().Single(e => e.GetProperty("code").GetString() == "currency").GetProperty("params").GetProperty("currency").GetString());
        var cz = markets[1];
        Assert.Equal("limited", cz.GetProperty("checksStatus").GetString());
        var citation = cz.GetProperty("evidence").EnumerateArray().Single(e => e.GetProperty("kind").GetString() == "citation");
        Assert.Equal("Doprava do Českej republiky 3,90 €", citation.GetProperty("quote").GetString());
        Assert.Equal($"https://{domain}/doprava", citation.GetProperty("pageUrl").GetString());
        Assert.Contains(cz.GetProperty("evidence").EnumerateArray(), e => e.GetProperty("code").GetString() == "language_version"
            && e.GetProperty("params").GetProperty("language").GetString() == "cs");
        var body = (await GetJsonAsync(owner, $"/api/t/{owner.TenantId}/shops/{shopId}/markets")).GetRawText();
        Assert.DoesNotContain("interní", body, StringComparison.Ordinal);
        Assert.Equal("unsupported", await AdminScalarAsync<string>("SELECT status FROM shop.shop_markets WHERE shop_id = $1 AND country_code = 'PL'", shopId));
    }

    [Fact]
    public async Task GeneralClaim_IsNotPreselected()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        await SeedMarketAsync(owner.TenantId, shopId, "CZ", "suggested", "generic", false, null,
            """{"quotes":[{"quote":"Doručujeme do celej EÚ","source":"https://x.sk/doprava"}],"home_basis":null,"raised_by":null,"internal":{"reason":"","uncertain":""}}""");

        var market = (await GetJsonAsync(owner, $"/api/t/{owner.TenantId}/shops/{shopId}/markets")).GetProperty("markets")[0];

        Assert.False(market.GetProperty("preselected").GetBoolean());
        Assert.Equal("generic", market.GetProperty("evidenceLevel").GetString());
    }

    [Fact]
    public async Task NoMarket_UnsupportedAndUnknown_AreRefused_AndNothingChanges()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain();
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        await SeedBylinkovoAsync(owner.TenantId, shopId, domain);
        var path = $"/api/t/{owner.TenantId}/shops/{shopId}/markets";

        using var none = await owner.Browser.PutAsync(path, new { active = Array.Empty<string>() });
        using var poland = await owner.Browser.PutAsync(path, new { active = new[] { "pl" } });
        using var garbage = await owner.Browser.PutAsync(path, new { active = new[] { "sk", "x-1" } });

        Assert.Equal((HttpStatusCode.BadRequest, "markets.none_selected"), ((await ApiClient.ProblemAsync(none)).Status, (await ApiClient.ProblemAsync(none)).Code));
        var problem = await ApiClient.ProblemAsync(poland);
        Assert.Equal("markets.unsupported", problem.Code);
        Assert.Equal("pl", problem.Body.GetProperty("params").GetProperty("code").GetString());
        Assert.Equal("markets.unknown", (await ApiClient.ProblemAsync(garbage)).Code);
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM shop.shop_markets WHERE shop_id = $1 AND confirmed_at IS NOT NULL", shopId));
    }

    [Fact]
    public async Task Confirmation_StoresActiveAndDeclined_AndAManualCountryIsTheUsers()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        await SeedMarketAsync(owner.TenantId, shopId, "SK", "suggested", "strong", true, null, null);

        using var response = await owner.Browser.PutAsync($"/api/t/{owner.TenantId}/shops/{shopId}/markets", new { active = new[] { "sk", "cz" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ApiClient.JsonAsync(response);
        Assert.Equal(owner.UserId, body.GetProperty("confirmedBy").GetGuid());
        var rows = (await AdminRowsAsync("SELECT country_code, status, source, confirmed_by FROM shop.shop_markets WHERE shop_id = $1 ORDER BY country_code", shopId))
            .Select(r => (r[0], r[1], r[2], r[3])).ToList();
        Assert.Equal([("CZ", "active", "user", (object?)owner.UserId), ("SK", "active", "detected", owner.UserId)], rows);
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'markets.confirmed' AND entity_id = $1", shopId.ToString("D")));

        using var only = await owner.Browser.PutAsync($"/api/t/{owner.TenantId}/shops/{shopId}/markets", new { active = new[] { "sk" } });
        Assert.Equal("declined", await AdminScalarAsync<string>("SELECT status FROM shop.shop_markets WHERE shop_id = $1 AND country_code = 'CZ'", shopId));
    }

    [Fact]
    public async Task DuringAnAnalysis_TheMarketsAreLocked()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        await SeedRunAsync(owner.TenantId, shopId, "full_analysis", "crawling");

        using var response = await owner.Browser.PutAsync($"/api/t/{owner.TenantId}/shops/{shopId}/markets", new { active = new[] { "sk" } });

        Assert.Equal((HttpStatusCode.Conflict, "markets.locked_during_run"), ((await ApiClient.ProblemAsync(response)).Status, (await ApiClient.ProblemAsync(response)).Code));
    }
}
