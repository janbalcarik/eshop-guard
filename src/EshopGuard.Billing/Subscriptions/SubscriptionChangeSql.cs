using System.Globalization;
using System.Text.Json.Nodes;
using EshopGuard.Billing.Jobs;
using EshopGuard.Billing.Pricing;
using EshopGuard.Billing.Webhooks;
using EshopGuard.Jobs.Queue;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EshopGuard.Billing.Subscriptions;

/// <summary>A subscription as the planning of its changes reads it (in the transaction of its tenant).</summary>
public sealed record PlannedSubscription(
    Guid Id, Guid ShopId, string Status, string Interval, Guid PriceListId, string TierCode, decimal UnitPrice, decimal? DiscountPercent,
    DateTimeOffset? TrialEnd, DateTimeOffset? PeriodStart, DateTimeOffset? PeriodEnd, bool CancelAtPeriodEnd, string? StripeSubscriptionId,
    string? StripePriceId, string? StripeCouponId, string? StripeScheduleId, string? ScheduleHash, int? ShopOrdinal)
{
    public bool Running => Status is "trialing" or "active" or "past_due";

    public PriceState Price => new(PriceListId, TierCode, Interval, StripePriceId, UnitPrice, StripeCouponId, DiscountPercent);

    /// <summary>Months of one period (12 for a yearly subscription).</summary>
    public int StepMonths => Interval == "year" ? 12 : 1;

    /// <summary>The day the periods are counted from: the end of the trial, else the start of the current period.</summary>
    public DateTimeOffset? Anchor => TrialEnd ?? PeriodStart;

    /// <summary>The start of the next period.</summary>
    public DateTimeOffset? NextStart => PeriodEnd ?? TrialEnd;

    /// <summary>The end of the trial while it runs (the first phase of a schedule keeps it).</summary>
    public DateTimeOffset? RunningTrialEnd => Status == "trialing" ? TrialEnd : null;
}

/// <summary>Plain SQL of the changes of a subscription shared by the planner and the composer of the schedule (task 8.1, 8.2).</summary>
internal static class SubscriptionChangeSql
{
    /// <summary>A pending change older than this was not applied by Stripe in time: it is canceled as <c>missed</c> with an alert.</summary>
    public static readonly TimeSpan MissedAfter = TimeSpan.FromDays(1);

    public static async Task<PlannedSubscription?> SubscriptionAsync(NpgsqlTransaction transaction, Guid id, CancellationToken ct)
    {
        var rows = await BillingSql.ListAsync(transaction,
            """
            SELECT id, shop_id, status, interval, price_list_id, tier_code, unit_price, discount_percent, trial_end, current_period_start, current_period_end,
                   cancel_at_period_end, stripe_subscription_id, stripe_price_id, stripe_coupon_id, stripe_schedule_id, schedule_hash, shop_ordinal
            FROM billing.subscriptions WHERE id = $1 FOR UPDATE
            """,
            r => new PlannedSubscription(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetGuid(4), r.GetString(5), r.GetDecimal(6), r.Get<decimal?>(7),
                r.Get<DateTimeOffset?>(8), r.Get<DateTimeOffset?>(9), r.Get<DateTimeOffset?>(10), r.GetBoolean(11), r.Get<string>(12), r.Get<string>(13), r.Get<string>(14),
                r.Get<string>(15), r.Get<string>(16), r.Get<int?>(17)), ct, id).ConfigureAwait(false);
        return rows.FirstOrDefault();
    }

