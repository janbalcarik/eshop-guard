using System.Text.Json;
using EshopGuard.Api.Tests.Shops;

namespace EshopGuard.Api.Tests.Billing;

/// <summary>
/// The price of the scope of change 10 by the price list (change 12, tasks 4.1–4.5; requirement „Ocenění rozsahu e-shopu“):
/// <c>bylinkovo.sk</c> with SK and CZ versions of 5 834 products each, a price list of the market of the test.
/// </summary>
public sealed class PriceQuoteTests : ShopTestBase
{
    [Fact]
    public async Task TwoCountries_11668Products_AreTierT20000_AndStoredOnce()
    {
        await using var factory = Factory();
        var (owner, shopId) = await BylinkovoAsync(factory);
        using var __ = owner;
        var market = await BillingSeed.MarketAsync();
        await BillingSeed.UseMarketAsync(owner.TenantId, market);
        var list = await BillingSeed.PublishedAsync(market);

        var quote = await QuoteAsync(owner, shopId, "sk", "cz");

        var price = quote.GetProperty("price");
        Assert.Equal(11668, quote.GetProperty("scope").GetProperty("productTotal").GetInt32());
        Assert.Equal(["cz", "sk"], quote.GetProperty("scope").GetProperty("markets").EnumerateArray().Select(m => m.GetProperty("marketCode").GetString()!.ToLowerInvariant()).Order());
        Assert.Equal("offer", price.GetProperty("status").GetString());
        Assert.Equal("t20000", price.GetProperty("tierCode").GetString());
        Assert.Equal(20000, price.GetProperty("tierMaxProducts").GetInt32());
        Assert.Equal(199m, price.GetProperty("analysisNet").GetDecimal());
        Assert.Equal(59m, price.GetProperty("monitoringMonthlyNet").GetDecimal());
        Assert.Equal(11668, price.GetProperty("countedProducts").GetInt32());
        Assert.Equal("products", price.GetProperty("priceUnit").GetString());
        Assert.Equal(list, price.GetProperty("priceListId").GetGuid());
        var hash = quote.GetProperty("scope").GetProperty("scopeHash").GetString();
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM billing.price_quotes WHERE shop_id = $1 AND scope_hash = $2", shopId, hash));
        Assert.Empty(factory.Stripe.Calls);
    }

