using System.Net;
using System.Text.Json;
using EshopGuard.Api.Tests.Shops;

namespace EshopGuard.Api.Tests.Billing;

/// <summary>
/// The order of a full analysis and its Checkout (change 12, tasks 5.1–5.4; requirements „Garantovaná cena objednávky“ and
/// „Platba úvodní analýzy a sledování přes Stripe Checkout“): <c>bylinkovo.sk</c> with SK and CZ (11 668 products, tier
/// <c>t20000</c>), a price list of the market of the test, a Slovak company as the tenant.
/// </summary>
public sealed class OrderFlowTests : ShopTestBase
{
    [Fact]
    public async Task Order_KeepsTheSnapshot_AndTheRunWaitsForThePayment()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;

        using var response = await OrderAsync(ready);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await ApiClient.JsonAsync(response);
        Assert.Equal("created", order.GetProperty("status").GetString());
        Assert.Equal("t20000", order.GetProperty("tierCode").GetString());
        Assert.Equal((199m, 23m, 45.77m, 244.77m), (order.GetProperty("analysisNet").GetDecimal(), order.GetProperty("vatRate").GetDecimal(),
            order.GetProperty("vatAmount").GetDecimal(), order.GetProperty("analysisGross").GetDecimal()));
        Assert.Equal(("EUR", "domestic_vat", 59m), (order.GetProperty("currency").GetString(), order.GetProperty("taxTreatment").GetString(),
            order.GetProperty("monitoringMonthlyNet").GetDecimal()));
        var id = order.GetProperty("id").GetGuid();
        var key = ready.PriceListId.ToString("N")[..8];
        var row = Assert.Single(await AdminRowsAsync(
            "SELECT scope_hash, terms_version, stripe_price_analysis, stripe_price_monitoring, price_quote_id, price_list_id FROM billing.orders WHERE id = $1", id));
        Assert.Equal(new object?[] { ready.ScopeHash, "test-terms-1", $"price_{key}_t20000_analysis", $"price_{key}_t20000_monthly", ready.QuoteId, ready.PriceListId }, row);
        var runId = order.GetProperty("runId").GetGuid();
        Assert.Equal("full_analysis", await AdminScalarAsync<string>("SELECT kind FROM checks.runs WHERE id = $1 AND shop_id = $2", runId, ready.ShopId));
        Assert.Equal("awaiting_payment", await AdminScalarAsync<string>("SELECT status FROM shop.shops WHERE id = $1", ready.ShopId));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'order.created' AND entity_id = $1", id.ToString("D")));
        Assert.Empty(factory.Stripe.Calls);
    }

    [Fact]
    public async Task SecondTab_GetsTheOpenOrder_AndTheSameCheckout()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;

        using var first = await OrderAsync(ready);
        using var second = await OrderAsync(ready);
        var id = (await ApiClient.JsonAsync(first)).GetProperty("id").GetGuid();
        var one = await CheckoutAsync(ready, id);
        var two = await CheckoutAsync(ready, id);

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.OK), (first.StatusCode, second.StatusCode));
        Assert.Equal(id, (await ApiClient.JsonAsync(second)).GetProperty("id").GetGuid());
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM billing.orders WHERE shop_id = $1", ready.ShopId));
        Assert.Equal(one.GetProperty("url").GetString(), two.GetProperty("url").GetString());
        Assert.Equal(1, factory.Stripe.Count("CreateCheckoutSessionAsync"));
        Assert.Equal(1, factory.Stripe.Count("CreateCustomerAsync"));
    }

    [Fact]
    public async Task ScopeChangedInAnotherTab_Is409Stale_WithTheNewQuote_AndNoCheckout()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;
        await MarketsAsync(ready, "sk");

        using var response = await OrderAsync(ready);

        var problem = await ApiClient.ProblemAsync(response);
        Assert.Equal((HttpStatusCode.Conflict, "quote.stale"), (problem.Status, problem.Code));
        var parameters = problem.Body.GetProperty("params");
        Assert.Equal("scope_changed", parameters.GetProperty("reason").GetString());
        Assert.NotEqual(ready.QuoteId, parameters.GetProperty("quote").GetProperty("quoteId").GetGuid());
        Assert.Equal(5834, parameters.GetProperty("quote").GetProperty("countedProducts").GetInt32());
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM billing.orders WHERE shop_id = $1", ready.ShopId));
        Assert.Empty(factory.Stripe.Calls);
    }

    [Fact]
    public async Task NewPriceList_Is409Stale_WithTheQuoteOfTheNewList()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;
        var newer = await BillingSeed.PublishedAsync(ready.Market, validFrom: DateTimeOffset.UtcNow.AddMinutes(-5), monthly: new Dictionary<string, decimal> { ["t20000"] = 65 });

        using var response = await OrderAsync(ready);

        var problem = await ApiClient.ProblemAsync(response);
        Assert.Equal((HttpStatusCode.Conflict, "quote.stale"), (problem.Status, problem.Code));
        var parameters = problem.Body.GetProperty("params");
        Assert.Equal("price_list_changed", parameters.GetProperty("reason").GetString());
        Assert.Equal(newer, parameters.GetProperty("quote").GetProperty("priceListId").GetGuid());
        Assert.Equal(65m, parameters.GetProperty("quote").GetProperty("monitoringMonthlyNet").GetDecimal());
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM billing.orders WHERE shop_id = $1", ready.ShopId));
    }

    [Fact]
    public async Task Checkout_HasTheSnapshotPrices_TheTrial_TheMetadataAndTheSentence()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;
        using var created = await OrderAsync(ready);
        var id = (await ApiClient.JsonAsync(created)).GetProperty("id").GetGuid();
        var before = DateTimeOffset.UtcNow;

        var checkout = await CheckoutAsync(ready, id);

        var request = Assert.Single(factory.Stripe.SessionRequests.Values);
        var key = ready.PriceListId.ToString("N")[..8];
        Assert.Equal(($"price_{key}_t20000_analysis", $"price_{key}_t20000_monthly", (string?)null), (request.AnalysisPriceId, request.MonitoringPriceId, request.CouponId));
        Assert.Equal("sk", request.Locale);
        Assert.Equal(ready.Owner.TenantId.ToString("D"), request.Metadata["tenant_id"]);
        Assert.Equal(ready.ShopId.ToString("D"), request.Metadata["shop_id"]);
        Assert.Equal(id.ToString("D"), request.Metadata["order_id"]);
        Assert.InRange(request.TrialEnd, before.AddMonths(1).AddMinutes(-1), DateTimeOffset.UtcNow.AddMonths(1).AddMinutes(1));
        Assert.InRange(request.ExpiresAt, before.AddMinutes(59), DateTimeOffset.UtcNow.AddMinutes(61));
        var local = TimeZoneInfo.ConvertTime(request.TrialEnd, TimeZoneInfo.FindSystemTimeZoneById("Europe/Bratislava"));
        Assert.Contains($"Prvá mesačná platba za sledovanie 59\u00A0€ bez DPH bude {local.Day}. {local.Month}. {local.Year}.", request.SubmitMessage, StringComparison.Ordinal);
        Assert.StartsWith("https://checkout.stripe.test/", checkout.GetProperty("url").GetString(), StringComparison.Ordinal);
        var customer = Assert.Single(factory.Stripe.CustomerRequests.Values);
        Assert.Equal(("sk", "SK"), (customer.PreferredLocale, customer.Address.Country));
        var row = Assert.Single(await AdminRowsAsync("SELECT status, stripe_checkout_session_id, checkout_attempt FROM billing.orders WHERE id = $1", id));
        Assert.Equal(new object?[] { "checkout_open", Assert.Single(factory.Stripe.Sessions.Keys), 1 }, row);
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'order.checkout_started' AND entity_id = $1", id.ToString("D")));
    }

    [Fact]
    public async Task ReturnBeforeTheWebhook_IsAwaitingConfirmation_AndTheRunStillWaits()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;
        using var created = await OrderAsync(ready);
        var order = await ApiClient.JsonAsync(created);
        var id = order.GetProperty("id").GetGuid();
        await CheckoutAsync(ready, id);
        var session = Assert.Single(factory.Stripe.Sessions.Keys);

        var returned = await GetJsonAsync(ready.Owner, $"/api/t/{ready.Owner.TenantId}/orders/{id}?sessionId={session}");
        var plain = await GetJsonAsync(ready.Owner, $"/api/t/{ready.Owner.TenantId}/orders/{id}");
        var foreign = await GetJsonAsync(ready.Owner, $"/api/t/{ready.Owner.TenantId}/orders/{id}?sessionId=cs_test_other");

        Assert.Equal(("checkout_open", true), (returned.GetProperty("status").GetString(), returned.GetProperty("awaitingConfirmation").GetBoolean()));
        Assert.False(plain.GetProperty("awaitingConfirmation").GetBoolean());
        Assert.False(foreign.GetProperty("awaitingConfirmation").GetBoolean());
        Assert.Equal("checkout_open", await AdminScalarAsync<string>("SELECT status FROM billing.orders WHERE id = $1", id));
        Assert.NotEqual("crawling", await AdminScalarAsync<string>("SELECT status FROM checks.runs WHERE id = $1", order.GetProperty("runId").GetGuid()));
    }

    [Fact]
    public async Task WithoutCompanyId_Is422_AndWithoutAnOrder()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;
        await AdminAsync("UPDATE iam.tenants SET ico = NULL WHERE id = $1", ready.Owner.TenantId);

        using var response = await OrderAsync(ready);

        var problem = await ApiClient.ProblemAsync(response);
        Assert.Equal(((HttpStatusCode)422, "billing.company_id_required"), (problem.Status, problem.Code));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM billing.orders WHERE shop_id = $1", ready.ShopId));
    }

    [Fact]
    public async Task BuyerOutsideTheEu_Is422Undetermined_AndAuditedForTheAccountant()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;
        await AdminAsync("UPDATE iam.tenants SET country_code = 'CH' WHERE id = $1", ready.Owner.TenantId);

        using var response = await OrderAsync(ready);

        var problem = await ApiClient.ProblemAsync(response);
        Assert.Equal(((HttpStatusCode)422, "billing.tax_treatment_undetermined"), (problem.Status, problem.Code));
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.audit_log WHERE tenant_id = $1 AND action = 'billing.tax_treatment_undetermined' AND entity_id = $2", ready.Owner.TenantId, ready.ShopId.ToString("D")));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM billing.orders WHERE shop_id = $1", ready.ShopId));
    }

    [Fact]
    public async Task VatIdBeingVerified_RefusesTheCheckoutWith409()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;
        using var created = await OrderAsync(ready);
        var id = (await ApiClient.JsonAsync(created)).GetProperty("id").GetGuid();
        await AdminAsync("UPDATE iam.tenants SET country_code = 'CZ', ic_dph = 'CZ12345678', tax_id_status = 'pending' WHERE id = $1", ready.Owner.TenantId);

        using var response = await ready.Owner.Browser.PostAsync($"/api/t/{ready.Owner.TenantId}/orders/{id}/checkout");

        var problem = await ApiClient.ProblemAsync(response);
        Assert.Equal((HttpStatusCode.Conflict, "billing.tax_id_pending"), (problem.Status, problem.Code));
        Assert.Equal(0, factory.Stripe.Count("CreateCheckoutSessionAsync"));
    }

    [Fact]
    public async Task VatIdNotYetSent_StartsTheVerificationInStripe_AndAfterItTheOrderIsPaidWithReverseCharge()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;
        await AdminAsync("UPDATE iam.tenants SET country_code = 'CZ', ic_dph = 'CZ 12345678', tax_id_status = 'none' WHERE id = $1", ready.Owner.TenantId);

        using var created = await OrderAsync(ready);
        var order = await ApiClient.JsonAsync(created);
        var id = order.GetProperty("id").GetGuid();
        using var waiting = await ready.Owner.Browser.PostAsync($"/api/t/{ready.Owner.TenantId}/orders/{id}/checkout");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("pending_verification", order.GetProperty("taxTreatment").GetString());
        var problem = await ApiClient.ProblemAsync(waiting);
        Assert.Equal((HttpStatusCode.Conflict, "billing.tax_id_pending"), (problem.Status, problem.Code));
        var customer = await AdminScalarAsync<string>("SELECT stripe_customer_id FROM iam.tenants WHERE id = $1", ready.Owner.TenantId);
        Assert.Equal("CZ12345678", Assert.Single(factory.Stripe.Customers[customer!].TaxIds).Value);
        Assert.Equal("pending", await AdminScalarAsync<string>("SELECT tax_id_status FROM iam.tenants WHERE id = $1", ready.Owner.TenantId));
        Assert.Equal(0, factory.Stripe.Count("CreateCheckoutSessionAsync"));

        // customer.tax_id.updated with „verified“ (StripeEventProcessorTests.VerifiedVatId_IsStoredForTheTenant).
        await AdminAsync("UPDATE iam.tenants SET tax_id_status = 'verified', tax_id_verified_at = now() WHERE id = $1", ready.Owner.TenantId);
        await CheckoutAsync(ready, id);

        Assert.Equal(1, factory.Stripe.Count("CreateTaxIdAsync"));
        Assert.Equal(new object?[] { "reverse_charge", 0m, 0m, 199m, "checkout_open" }, Assert.Single(await AdminRowsAsync(
            "SELECT tax_treatment, vat_rate, vat_amount, amount_gross, status FROM billing.orders WHERE id = $1", id)));
    }

    [Fact]
    public async Task IndividualOffer_IsNotPayable()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory, otherPages: 40000);
        using var _ = ready.Owner;

        using var response = await OrderAsync(ready);

        var problem = await ApiClient.ProblemAsync(response);
        Assert.Equal((HttpStatusCode.Conflict, "billing.quote_not_payable"), (problem.Status, problem.Code));
        Assert.Equal("billing.fair_use_exceeded", problem.Body.GetProperty("params").GetProperty("reason").GetString());
    }

    [Fact]
    public async Task OutdatedTerms_Is409WithTheCurrentVersion()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;

        using var response = await OrderAsync(ready, terms: "test-terms-0");

        var problem = await ApiClient.ProblemAsync(response);
        Assert.Equal((HttpStatusCode.Conflict, "billing.terms_outdated"), (problem.Status, problem.Code));
        Assert.Equal("test-terms-1", problem.Body.GetProperty("params").GetProperty("current").GetString());
    }

    [Fact]
    public async Task OrderOfAnotherTenant_Is404Everywhere()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;
        using var created = await OrderAsync(ready);
        var id = (await ApiClient.JsonAsync(created)).GetProperty("id").GetGuid();
        using var other = await People.OwnerAsync(factory);

        using var read = await other.Browser.GetAsync($"/api/t/{other.TenantId}/orders/{id}");
        using var checkout = await other.Browser.PostAsync($"/api/t/{other.TenantId}/orders/{id}/checkout");

        Assert.Equal((HttpStatusCode.NotFound, "billing.order_not_found"), ((await ApiClient.ProblemAsync(read)).Status, (await ApiClient.ProblemAsync(read)).Code));
        Assert.Equal((HttpStatusCode.NotFound, "billing.order_not_found"), ((await ApiClient.ProblemAsync(checkout)).Status, (await ApiClient.ProblemAsync(checkout)).Code));
        Assert.Equal("created", await AdminScalarAsync<string>("SELECT status FROM billing.orders WHERE id = $1", id));
        Assert.Equal(0, factory.Stripe.Count("CreateCheckoutSessionAsync"));
    }

    /// <summary>The e-shop, the tenant and the price list ready to be ordered, with the quote the customer saw.</summary>
    internal sealed record Ready(Person Owner, Guid ShopId, string Market, Guid PriceListId, Guid QuoteId, string ScopeHash);

    internal static async Task<Ready> ReadyAsync(ApiFactory factory, int? otherPages = null)
    {
        var (owner, shopId) = await PriceQuoteTests.BylinkovoAsync(factory);
        var market = await BillingSeed.MarketAsync();
        await BillingSeed.UseMarketAsync(owner.TenantId, market);
        var list = await BillingSeed.PublishedAsync(market, validFrom: DateTimeOffset.UtcNow.AddHours(-2));
        await AdminAsync(
            "UPDATE iam.tenants SET legal_name = 'Bylinkovo s.r.o.', ico = '12345678', street = 'Hlavná 1', city = 'Trenčín', postal_code = '91101', country_code = 'SK' WHERE id = $1",
            owner.TenantId);
        await AdminAsync("UPDATE shop.shops SET ownership_verified_at = now(), verification_method = 'dns' WHERE id = $1", shopId);
        if (otherPages is { } pages)
        {
            await AdminAsync("UPDATE checks.runs SET estimate = jsonb_set(estimate, '{basis,versions,0,other_pages}', to_jsonb($2)) WHERE shop_id = $1", shopId, pages);
        }

        var ready = new Ready(owner, shopId, market, list, Guid.Empty, string.Empty);
        await MarketsAsync(ready, "sk", "cz");
        var quote = await PriceQuoteTests.QuoteAsync(owner, shopId, "sk", "cz");
        return ready with
        {
            QuoteId = quote.GetProperty("price").GetProperty("quoteId").GetGuid(),
            ScopeHash = quote.GetProperty("scope").GetProperty("scopeHash").GetString()!,
        };
    }

    internal static async Task MarketsAsync(Ready ready, params string[] active)
    {
        using var response = await ready.Owner.Browser.PutAsync($"/api/t/{ready.Owner.TenantId}/shops/{ready.ShopId}/markets", new { active });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    internal static Task<HttpResponseMessage> OrderAsync(Ready ready, string terms = "test-terms-1") =>
        ready.Owner.Browser.PostAsync($"/api/t/{ready.Owner.TenantId}/shops/{ready.ShopId}/orders",
            new { quoteId = ready.QuoteId, scopeHash = ready.ScopeHash, termsVersion = terms });

    internal static async Task<JsonElement> CheckoutAsync(Ready ready, Guid orderId)
    {
        using var response = await ready.Owner.Browser.PostAsync($"/api/t/{ready.Owner.TenantId}/orders/{orderId}/checkout");
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await ApiClient.JsonAsync(response);
    }
}