    /// <summary>The changes waiting for their period (<c>scheduled</c>, <c>notified</c>) of the kinds planned by change 12, locked.</summary>
    public static Task<List<PendingChange>> PendingAsync(NpgsqlTransaction transaction, Guid subscriptionId, CancellationToken ct) =>
        BillingSql.ListAsync(transaction,
            """
            SELECT id, kind, effective_at, created_at, status, notified_at, "to", "from"
            FROM billing.subscription_changes
            WHERE subscription_id = $1 AND status IN ('scheduled', 'notified') AND kind IN ('tier', 'price_list', 'discount')
            ORDER BY effective_at, created_at
            FOR UPDATE
            """,
            r =>
            {
                var to = r.IsDBNull(6) ? null : JsonNode.Parse(r.GetString(6)) as JsonObject;
                var from = r.IsDBNull(7) ? null : JsonNode.Parse(r.GetString(7)) as JsonObject;
                Guid? list = Guid.TryParse((string?)to?["price_list_id"], out var parsed) ? parsed : null;
                var kind = r.GetString(1);
                return new PendingChange(r.GetGuid(0), kind, r.GetFieldValue<DateTimeOffset>(2), r.GetFieldValue<DateTimeOffset>(3),
                    kind == ChangeKinds.Tier ? (string?)to?["tier_code"] : null, kind == ChangeKinds.PriceList ? list : null, r.GetString(4), r.Get<DateTimeOffset?>(5), to, from);
            }, ct, subscriptionId);

    /// <summary>The price lists by id (the missing ones are left out).</summary>
    public static async Task<Dictionary<Guid, PriceListSnapshot>> ListsAsync(NpgsqlTransaction transaction, IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var lists = new Dictionary<Guid, PriceListSnapshot>();
        foreach (var id in ids.OfType<Guid>().Distinct())
        {
            if (await PriceListSql.LoadAsync(transaction, id, ct).ConfigureAwait(false) is { } list)
            {
                lists[id] = list;
            }
        }

        return lists;
    }

    /// <summary>
    /// Cancels the pending changes whose date passed more than <see cref="MissedAfter"/> ago (Stripe did not switch the price, or
    /// its event got lost): status <c>canceled</c>, reason <c>missed</c>, an alert. Returns the remaining pending changes.
    /// </summary>
    public static async Task<List<PendingChange>> CancelMissedAsync(
        NpgsqlTransaction transaction, Guid tenantId, List<PendingChange> pending, DateTimeOffset now, ILogger logger, CancellationToken ct)
    {
        var remaining = new List<PendingChange>();
        foreach (var change in pending)
        {
            if (change.EffectiveAt >= now - MissedAfter)
            {
                remaining.Add(change);
                continue;
            }

            await SetStatusAsync(transaction, change.Id, "canceled", "missed", now, ct).ConfigureAwait(false);
            await BillingAlerts.RaiseAsync(transaction, tenantId, "billing.alert.change_missed", "subscription_change", change.Id.ToString("D"), now, logger, ct)
                .ConfigureAwait(false);
        }

        return remaining;
    }

    public static Task SetStatusAsync(NpgsqlTransaction transaction, Guid changeId, string status, string reason, DateTimeOffset now, CancellationToken ct) =>
        BillingSql.ExecuteAsync(transaction,
            """
            UPDATE billing.subscription_changes SET status = $2, reason_code = $3, updated_at = $4, applied_at = CASE WHEN $2 = 'applied' THEN $4 ELSE applied_at END
            WHERE id = $1
            """, ct, changeId, status, reason, now);

    /// <summary>
    /// The version of the changes of a subscription: it grows with every planned, canceled or applied change, so each new state of
    /// the plan gets its own job of composition (keys of the queue stay forever).
    /// </summary>
    public static async Task<long> VersionAsync(NpgsqlTransaction transaction, Guid subscriptionId, CancellationToken ct) =>
        await BillingSql.ScalarAsync<long>(transaction,
            "SELECT count(*) + count(*) FILTER (WHERE status IN ('canceled', 'applied')) FROM billing.subscription_changes WHERE subscription_id = $1",
            ct, subscriptionId).ConfigureAwait(false);

    /// <summary>Enqueues the composition of the schedule for the current version and timeline of the subscription.</summary>
    public static async Task EnqueueComposeAsync(
        IJobQueue queue, NpgsqlTransaction transaction, Guid tenantId, PlannedSubscription subscription, string hash, CancellationToken ct)
    {
        var version = await VersionAsync(transaction, subscription.Id, ct).ConfigureAwait(false);
        await queue.EnqueueAsync(BillingJobs.ComposeSchedule(tenantId, subscription.ShopId, subscription.Id, $"v{version}:{hash[..12]}"), transaction, ct)
            .ConfigureAwait(false);
    }

    public static DateOnly LocalDay(DateTimeOffset at, TimeZoneInfo zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);

    public static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
