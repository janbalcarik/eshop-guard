using System.Net;
using System.Text;
using System.Text.Json;
using EshopGuard.Api.Tests.Findings;
using EshopGuard.Api.Tests.Shops;
using EshopGuard.Billing.Jobs;
using EshopGuard.Billing.Stripe;
using static EshopGuard.Api.Tests.Billing.OrderFlowTests;

namespace EshopGuard.Api.Tests.Billing;

/// <summary>
/// The monitoring after the first order (change 12, tasks 7.3–7.5, 7.7 and 7.8; requirements „Předplatné sledování po e-shopech“
/// and „Jedna platební karta na účet“): payment with the card of the account, the change of the card, canceling, resuming and
/// starting again, the data after the end and the page „Predplatné a platby“ with the three e-shops of the design Billing.
/// </summary>
public sealed class SubscriptionFlowTests : ShopTestBase
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n%%EOF\n");

    [Fact]
    public async Task SavedCard_StartsTheSubscriptionOfTheOrder_WithTheTrial_Once()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;
        var id = await OrderIdAsync(ready);

        using var missing = await PayAsync(ready, id);
        var problem = await ApiClient.ProblemAsync(missing);
        Assert.Equal((HttpStatusCode.Conflict, "billing.saved_card_missing"), (problem.Status, problem.Code));
        Assert.Equal(0, factory.Stripe.Count(nameof(IStripeGateway.CreateSubscriptionAsync)));

        var (_, card) = await CardAsync(factory, ready.Owner.TenantId);
        var before = DateTimeOffset.UtcNow;
        var paid = await PayJsonAsync(ready, id);
        var again = await PayJsonAsync(ready, id);

        Assert.Equal(("processing", JsonValueKind.Null), (paid.GetProperty("status").GetString(), paid.GetProperty("clientSecret").ValueKind));
        Assert.Equal("processing", again.GetProperty("status").GetString());
        var (subscription, request) = Assert.Single(factory.Stripe.SubscriptionRequests);
        var key = ready.PriceListId.ToString("N")[..8];
        Assert.Equal(($"price_{key}_t20000_analysis", $"price_{key}_t20000_monthly", card), (request.AnalysisPriceId, request.MonitoringPriceId, request.DefaultPaymentMethodId));
        Assert.Equal(id.ToString("D"), request.Metadata["order_id"]);
        Assert.InRange(request.TrialEnd!.Value, before.AddMonths(1).AddMinutes(-1), DateTimeOffset.UtcNow.AddMonths(1).AddMinutes(1));
        Assert.Equal(new object?[] { "checkout_open", subscription, 1 }, Assert.Single(await AdminRowsAsync(
            "SELECT status, stripe_subscription_id, checkout_attempt FROM billing.orders WHERE id = $1", id)));
        Assert.Equal([$"expire-order:{id:N}:1:expire", $"expire-order:{id:N}:1:settle"], (await AdminRowsAsync(
            "SELECT dedupe_key FROM ops.jobs WHERE kind = $1 AND tenant_id = $2 ORDER BY dedupe_key", BillingJobs.ExpireOrderKind, ready.Owner.TenantId)).Select(r => (string)r[0]!));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'order.saved_card_payment_started' AND entity_id = $1", id.ToString("D")));
    }

    [Fact]
    public async Task SavedCardNeedingThreeDSecure_AnswersTheClientSecret_AndAnOpenCheckoutIsClosedFirst()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;
        var id = await OrderIdAsync(ready);
        await CheckoutAsync(ready, id);
        var session = Assert.Single(factory.Stripe.Sessions.Keys);
        await CardAsync(factory, ready.Owner.TenantId);
        factory.Stripe.NewSubscriptionStatus = "incomplete";

        var paid = await PayJsonAsync(ready, id);
        var again = await PayJsonAsync(ready, id);

        var subscription = Assert.Single(factory.Stripe.SubscriptionRequests.Keys);
        Assert.Equal(("requires_action", "pi_secret_" + subscription), (paid.GetProperty("status").GetString(), paid.GetProperty("clientSecret").GetString()));
        Assert.Equal(paid.GetProperty("clientSecret").GetString(), again.GetProperty("clientSecret").GetString());
        Assert.Equal(1, factory.Stripe.Count(nameof(IStripeGateway.CreateSubscriptionAsync)));
        Assert.Equal(1, factory.Stripe.Count(nameof(IStripeGateway.ExpireCheckoutSessionAsync)));
        Assert.Equal(session, factory.Stripe.Calls.Single(c => c.Method == nameof(IStripeGateway.ExpireCheckoutSessionAsync)).Argument);
        Assert.Equal(new object?[] { "checkout_open", null, subscription, 2 }, Assert.Single(await AdminRowsAsync(
            "SELECT status, stripe_checkout_session_id, stripe_subscription_id, checkout_attempt FROM billing.orders WHERE id = $1", id)));
    }

    [Fact]
    public async Task CardChange_AnswersOnlyTheUrlOfThePortal_OrTheSecretOfTheSetupIntent()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;
        var tenant = ready.Owner.TenantId;

        using var missing = await ready.Owner.Browser.PostAsync($"/api/t/{tenant}/billing/card/portal-session");
        var problem = await ApiClient.ProblemAsync(missing);
        Assert.Equal((HttpStatusCode.Conflict, "billing.saved_card_missing"), (problem.Status, problem.Code));

        var (customer, _) = await CardAsync(factory, tenant);
        using var portal = await ready.Owner.Browser.PostAsync($"/api/t/{tenant}/billing/card/portal-session");
        using var setup = await ready.Owner.Browser.PostAsync($"/api/t/{tenant}/billing/card/setup-intent");

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (portal.StatusCode, setup.StatusCode));
        var text = await portal.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var body = JsonDocument.Parse(text).RootElement;
        Assert.Equal(["url"], body.EnumerateObject().Select(p => p.Name));
        Assert.StartsWith("https://billing.stripe.test/", body.GetProperty("url").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("cus_", text, StringComparison.Ordinal);
        Assert.DoesNotContain(customer, text, StringComparison.Ordinal);
        var secret = (await ApiClient.JsonAsync(setup)).GetProperty("clientSecret").GetString();
        var intent = Assert.Single(factory.Stripe.SetupIntents.Values);
        Assert.Equal(intent.Id + "_secret_test", secret);
        Assert.Equal((customer, StripeSetupIntentState.AccountCard), (intent.CustomerId, intent.Metadata["purpose"]));
    }

    [Fact]
    public async Task CancelDuringTheTrial_EndsWithTheTrial_AndCanBeResumedBeforeThen()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;
        var path = $"/api/t/{ready.Owner.TenantId}/shops/{ready.ShopId}/subscription";

        using var none = await ready.Owner.Browser.PostAsync(path + "/cancel");
        var problem = await ApiClient.ProblemAsync(none);
        Assert.Equal((HttpStatusCode.NotFound, "billing.subscription_not_found"), (problem.Status, problem.Code));

        var trialEnd = DateTimeOffset.UtcNow.AddDays(20);
        var (subscription, stripeId) = await SubscribedAsync(factory, ready.Owner.TenantId, ready.ShopId, ready.PriceListId, "t20000", 59m, "trialing", trialEnd, trialEnd);
        var schedule = (await factory.Stripe.CreateScheduleFromSubscriptionAsync(stripeId, "schedule-test", CancellationToken.None)).Id;
        await AdminAsync("UPDATE billing.subscriptions SET stripe_schedule_id = $2, schedule_hash = 'test' WHERE id = $1", subscription, schedule);
        var canceled = await PostJsonAsync(ready.Owner, path + "/cancel");
        await PostJsonAsync(ready.Owner, path + "/cancel");
        var ending = ShopRow(await OverviewAsync(ready.Owner), ready.ShopId);

        Assert.Equal(("trialing", true), (canceled.GetProperty("status").GetString(), canceled.GetProperty("cancelAtPeriodEnd").GetBoolean()));
        Assert.True(factory.Stripe.Subscriptions[stripeId].CancelAtPeriodEnd);
        Assert.Equal(1, factory.Stripe.Count(nameof(IStripeGateway.SetCancelAtPeriodEndAsync)));
        Assert.Equal(("released", null), (factory.Stripe.Schedules[schedule].Status, factory.Stripe.Subscriptions[stripeId].ScheduleId));
        Assert.Equal(1, factory.Stripe.Count(nameof(IStripeGateway.ReleaseScheduleAsync)));
        Assert.Equal(new object?[] { null, null }, Assert.Single(await AdminRowsAsync(
            "SELECT stripe_schedule_id, schedule_hash FROM billing.subscriptions WHERE id = $1", subscription)));
        Assert.Equal("ending", ending.GetProperty("status").GetString());
        Assert.Equal(trialEnd, ending.GetProperty("periodEnd").GetDateTimeOffset(), TimeSpan.FromSeconds(1));
        Assert.Equal(JsonValueKind.Null, ending.GetProperty("nextPaymentAt").ValueKind);

        using var running = await ready.Owner.Browser.PostAsync(path, new { confirm = true, amount = 59m });
        problem = await ApiClient.ProblemAsync(running);
        Assert.Equal((HttpStatusCode.Conflict, "billing.subscription_already_running"), (problem.Status, problem.Code));

        var resumed = await PostJsonAsync(ready.Owner, path + "/resume");

        Assert.False(resumed.GetProperty("cancelAtPeriodEnd").GetBoolean());
        Assert.False(factory.Stripe.Subscriptions[stripeId].CancelAtPeriodEnd);
        Assert.Equal("trial", ShopRow(await OverviewAsync(ready.Owner), ready.ShopId).GetProperty("status").GetString());
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'subscription.cancel_requested' AND entity_id = $1", subscription.ToString("D")));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'subscription.resumed' AND entity_id = $1", subscription.ToString("D")));
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.jobs WHERE kind = 'billing.compose_schedule' AND dedupe_key LIKE $1", $"schedule:{subscription:N}:resume:%"));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind = 'billing.evaluate_tiers' AND shop_id = $1", ready.ShopId));

        await AdminAsync("UPDATE billing.subscriptions SET status = 'canceled', canceled_at = now(), pause_reason = 'canceled' WHERE id = $1", subscription);
        using var late = await ready.Owner.Browser.PostAsync(path + "/resume");
        using var again = await ready.Owner.Browser.PostAsync(path + "/cancel");

        problem = await ApiClient.ProblemAsync(late);
        Assert.Equal((HttpStatusCode.Conflict, "billing.subscription_not_resumable"), (problem.Status, problem.Code));
        problem = await ApiClient.ProblemAsync(again);
        Assert.Equal((HttpStatusCode.Conflict, "billing.subscription_not_cancelable"), (problem.Status, problem.Code));
        Assert.Equal(2, factory.Stripe.Count(nameof(IStripeGateway.SetCancelAtPeriodEndAsync)));
    }

    [Fact]
    public async Task StartAgain_AsksForTheConfirmationOfTheAmount_ThenStartsWithoutTrialAndAnalysis()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;
        var path = $"/api/t/{ready.Owner.TenantId}/shops/{ready.ShopId}/subscription";
        var (_, card) = await CardAsync(factory, ready.Owner.TenantId);
        var (ended, _) = await SubscribedAsync(factory, ready.Owner.TenantId, ready.ShopId, ready.PriceListId, "t2000", 19m, "canceled", DateTimeOffset.UtcNow.AddDays(-3));
        await AdminAsync("UPDATE shop.shops SET status = 'canceled', tier_code = 't2000' WHERE id = $1", ready.ShopId);
        var before = DateTimeOffset.UtcNow;

        var asked = await PostJsonAsync(ready.Owner, path, new { });
        var wrong = await PostJsonAsync(ready.Owner, path, new { confirm = true, amount = 18m });

        Assert.Equal(("confirm_required", "t2000", 19m, "EUR"), (asked.GetProperty("status").GetString(), asked.GetProperty("tierCode").GetString(),
            asked.GetProperty("amount").GetDecimal(), asked.GetProperty("currency").GetString()));
        Assert.Equal(JsonValueKind.Null, asked.GetProperty("discountPercent").ValueKind);
        Assert.InRange(asked.GetProperty("date").GetDateTimeOffset(), before.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Equal("confirm_required", wrong.GetProperty("status").GetString());
        Assert.Equal(0, factory.Stripe.Count(nameof(IStripeGateway.CreateSubscriptionAsync)));

        var started = await PostJsonAsync(ready.Owner, path, new { confirm = true, amount = 19m });
        var twice = await PostJsonAsync(ready.Owner, path, new { confirm = true, amount = 19m });

        Assert.Equal(("processing", "processing"), (started.GetProperty("status").GetString(), twice.GetProperty("status").GetString()));
        var (subscription, request) = Assert.Single(factory.Stripe.SubscriptionRequests);
        var key = ready.PriceListId.ToString("N")[..8];
        Assert.Equal(($"price_{key}_t2000_monthly", (string?)null, (DateTimeOffset?)null, (string?)null, card),
            (request.MonitoringPriceId, request.AnalysisPriceId, request.TrialEnd, request.CouponId, request.DefaultPaymentMethodId));
        Assert.Equal(ready.ShopId.ToString("D"), request.Metadata["shop_id"]);
        Assert.False(request.Metadata.ContainsKey("order_id"));
        Assert.Equal("active", factory.Stripe.Subscriptions[subscription].Status);
        Assert.Equal(2L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'subscription.start_again_requested' AND entity_id = $1", ready.ShopId.ToString("D")));
        Assert.Equal("canceled", await AdminScalarAsync<string>("SELECT status FROM billing.subscriptions WHERE id = $1", ended));
    }

    [Fact]
    public async Task StartAgainNeedingThreeDSecure_AnswersTheSameSecretUntilItIsConfirmed()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;
        var path = $"/api/t/{ready.Owner.TenantId}/shops/{ready.ShopId}/subscription";
        await CardAsync(factory, ready.Owner.TenantId);
        await SubscribedAsync(factory, ready.Owner.TenantId, ready.ShopId, ready.PriceListId, "t20000", 59m, "canceled", DateTimeOffset.UtcNow.AddDays(-3));
        factory.Stripe.NewSubscriptionStatus = "incomplete";

        var started = await PostJsonAsync(ready.Owner, path, new { confirm = true, amount = 59m });
        var stripeId = Assert.Single(factory.Stripe.SubscriptionRequests.Keys);
        await AdminAsync(
            """
            INSERT INTO billing.subscriptions (tenant_id, shop_id, stripe_subscription_id, status, interval, price_list_id, tier_code, unit_price, created_at, updated_at)
            VALUES ($1, $2, $3, 'incomplete', 'month', $4, 't20000', 59, now(), now())
            """, ready.Owner.TenantId, ready.ShopId, stripeId, ready.PriceListId);
        var again = await PostJsonAsync(ready.Owner, path, new { confirm = true, amount = 59m });

        Assert.Equal(("requires_action", "pi_secret_" + stripeId), (started.GetProperty("status").GetString(), started.GetProperty("clientSecret").GetString()));
        Assert.Equal(("requires_action", "pi_secret_" + stripeId), (again.GetProperty("status").GetString(), again.GetProperty("clientSecret").GetString()));
        Assert.Equal(1, factory.Stripe.Count(nameof(IStripeGateway.CreateSubscriptionAsync)));
    }

    [Fact]
    public async Task EndedMonitoring_KeepsTheFindingsAndTheEvidenceReadable()
    {
        var (factory, owner, data) = await FindingsTestBase.BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        using var uploaded = await EvidenceTests.UploadAsync(owner, EvidenceTests.Metadata(), Pdf, "certifikat.pdf");
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var evidence = (await ApiClient.JsonAsync(uploaded)).GetProperty("id").GetGuid();
        var market = await BillingSeed.MarketAsync();
        var list = await BillingSeed.PublishedAsync(market);
        var (subscription, _) = await SubscribedAsync(factory, owner.TenantId, data.ShopId, list, "t2000", 19m, "canceled", DateTimeOffset.UtcNow.AddDays(-1));
        await AdminAsync("UPDATE billing.subscriptions SET canceled_at = now(), pause_reason = 'canceled' WHERE id = $1", subscription);
        await AdminAsync("UPDATE shop.shops SET status = 'canceled' WHERE id = $1", data.ShopId);
        var shop = FindingsTestBase.S(owner, data.ShopId);

        var findings = await GetJsonAsync(owner, shop + "/findings?limit=100");
        var detail = await GetJsonAsync(owner, $"{shop}/findings/{data.Findings["group"]}");
        var item = await GetJsonAsync(owner, $"/api/t/{owner.TenantId}/evidence/{evidence}");
        using var file = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}/evidence/{evidence}/file");

        Assert.NotEqual(0, findings.GetProperty("items").GetArrayLength());
        Assert.Equal(data.Findings["group"], detail.GetProperty("finding").GetProperty("findingId").GetGuid());
        Assert.Equal(evidence, item.GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Redirect, file.StatusCode);
        Assert.Equal("ended", ShopRow(await OverviewAsync(owner), data.ShopId).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Overview_HasTheThreeShopsOfTheDesign_TheCard_TheTiersAndTheMonthlySumAfterDiscounts()
    {
        await using var factory = Factory();
        var ready = await ReadyAsync(factory);
        using var _ = ready.Owner;
        var tenant = ready.Owner.TenantId;
        await AdminAsync(
            "INSERT INTO billing.volume_discounts (price_list_id, from_shop_number, percent, stripe_coupon_id, stripe_mode, created_at, updated_at) VALUES ($1, 3, 10, $2, 'test', now(), now())",
            ready.PriceListId, $"coupon_{ready.PriceListId.ToString("N")[..8]}_3");
        await CardAsync(factory, tenant);
        var order = await OrderIdAsync(ready);
        var paidAt = DateTimeOffset.UtcNow.AddDays(-10);
        await AdminAsync("UPDATE billing.orders SET status = 'paid', paid_at = $2 WHERE id = $1", order, paidAt);
        var periodEnd = DateTimeOffset.UtcNow.AddDays(20);
        var (main, _) = await SubscribedAsync(factory, tenant, ready.ShopId, ready.PriceListId, "t20000", 59m, "active", periodEnd, ordinal: 1, orderId: order);
        var change = DateTimeOffset.UtcNow.AddDays(40);
        await AdminAsync(
            """
            INSERT INTO billing.subscription_changes (subscription_id, kind, "from", "to", effective_at, status, tenant_id, created_at, updated_at)
            VALUES ($1, 'tier', '{"tier_code": "t20000", "unit_price": 59}'::jsonb, '{"tier_code": "t5000", "unit_price": 29}'::jsonb, $2, 'scheduled', $3, now(), now())
            """, main, change, tenant);
        var gifts = await ShopRowAsync(tenant, "darceky", 200, "t500");
        await SubscribedAsync(factory, tenant, gifts, ready.PriceListId, "t500", 9m, "active", periodEnd, ordinal: 2);
        var czech = await ShopRowAsync(tenant, "bylinkovocz", 1460, "t2000");
        var trialEnd = DateTimeOffset.UtcNow.AddDays(17);
        await SubscribedAsync(factory, tenant, czech, ready.PriceListId, "t2000", 19m, "trialing", trialEnd, trialEnd, ordinal: 3, discount: 10m);
        var never = await ShopRowAsync(tenant, "nikdy", 50, "t500");
        var (pending, _) = await SubscribedAsync(factory, tenant, never, ready.PriceListId, "t500", 9m, "canceled", periodEnd);
        await AdminAsync("UPDATE billing.subscriptions SET pause_reason = 'not_started' WHERE id = $1", pending);

        var overview = await OverviewAsync(ready.Owner);

        Assert.Equal("EUR", overview.GetProperty("currency").GetString());
        var shops = overview.GetProperty("shops").EnumerateArray().ToList();
        Assert.Equal([ready.ShopId, gifts, czech], shops.Select(s => s.GetProperty("shopId").GetGuid()));
        var first = shops[0];
        Assert.Equal(["SK", "CZ"], first.GetProperty("markets").EnumerateArray().Select(m => m.GetString()!.ToUpperInvariant()));
        Assert.Equal((11668, "t20000", 20000, 199m, "active", 59m, 1), (first.GetProperty("productCount").GetInt32(), first.GetProperty("tierCode").GetString(),
            first.GetProperty("tierMaxProducts").GetInt32(), first.GetProperty("analysisNet").GetDecimal(), first.GetProperty("status").GetString(),
            first.GetProperty("monthlyNet").GetDecimal(), first.GetProperty("shopOrdinal").GetInt32()));
        Assert.Equal(paidAt, first.GetProperty("analysisPaidAt").GetDateTimeOffset(), TimeSpan.FromSeconds(1));
        Assert.Equal(periodEnd, first.GetProperty("nextPaymentAt").GetDateTimeOffset(), TimeSpan.FromSeconds(1));
        Assert.Equal(59m, first.GetProperty("nextPaymentNet").GetDecimal());
        var pendingChange = first.GetProperty("pendingChange");
        Assert.Equal(["tier"], pendingChange.GetProperty("kinds").EnumerateArray().Select(k => k.GetString()));
        Assert.Equal(("t5000", 29m), (pendingChange.GetProperty("tierCode").GetString(), pendingChange.GetProperty("monthlyNet").GetDecimal()));
        Assert.Equal(change, pendingChange.GetProperty("effectiveAt").GetDateTimeOffset(), TimeSpan.FromSeconds(1));
        var second = shops[1];
        Assert.Equal((200, "t500", 500, "active", 9m, 2), (second.GetProperty("productCount").GetInt32(), second.GetProperty("tierCode").GetString(),
            second.GetProperty("tierMaxProducts").GetInt32(), second.GetProperty("status").GetString(), second.GetProperty("monthlyNet").GetDecimal(),
            second.GetProperty("shopOrdinal").GetInt32()));
        Assert.Equal(JsonValueKind.Null, second.GetProperty("analysisNet").ValueKind);
        Assert.Equal(JsonValueKind.Null, second.GetProperty("pendingChange").ValueKind);
        var third = shops[2];
        Assert.Equal((1460, "t2000", "trial", 17.10m, 10m, 3), (third.GetProperty("productCount").GetInt32(), third.GetProperty("tierCode").GetString(),
            third.GetProperty("status").GetString(), third.GetProperty("monthlyNet").GetDecimal(), third.GetProperty("discountPercent").GetDecimal(),
            third.GetProperty("shopOrdinal").GetInt32()));
        Assert.Equal(trialEnd, third.GetProperty("trialEnd").GetDateTimeOffset(), TimeSpan.FromSeconds(1));
        Assert.Equal(trialEnd, third.GetProperty("nextPaymentAt").GetDateTimeOffset(), TimeSpan.FromSeconds(1));
        Assert.Equal(17.10m, third.GetProperty("nextPaymentNet").GetDecimal());
        var card = overview.GetProperty("card");
        Assert.Equal(("visa", "4242", 8, 2028), (card.GetProperty("brand").GetString(), card.GetProperty("last4").GetString(),
            card.GetProperty("expMonth").GetInt32(), card.GetProperty("expYear").GetInt32()));
        Assert.Equal(["t500", "t2000", "t5000", "t20000", "custom"], overview.GetProperty("tiers").EnumerateArray().Select(t => t.GetProperty("code").GetString()));
        Assert.True(overview.GetProperty("tiers")[4].GetProperty("isCustom").GetBoolean());
        var discount = Assert.Single(overview.GetProperty("volumeDiscounts").EnumerateArray());
        Assert.Equal((3, 10m), (discount.GetProperty("fromShopNumber").GetInt32(), discount.GetProperty("percent").GetDecimal()));
        Assert.Equal(85.10m, overview.GetProperty("monthlyTotal").GetDecimal());
        var text = overview.GetRawText();
        Assert.DoesNotContain("sub_", text, StringComparison.Ordinal);
        Assert.DoesNotContain("pm_", text, StringComparison.Ordinal);
        Assert.DoesNotContain("cus_", text, StringComparison.Ordinal);
    }

    private static async Task<Guid> OrderIdAsync(Ready ready)
    {
        using var created = await OrderAsync(ready);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await ApiClient.JsonAsync(created)).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> PayAsync(Ready ready, Guid orderId) =>
        ready.Owner.Browser.PostAsync($"/api/t/{ready.Owner.TenantId}/orders/{orderId}/pay-with-saved-card");

    private static async Task<JsonElement> PayJsonAsync(Ready ready, Guid orderId)
    {
        using var response = await PayAsync(ready, orderId);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await ApiClient.JsonAsync(response);
    }

    private static async Task<JsonElement> PostJsonAsync(Person person, string path, object? body = null)
    {
        using var response = body is null ? await person.Browser.PostAsync(path) : await person.Browser.PostAsync(path, body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await ApiClient.JsonAsync(response);
    }

    private static Task<JsonElement> OverviewAsync(Person person) => GetJsonAsync(person, $"/api/t/{person.TenantId}/billing/overview");

    private static JsonElement ShopRow(JsonElement overview, Guid shopId) =>
        Assert.Single(overview.GetProperty("shops").EnumerateArray(), s => s.GetProperty("shopId").GetGuid() == shopId);

    /// <summary>The customer of the tenant in Stripe with its default card VISA •••• 4242 (08/28), as the webhook of the first payment leaves it.</summary>
    private static async Task<(string Customer, string Card)> CardAsync(ApiFactory factory, Guid tenantId)
    {
        var customer = factory.Stripe.NewId("cus");
        var card = factory.Stripe.NewId("pm");
        factory.Stripe.Customers[customer] = new StripeCustomerState(customer, card, [], false, new Dictionary<string, string>());
        factory.Stripe.PaymentMethods[card] = new StripePaymentMethodState(card, customer, "visa", "4242", 8, 2028);
        await AdminAsync("UPDATE iam.tenants SET stripe_customer_id = $2 WHERE id = $1", tenantId, customer);
        await AdminAsync(
            "INSERT INTO billing.payment_methods (tenant_id, stripe_payment_method_id, brand, last4, exp_month, exp_year, is_default) VALUES ($1, $2, 'visa', '4242', 8, 2028, true)",
            tenantId, card);
        return (customer, card);
    }

    /// <summary>An e-shop of the tenant with monitoring only (no sample, no order).</summary>
    private static async Task<Guid> ShopRowAsync(Guid tenantId, string prefix, int products, string tier)
    {
        var id = Guid.CreateVersion7();
        var domain = NewDomain(prefix);
        await AdminAsync(
            """
            INSERT INTO shop.shops (id, tenant_id, domain, base_url, base_path, home_country, platform, source_mode, status, product_count, tier_code, created_at, updated_at)
            VALUES ($1, $2, $3, $4, '/', 'SK', 'shoptet', 'web', 'active', $5, $6, now(), now())
            """, id, tenantId, domain, $"https://{domain}/", products, tier);
        return id;
    }

    /// <summary>A subscription of the e-shop as the webhook stores it, and the same in the fake Stripe.</summary>
    private static async Task<(Guid Id, string StripeId)> SubscribedAsync(
        ApiFactory factory, Guid tenantId, Guid shopId, Guid priceListId, string tier, decimal unitPrice, string status, DateTimeOffset periodEnd,
        DateTimeOffset? trialEnd = null, int? ordinal = null, decimal? discount = null, Guid? orderId = null)
    {
        var id = Guid.CreateVersion7();
        var stripeId = factory.Stripe.NewId("sub");
        var price = $"price_{priceListId.ToString("N")[..8]}_{tier}_monthly";
        var start = periodEnd.AddMonths(-1);
        factory.Stripe.Subscriptions[stripeId] = new StripeSubscriptionState(stripeId, "cus_test", status, price, null, trialEnd, start, periodEnd, false, null, null, null,
            null, null, null, start, new Dictionary<string, string> { ["tenant_id"] = tenantId.ToString("D"), ["shop_id"] = shopId.ToString("D") });
        await AdminAsync(
            """
            INSERT INTO billing.subscriptions (id, tenant_id, shop_id, stripe_subscription_id, status, interval, price_list_id, tier_code, unit_price, stripe_price_id,
                current_period_start, current_period_end, trial_end, shop_ordinal, discount_percent, order_id, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, 'month', $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, now(), now())
            """, id, tenantId, shopId, stripeId, status, priceListId, tier, unitPrice, price, start, periodEnd, trialEnd, ordinal, discount, orderId);
        return (id, stripeId);
    }
}
