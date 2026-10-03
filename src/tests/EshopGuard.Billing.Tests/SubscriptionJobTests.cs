using EshopGuard.Billing.Jobs;
using EshopGuard.Billing.Stripe;
using EshopGuard.Jobs.Processing;
using Microsoft.Extensions.DependencyInjection;
using static EshopGuard.Billing.Tests.BillingTestHost;
using static EshopGuard.Billing.Tests.StripeScenario;

namespace EshopGuard.Billing.Tests;

/// <summary>
/// The jobs of the subscriptions (change 12, tasks 7.3 and 7.6): <c>billing.expire_order</c> settles or expires an order paid
/// with the saved card, <c>billing.trial_reminder</c> reminds the first payment seven days ahead, once.
/// </summary>
[Trait("Category", "Db")]
public sealed class SubscriptionJobTests
{
    [Fact]
    public async Task SavedCardOrder_RunningInStripe_IsPaidByTheJob_AndTheRunStarts()
    {
        await using var host = await HostAsync();
        var paying = await SavedCardAsync(host, "trialing");

        Assert.Same(JobResult.Done, await ExpireAsync(host, paying, 1));
        Assert.Same(JobResult.Done, await ExpireAsync(host, paying, 1));

        Assert.Equal("paid", await AdminScalarAsync<string>("SELECT status FROM billing.orders WHERE id = $1", paying.OrderId));
        Assert.Equal(["saved_card"], (await AdminRowsAsync(
            "SELECT data->>'method' FROM ops.audit_log WHERE action = 'order.paid' AND entity_id = $1", paying.OrderId.ToString("D"))).Select(r => (string)r[0]!));
        Assert.Equal(new object?[] { "trialing", paying.OrderId }, Assert.Single(await AdminRowsAsync(
            "SELECT status, order_id FROM billing.subscriptions WHERE stripe_subscription_id = $1", paying.SubscriptionId)));
        Assert.Equal("analyzing", await AdminScalarAsync<string>("SELECT status FROM shop.shops WHERE id = $1", paying.ShopId));
        Assert.Equal("profiling", await AdminScalarAsync<string>("SELECT status FROM checks.runs WHERE id = $1", paying.RunId));
    }

    [Fact]
    public async Task UnconfirmedSavedCardOrder_WaitsForTheExpiry_ThenIsCanceledOnce_AndTheShopIsUntouched()
    {
        await using var host = await HostAsync();
        var paying = await SavedCardAsync(host, "incomplete");
        await ProcessAsync(host, await StoreAsync(host, "customer.subscription.created", "subscription", paying.SubscriptionId));

        await ExpireAsync(host, paying, 1);
        await ExpireAsync(host, paying, 2);

        Assert.Equal("checkout_open", await AdminScalarAsync<string>("SELECT status FROM billing.orders WHERE id = $1", paying.OrderId));
        Assert.Equal(0, host.Stripe.Count(nameof(IStripeGateway.CancelSubscriptionAsync)));

        host.Time.Advance(TimeSpan.FromMinutes(31));
        await ExpireAsync(host, paying, 1);
        await ExpireAsync(host, paying, 1);
        await ProcessAsync(host, await StoreAsync(host, "customer.subscription.deleted", "subscription", paying.SubscriptionId));

        Assert.Equal("expired", await AdminScalarAsync<string>("SELECT status FROM billing.orders WHERE id = $1", paying.OrderId));
        Assert.Equal(1, host.Stripe.Count(nameof(IStripeGateway.CancelSubscriptionAsync)));
        Assert.Equal("canceled", host.Stripe.Subscriptions[paying.SubscriptionId].Status);
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'order.expired' AND entity_id = $1", paying.OrderId.ToString("D")));
        Assert.Equal(new object?[] { "canceled", "not_started" }, Assert.Single(await AdminRowsAsync(
            "SELECT status, pause_reason FROM billing.subscriptions WHERE stripe_subscription_id = $1", paying.SubscriptionId)));
        Assert.Equal("awaiting_payment", await AdminScalarAsync<string>("SELECT status FROM shop.shops WHERE id = $1", paying.ShopId));
        Assert.Equal("awaiting_payment", await AdminScalarAsync<string>("SELECT status FROM checks.runs WHERE id = $1", paying.RunId));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'subscription.ended' AND tenant_id = $1", paying.TenantId));
    }

