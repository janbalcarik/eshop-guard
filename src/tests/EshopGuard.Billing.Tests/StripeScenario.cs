using System.Text.Json.Nodes;
using EshopGuard.Billing.Stripe;
using EshopGuard.Billing.Webhooks;
using EshopGuard.Jobs.Runs;
using EshopGuard.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using static EshopGuard.Billing.Tests.BillingTestHost;

namespace EshopGuard.Billing.Tests;

/// <summary>
/// Shared scenes of the tests of the events of Stripe (change 12): an order paid in Checkout with its customer, subscription and
/// card in <see cref="FakeStripeGateway"/>, invoices, and events stored and processed as the worker does.
/// </summary>
internal static class StripeScenario
{
    /// <summary>An open order of a full analysis paid in Checkout: the tenant with its customer, the session, the subscription and the card in Stripe.</summary>
    internal sealed record Paying(
        Guid TenantId, Guid ShopId, Guid PriceListId, Guid OrderId, Guid RunId, string CustomerId, string SessionId, string SubscriptionId, string CardId,
        DateTimeOffset TrialEnd);

    internal static Task<BillingTestHost> HostAsync() => CreateAsync(configure: s => s.AddRunService());

    internal static async Task<Paying> PayingAsync(BillingTestHost host)
    {
        var now = host.Time.GetUtcNow();
        var market = await BillingData.MarketAsync();
        var list = await BillingData.PriceListAsync(market, "published");
        var key = list.ToString("N")[..8];
        var tenant = await BillingData.TenantAsync(market);
        var customer = host.Stripe.NewId("cus");
        host.Stripe.Customers[customer] = new StripeCustomerState(customer, null, [], false, new Dictionary<string, string>());
        await AdminAsync("UPDATE iam.tenants SET stripe_customer_id = $2 WHERE id = $1", tenant, customer);
        var shop = await BillingData.ShopAsync(tenant, "awaiting_payment");
        var run = Guid.CreateVersion7();
        await AdminAsync(
            """
            INSERT INTO checks.runs (id, tenant_id, shop_id, kind, trigger, status, priority, jurisdictions, modules, created_at, updated_at)
            VALUES ($1, $2, $3, 'full_analysis', 'user', 'awaiting_payment', 0, '{sk}', '{}', now(), now())
            """, run, tenant, shop);
        var session = host.Stripe.NewId("cs_test");
        var order = Guid.CreateVersion7();
        await AdminAsync(
            """
            INSERT INTO billing.orders (id, tenant_id, shop_id, kind, price_list_id, tier_code, amount_net, discount_amount, vat_rate, vat_amount, amount_gross, currency,
                status, stripe_checkout_session_id, run_id, checkout_attempt, monitoring_monthly, stripe_price_analysis, stripe_price_monitoring, tax_treatment,
                created_at, updated_at)
            VALUES ($1, $2, $3, 'analysis_with_trial', $4, 't2000', 69, 0, 23, 15.87, 84.87, 'EUR', 'checkout_open', $5, $6, 1, 19, $7, $8, 'domestic_vat', now(), now())
            """, order, tenant, shop, list, session, run, $"price_{key}_t2000_analysis", $"price_{key}_t2000_monthly");
        var metadata = new Dictionary<string, string>
        {
            ["tenant_id"] = tenant.ToString("D"),
            ["shop_id"] = shop.ToString("D"),
            ["order_id"] = order.ToString("D"),
        };
        var card = host.Stripe.NewId("pm");
        host.Stripe.PaymentMethods[card] = new StripePaymentMethodState(card, customer, "visa", "4242", 12, 2030);
        var subscription = host.Stripe.NewId("sub");
        var trialEnd = now.AddMonths(1);
        host.Stripe.Subscriptions[subscription] = new StripeSubscriptionState(subscription, customer, "trialing", $"price_{key}_t2000_monthly", null, trialEnd, now, trialEnd,
            false, null, null, null, card, host.Stripe.NewId("in"), null, now, metadata);
        host.Stripe.Sessions[session] = new StripeCheckoutSession(session, null, "complete", "paid", customer, subscription, now.AddHours(1), metadata);
        return new Paying(tenant, shop, list, order, run, customer, session, subscription, card, trialEnd);
    }

    /// <summary>The metadata of a subscription of the e-shop started outside the order (no <c>order_id</c>).</summary>
    internal static Dictionary<string, string> ShopMetadata(Paying paying) => new()
    {
        ["tenant_id"] = paying.TenantId.ToString("D"),
        ["shop_id"] = paying.ShopId.ToString("D"),
    };

    /// <summary>A paid (or open) invoice of the subscription in Stripe.</summary>
    internal static string Invoice(BillingTestHost host, Paying paying, long paid, string reason, long? total = null, string status = "paid")
    {
        var id = host.Stripe.NewId("in");
        var now = host.Time.GetUtcNow();
        var amount = total ?? paid;
        host.Stripe.Invoices[id] = new StripeInvoiceState(id, paying.CustomerId, paying.SubscriptionId, status, "eur", paid, amount, amount, amount, 0, false, null, reason, now,
            status == "paid" ? now : null, host.Stripe.NewId("pi"), [], new Dictionary<string, string>());
        return id;
    }

    internal static async Task<string> StoreAsync(
        BillingTestHost host, string type, string objectType, string objectId, JsonObject? data = null, DateTimeOffset? created = null)
    {
        var id = "evt_" + Guid.NewGuid().ToString("N");
        var at = created ?? host.Time.GetUtcNow();
        var json = StripeTestEvents.Json(id, type, objectType, objectId, created: at, data: data);
        Assert.True(await host.RunAsync(s => s.GetRequiredService<StripeEventIntake>().StoreAsync(new StripeEventEnvelope(id, type, objectId, false, at, json), Ct)));
        return id;
    }

    internal static Task<StripeEventOutcome> ProcessAsync(BillingTestHost host, string eventId) =>
        host.RunAsync(s => s.GetRequiredService<StripeEventProcessor>().ProcessAsync(eventId, Ct));

    /// <summary>A member of the tenant (notifications go to the members, e-mails of billing to owners and admins).</summary>
    internal static async Task<Guid> MemberAsync(Guid tenantId, string role)
    {
        var user = Guid.CreateVersion7();
        await AdminAsync("INSERT INTO iam.users (id, email, email_confirmed, locale, access_failed_count) VALUES ($1, $2, true, 'sk', 0)", user, $"clen.{user:N}@bylinkovo-test.sk");
        await AdminAsync("INSERT INTO iam.memberships (tenant_id, user_id, role, created_at, updated_at) VALUES ($1, $2, $3, now(), now())", tenantId, user, role);
        return user;
    }
}