    [Fact]
    public async Task UntickingCzechia_GivesANewQuote_AndKeepsTheReasonOfTheVersion()
    {
        await using var factory = Factory();
        var (owner, shopId) = await BylinkovoAsync(factory);
        using var __ = owner;
        var market = await BillingSeed.MarketAsync();
        await BillingSeed.UseMarketAsync(owner.TenantId, market);
        await BillingSeed.PublishedAsync(market);

        var both = await QuoteAsync(owner, shopId, "sk", "cz");
        var sk = await QuoteAsync(owner, shopId, "sk");

        Assert.Equal(5834, sk.GetProperty("price").GetProperty("countedProducts").GetInt32());
        Assert.Equal("t20000", sk.GetProperty("price").GetProperty("tierCode").GetString());
        Assert.NotEqual(both.GetProperty("price").GetProperty("quoteId").GetGuid(), sk.GetProperty("price").GetProperty("quoteId").GetGuid());
        Assert.Contains(sk.GetProperty("scope").GetProperty("notCheckedVersions").EnumerateArray(),
            v => v.GetProperty("language").GetString() == "cs" && v.GetProperty("reason").GetString() == "not_needed_by_markets");
        Assert.Equal(2L, await AdminScalarAsync<long>("SELECT count(*) FROM billing.price_quotes WHERE shop_id = $1", shopId));
        var versions = await AdminScalarAsync<string>("SELECT versions::text FROM billing.price_quotes WHERE shop_id = $1 AND scope_hash = $2",
            shopId, sk.GetProperty("scope").GetProperty("scopeHash").GetString());
        Assert.Contains("not_needed_by_markets", versions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SameScope_SameQuoteIdAndAmounts_NoSecondRecord()
    {
        await using var factory = Factory();
        var (owner, shopId) = await BylinkovoAsync(factory);
        using var __ = owner;
        var market = await BillingSeed.MarketAsync();
        await BillingSeed.UseMarketAsync(owner.TenantId, market);
        await BillingSeed.PublishedAsync(market);

        var first = (await QuoteAsync(owner, shopId, "sk", "cz")).GetProperty("price");
        var second = (await QuoteAsync(owner, shopId, "sk", "cz")).GetProperty("price");

        Assert.Equal(first.GetProperty("quoteId").GetGuid(), second.GetProperty("quoteId").GetGuid());
        Assert.Equal(first.GetProperty("analysisNet").GetDecimal(), second.GetProperty("analysisNet").GetDecimal());
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM billing.price_quotes WHERE shop_id = $1", shopId));
    }

    [Fact]
    public async Task NoPublishedPriceList_IsUnavailable_WithoutStripe()
    {
        await using var factory = Factory();
        var (owner, shopId) = await BylinkovoAsync(factory);
        using var __ = owner;
        var market = await BillingSeed.MarketAsync("CZK");
        await BillingSeed.UseMarketAsync(owner.TenantId, market);
        await AdminAsync("INSERT INTO billing.price_lists (name, market_code, currency, valid_from, status, notice_days, created_at, updated_at) VALUES ('koncept', $1, 'CZK', now(), 'draft', 30, now(), now())", market);

        var price = (await QuoteAsync(owner, shopId, "sk", "cz")).GetProperty("price");

        Assert.Equal("unavailable", price.GetProperty("status").GetString());
        Assert.Equal("billing.price_list_missing", price.GetProperty("reasonCode").GetString());
        Assert.Equal("CZK", price.GetProperty("currency").GetString());
        Assert.Equal(JsonValueKind.Null, price.GetProperty("analysisNet").ValueKind);
        Assert.Equal(JsonValueKind.Null, price.GetProperty("todayNet").ValueKind);
        Assert.Empty(factory.Stripe.Calls);
    }

    [Fact]
    public async Task OtherPagesAboveTwiceTheProducts_IsAnIndividualOffer()
    {
        await using var factory = Factory();
        var (owner, shopId) = await BylinkovoAsync(factory);
        using var __ = owner;
        var market = await BillingSeed.MarketAsync();
        await BillingSeed.UseMarketAsync(owner.TenantId, market);
        await BillingSeed.PublishedAsync(market);
        await AdminAsync("UPDATE shop.shop_languages SET product_count = 300 WHERE shop_id = $1 AND language = 'sk'", shopId);
        await AdminAsync("""UPDATE checks.runs SET estimate = '{"basis":{"versions":[{"language":"sk","other_pages":4000},{"language":"cs","other_pages":38}]}}'::jsonb WHERE shop_id = $1""", shopId);

        var price = (await QuoteAsync(owner, shopId, "sk")).GetProperty("price");

        Assert.Equal("individual_offer", price.GetProperty("status").GetString());
        Assert.Equal("billing.fair_use_exceeded", price.GetProperty("reasonCode").GetString());
        Assert.True(price.GetProperty("fairUse").GetProperty("exceeded").GetBoolean());
        Assert.Equal(600, price.GetProperty("fairUse").GetProperty("otherPagesLimit").GetInt32());
        Assert.Equal(JsonValueKind.Null, price.GetProperty("todayNet").ValueKind);
    }

    [Fact]
    public async Task PriceListOfTheOtherModeOfStripe_IsRefused()
    {
        await using var factory = Factory();
        var (owner, shopId) = await BylinkovoAsync(factory);
        using var __ = owner;
        var market = await BillingSeed.MarketAsync();
        await BillingSeed.UseMarketAsync(owner.TenantId, market);
        await BillingSeed.PublishedAsync(market, mode: "live");

        var price = (await QuoteAsync(owner, shopId, "sk")).GetProperty("price");

        Assert.Equal("unavailable", price.GetProperty("status").GetString());
        Assert.Equal("billing.stripe_mode_mismatch", price.GetProperty("reasonCode").GetString());
    }

    [Fact]
    public async Task ThirdShopOfTheAccount_GetsTheVolumeDiscount_AndTheVatPreview()
    {
        await using var factory = Factory();
        var (owner, shopId) = await BylinkovoAsync(factory);
        using var __ = owner;
        var market = await BillingSeed.MarketAsync();
        await BillingSeed.UseMarketAsync(owner.TenantId, market);
        await BillingSeed.PublishedAsync(market, discount: (3, 10m));
        foreach (var _ in Enumerable.Range(0, 2))
        {
            var other = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
            await SeedSubscriptionAsync(owner.TenantId, other, "active");
        }

        var price = (await QuoteAsync(owner, shopId, "sk")).GetProperty("price");

        Assert.Equal(10m, price.GetProperty("monitoringDiscountPercent").GetDecimal());
        var vat = price.GetProperty("vatPreview");
        Assert.Equal("domestic_vat", vat.GetProperty("treatment").GetString());
        Assert.Equal(23m, vat.GetProperty("rate").GetDecimal());
        Assert.Equal(244.77m, vat.GetProperty("analysisGross").GetDecimal());
        Assert.Equal(65.31m, vat.GetProperty("monitoringGross").GetDecimal());
    }

    [Fact]
    public async Task TheTierOfTheQuote_IsThatOfTheBasisOfTheSample()
    {
        // Change 8 stores only the basis (products and other pages by version); the tier and the amount are always from here.
        await using var factory = Factory();
        var (owner, shopId) = await BylinkovoAsync(factory);
        using var __ = owner;
        var market = await BillingSeed.MarketAsync();
        await BillingSeed.UseMarketAsync(owner.TenantId, market);
        await BillingSeed.PublishedAsync(market);
        await AdminAsync("UPDATE shop.shop_languages SET product_count = 1460 WHERE shop_id = $1 AND language = 'sk'", shopId);

        var quote = await QuoteAsync(owner, shopId, "sk");

        Assert.Equal(1460, quote.GetProperty("scope").GetProperty("priceBasis").GetProperty("count").GetInt32());
        Assert.Equal("t2000", quote.GetProperty("price").GetProperty("tierCode").GetString());
        Assert.Equal(69m, quote.GetProperty("price").GetProperty("analysisNet").GetDecimal());
    }

    internal static async Task<(Person Owner, Guid ShopId)> BylinkovoAsync(ApiFactory factory)
    {
        var owner = await People.OwnerAsync(factory);
        var domain = NewDomain("bylinkovo");
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        await SeedBylinkovoAsync(owner.TenantId, shopId, domain);
        return (owner, shopId);
    }

    internal static async Task<JsonElement> QuoteAsync(Person owner, Guid shopId, params string[] markets)
    {
        using var response = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/shops/{shopId}/quote", new { activeMarkets = markets });
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await ApiClient.JsonAsync(response);
    }
}
