using System.Text.Json.Nodes;
using EshopGuard.Billing.Stripe;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EshopGuard.Billing.Subscriptions;

/// <summary>The subscription of an e-shop after a synchronization: its id, the e-shop, the state before and now.</summary>
public sealed record SyncedSubscription(Guid Id, Guid ShopId, string? PreviousStatus, string Status, string? PreviousPriceId, string? PriceId, bool Created);

/// <summary>
/// Writes the state of a subscription of Stripe into <c>billing.subscriptions</c> (task 7.1): the state, the period, the trial,
/// <c>cancel_at_period_end</c>, the price and the coupon and with them the tier, the price list and the unit price. The object is
/// always the current one read from Stripe, so an older event never overwrites a newer state. A new subscription gets the
/// order of its e-shop in the account; a second running subscription of an e-shop fails on the unique index and the caller
/// raises an alert of operations.
/// </summary>
public sealed class SubscriptionSync(TimeProvider time, ILogger<SubscriptionSync> logger)
{
    /// <summary>The states of Stripe as stored (<c>incomplete_expired</c> ends, <c>unpaid</c> is a failed payment).</summary>
    public static string Status(string stripeStatus) => stripeStatus switch
    {
        "trialing" or "active" or "past_due" or "canceled" or "incomplete" or "paused" => stripeStatus,
        "incomplete_expired" => "canceled",
        "unpaid" => "past_due",
        _ => "incomplete",
    };

    /// <summary>The running states (one per e-shop).</summary>
    public static readonly string[] Running = ["trialing", "active", "past_due", "incomplete"];

    /// <summary>In the transaction of the tenant; null when the e-shop of a new subscription is not known.</summary>
    public async Task<SyncedSubscription?> UpsertAsync(
        NpgsqlTransaction transaction, Guid tenantId, StripeSubscriptionState state, Guid? shopId, Guid? orderId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(state);
        var now = time.GetUtcNow();
        var status = Status(state.Status);
        var existing = await BillingSql.ListAsync(transaction,
            "SELECT id, shop_id, status, stripe_price_id FROM billing.subscriptions WHERE stripe_subscription_id = $1 FOR UPDATE",
            r => (Id: r.GetGuid(0), ShopId: r.GetGuid(1), Status: r.GetString(2), PriceId: r.Get<string>(3)), ct, state.Id).ConfigureAwait(false);
        (Guid ListId, string Tier, decimal? Unit, string Interval)? price = state.PriceId is null ? null : (await BillingSql.ListAsync(transaction,
            """
            SELECT t.price_list_id, t.code, CASE WHEN t.stripe_price_yearly = $1 THEN t.monitoring_yearly ELSE t.monitoring_monthly END,
                   CASE WHEN t.stripe_price_yearly = $1 THEN 'year' ELSE 'month' END
            FROM billing.price_tiers t WHERE t.stripe_price_monthly = $1 OR t.stripe_price_yearly = $1
            """, r => ((Guid ListId, string Tier, decimal? Unit, string Interval)?)(r.GetGuid(0), r.GetString(1), r.Get<decimal?>(2), r.GetString(3)), ct, state.PriceId)
            .ConfigureAwait(false)).FirstOrDefault();
        var discount = state.CouponId is null ? null
            : await BillingSql.ScalarAsync<decimal?>(transaction, "SELECT percent FROM billing.volume_discounts WHERE stripe_coupon_id = $1", ct, state.CouponId).ConfigureAwait(false);
        if (existing.Count == 1)
        {
            var row = existing[0];
            await BillingSql.ExecuteAsync(transaction,
                """
                UPDATE billing.subscriptions SET status = $2, trial_end = $3, current_period_start = $4, current_period_end = $5, cancel_at_period_end = $6,
                    canceled_at = $7, stripe_price_id = $8, stripe_coupon_id = $9, stripe_schedule_id = $10,
                    price_list_id = coalesce($11, price_list_id), tier_code = coalesce($12, tier_code), unit_price = coalesce($13, unit_price),
                    interval = coalesce($14, interval), discount_percent = $15, updated_at = $16
                WHERE id = $1
                """, ct, row.Id, status, state.TrialEnd, state.CurrentPeriodStart, state.CurrentPeriodEnd, state.CancelAtPeriodEnd, state.CanceledAt ?? state.EndedAt,
                state.PriceId, state.CouponId, state.ScheduleId, price?.ListId, price?.Tier, price?.Unit, price?.Interval, discount, now).ConfigureAwait(false);
            return new SyncedSubscription(row.Id, row.ShopId, row.Status, status, row.PriceId, state.PriceId, false);
        }

        if ((shopId ?? ShopOf(state)) is not { } shop || price is null)
        {
            logger.LogWarning("subscription.unknown {StripeSubscriptionId} {TenantId}", state.Id, tenantId);
            return null;
        }

        var id = Guid.CreateVersion7();
        var ordinal = 1 + await BillingSql.ScalarAsync<long>(transaction,
            "SELECT count(DISTINCT shop_id) FROM billing.subscriptions WHERE shop_id <> $1 AND status IN ('trialing', 'active', 'past_due')", ct, shop).ConfigureAwait(false);
        await BillingSql.ExecuteAsync(transaction,
            """
            INSERT INTO billing.subscriptions (id, tenant_id, shop_id, stripe_subscription_id, status, interval, price_list_id, tier_code, unit_price, discount_percent,
                trial_end, current_period_start, current_period_end, cancel_at_period_end, canceled_at, order_id, stripe_price_id, stripe_coupon_id, stripe_schedule_id,
                shop_ordinal, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $21)
            """, ct, id, tenantId, shop, state.Id, status, price.Value.Interval, price.Value.ListId, price.Value.Tier, price.Value.Unit ?? 0m, discount, state.TrialEnd,
            state.CurrentPeriodStart, state.CurrentPeriodEnd, state.CancelAtPeriodEnd, state.CanceledAt, orderId ?? OrderOf(state), state.PriceId, state.CouponId,
            state.ScheduleId, (int)ordinal, now).ConfigureAwait(false);
        await BillingSql.AuditAsync(transaction, tenantId, null, BillingSql.System, "subscription.created", "subscription", id.ToString("D"),
            new JsonObject { ["shop_id"] = shop.ToString("D"), ["tier"] = price.Value.Tier, ["status"] = status }, now, ct).ConfigureAwait(false);
        return new SyncedSubscription(id, shop, null, status, null, state.PriceId, true);
    }

    public static Guid? ShopOf(StripeSubscriptionState state) => Meta(state.Metadata, "shop_id");

    public static Guid? OrderOf(StripeSubscriptionState state) => Meta(state.Metadata, "order_id");

    public static Guid? Meta(IReadOnlyDictionary<string, string> metadata, string key) =>
        metadata.TryGetValue(key, out var value) && Guid.TryParse(value, out var id) ? id : null;
}