    [Fact]
    public async Task TrialReminder_IsSentSevenDaysBeforeTheFirstPayment_Once_AndNotForACanceledTrial()
    {
        await using var host = await CreateAsync(now: new DateTimeOffset(2026, 10, 24, 8, 0, 0, TimeSpan.Zero));
        var market = await BillingData.MarketAsync();
        var list = await BillingData.PriceListAsync(market, "published");
        var tenant = await BillingData.TenantAsync(market);
        var owner = await MemberAsync(tenant, "owner");
        var trialEnd = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        var full = await TrialAsync(tenant, list, "t20000", 59m, trialEnd);
        var discounted = await TrialAsync(tenant, list, "t2000", 19m, trialEnd, "UPDATE billing.subscriptions SET discount_percent = 10 WHERE id = $1");
        var canceled = await TrialAsync(tenant, list, "t500", 9m, trialEnd, "UPDATE billing.subscriptions SET cancel_at_period_end = true WHERE id = $1");

        Assert.Equal(0, await RemindAsync(host, tenant));
        host.Time.Advance(TimeSpan.FromDays(1));
        Assert.Equal(2, await RemindAsync(host, tenant));
        Assert.Equal(0, await RemindAsync(host, tenant));

        var notifications = await AdminRowsAsync(
            """
            SELECT params->>'amount', params->>'currency', params->>'date', params->>'subscription_id', route->>'key'
            FROM iam.notifications WHERE user_id = $1 AND kind = 'trial_ending' ORDER BY params->>'amount'
            """, owner);
        Assert.Equal(
            [
                new object?[] { "17.10", "EUR", "2026-11-01", discounted.ToString("D"), "billing.overview" },
                new object?[] { "59.00", "EUR", "2026-11-01", full.ToString("D"), "billing.overview" },
            ],
            notifications);
        Assert.Equal(2L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.outbox WHERE tenant_id = $1 AND kind = 'email' AND payload->>'template' = 'trial_ending'", tenant));
        Assert.Equal([canceled], (await AdminRowsAsync("SELECT id FROM billing.subscriptions WHERE tenant_id = $1 AND trial_reminder_sent_at IS NULL", tenant))
            .Select(r => (Guid)r[0]!));
    }

    /// <summary>The order of <see cref="StripeScenario.PayingAsync"/> paid with the saved card: no Checkout, its subscription in the given status.</summary>
    private static async Task<Paying> SavedCardAsync(BillingTestHost host, string status)
    {
        var paying = await PayingAsync(host);
        host.Stripe.Sessions.TryRemove(paying.SessionId, out _);
        host.Stripe.Subscriptions[paying.SubscriptionId] = host.Stripe.Subscriptions[paying.SubscriptionId] with
        {
            Status = status,
            PaymentClientSecret = status == "incomplete" ? "pi_secret_" + paying.SubscriptionId : null,
        };
        await AdminAsync(
            "UPDATE billing.orders SET stripe_checkout_session_id = NULL, stripe_subscription_id = $2, checkout_expires_at = $3 WHERE id = $1",
            paying.OrderId, paying.SubscriptionId, host.Time.GetUtcNow().AddMinutes(30));
        return paying;
    }

    private static Task<JobResult> ExpireAsync(BillingTestHost host, Paying paying, int attempt) =>
        host.RunAsync(s => s.GetServices<IJobHandler>().OfType<ExpireOrderHandler>().Single().RunAsync(paying.TenantId, paying.OrderId, attempt, Ct));

    private static Task<int> RemindAsync(BillingTestHost host, Guid tenantId) =>
        host.RunAsync(s => s.GetServices<IJobHandler>().OfType<TrialReminderHandler>().Single().RunAsync(tenantId, Ct));

    private static async Task<Guid> TrialAsync(Guid tenantId, Guid priceListId, string tier, decimal unitPrice, DateTimeOffset trialEnd, string? update = null)
    {
        var shop = await BillingData.ShopAsync(tenantId);
        var subscription = await BillingData.SubscriptionAsync(tenantId, shop, priceListId, tier, unitPrice, trialEnd, "trialing");
        await AdminAsync("UPDATE billing.subscriptions SET trial_end = $2 WHERE id = $1", subscription, trialEnd);
        if (update is not null)
        {
            await AdminAsync(update, subscription);
        }

        return subscription;
    }
}
