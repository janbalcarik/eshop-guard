using System.Globalization;
using System.Text.Json.Nodes;
using EshopGuard.Application.Options;
using EshopGuard.Billing.Pricing;
using EshopGuard.Billing.Webhooks;
using EshopGuard.Jobs.Notifications;
using EshopGuard.Jobs.Queue;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EshopGuard.Billing.Subscriptions;

/// <summary>
/// What should change: the tier (by the counted products; <paramref name="LowerBound"/> = the count of a partial analysis, so
/// only a higher tier is planned), the price list, or the volume discount by the order of the e-shop.
/// </summary>
public sealed record ChangeRequest(string Kind, string? TierCode = null, Guid? PriceListId = null, int? Products = null, bool LowerBound = false)
{
    public static ChangeRequest Tier(string code, int? products, bool lowerBound = false) => new(ChangeKinds.Tier, code, null, products, lowerBound);

    public static ChangeRequest PriceList(Guid id) => new(ChangeKinds.PriceList, PriceListId: id);

    public static ChangeRequest Discount() => new(ChangeKinds.Discount);
}

/// <summary>The result of planning: <c>skipped</c>, <c>unchanged</c>, <c>kept</c>, <c>scheduled</c>, <c>canceled</c> or <c>refused</c>.</summary>
public sealed record PlanResult(string Outcome, Guid? ChangeId = null, DateTimeOffset? EffectiveAt = null, string? Code = null)
{
    public const string Skipped = "skipped";
    public const string Unchanged = "unchanged";
    public const string Kept = "kept";
    public const string Scheduled = "scheduled";
    public const string Canceled = "canceled";
    public const string Refused = "refused";
}

/// <summary>
/// Plans a change of a subscription into <c>billing.subscription_changes</c> (tasks 8.1–8.5, design „Plánování“) in the
/// transaction of the tenant. The date of effect is always the start of a billing period of the subscription:
/// <list type="bullet">
/// <item>a higher tier from the first period after <c>Billing:TierChangeNoticeDays</c>, a lower tier and a change of the volume
/// discount from the next period;</item>
/// <item>a more expensive price list from the first period after its <c>valid_from</c>, after <c>notice_days</c> since its
/// publication and since now, and after the locked price of a founder; a cheaper one from its <c>valid_from</c>.</item>
/// </list>
/// A target equal to the current price cancels the pending change of its kind (notified ones with an e-mail), another target
/// replaces it, the same target keeps it with its date. Every pending change gets its prices from
/// <see cref="SubscriptionTimeline"/>, a change of the amount is notified (in the app and by e-mail), and the schedule in Stripe
/// is composed by a job (<c>billing.compose_schedule</c>). What cannot be charged is refused with an alert (fail-closed).
/// </summary>
public sealed class SubscriptionChangePlanner(
    IJobQueue queue,
    NotificationDispatcher notifications,
    IOptions<BillingOptions> options,
    IOptions<LocalizationOptions> localization,
    TimeProvider time,
    ILogger<SubscriptionChangePlanner> logger)
{
    public async Task<PlanResult> PlanAsync(NpgsqlTransaction transaction, Guid tenantId, Guid subscriptionId, ChangeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(request);
        var now = time.GetUtcNow();
        var subscription = await SubscriptionChangeSql.SubscriptionAsync(transaction, subscriptionId, ct).ConfigureAwait(false);
        if (subscription is not { Running: true, CancelAtPeriodEnd: false, Anchor: { } anchor, NextStart: { } nextStart })
        {
            return new PlanResult(PlanResult.Skipped);
        }

        var all = await SubscriptionChangeSql.PendingAsync(transaction, subscription.Id, ct).ConfigureAwait(false);
        var pending = await SubscriptionChangeSql.CancelMissedAsync(transaction, tenantId, all, now, logger, ct).ConfigureAwait(false);
        var changed = pending.Count != all.Count;
        var lists = await SubscriptionChangeSql.ListsAsync(transaction, [subscription.PriceListId, request.PriceListId, .. pending.Select(c => c.PriceListId)], ct)
            .ConfigureAwait(false);
        if (!lists.TryGetValue(subscription.PriceListId, out var current))
        {
            return await RefuseAsync(transaction, tenantId, subscription, SubscriptionTimeline.PriceListMissing, now, ct).ConfigureAwait(false);
        }

        var existing = pending.Where(c => c.Kind == request.Kind).ToList();
        var target = Target(subscription, current, lists, existing, request);
        PlanResult result;
        if (target is null)
        {
            result = new PlanResult(PlanResult.Unchanged);
        }
        else if (target.Value.AtCurrent)
        {
            foreach (var change in existing)
            {
                await CancelAsync(transaction, tenantId, subscription, change, "not_needed", now, ct).ConfigureAwait(false);
            }

            pending.RemoveAll(existing.Contains);
            changed |= existing.Count > 0;
            result = new PlanResult(existing.Count > 0 ? PlanResult.Canceled : PlanResult.Unchanged);
        }
        else if (existing.FirstOrDefault(target.Value.Same) is { } same)
        {
            foreach (var other in existing.Where(c => c != same))
            {
                await CancelAsync(transaction, tenantId, subscription, other, "replaced", now, ct).ConfigureAwait(false);
                pending.Remove(other);
                changed = true;
            }

            result = new PlanResult(PlanResult.Kept, same.Id, same.EffectiveAt);
        }
        else
        {
            var founderUntil = await FounderUntilAsync(transaction, tenantId, ct).ConfigureAwait(false);
            var (threshold, reason) = Threshold(subscription, current, lists, pending, request, founderUntil, now);
            var effective = BillingPeriods.FirstStartOnOrAfter(anchor, nextStart, threshold, subscription.StepMonths);
            var note = request.Products is { } products ? new JsonObject { ["products"] = products } : null;
            var candidate = new PendingChange(Guid.CreateVersion7(now), request.Kind, effective, now, request.Kind == ChangeKinds.Tier ? request.TierCode : null,
                request.Kind == ChangeKinds.PriceList ? request.PriceListId : null, To: note);
            var planned = pending.Except(existing).Append(candidate).ToList();
            var check = SubscriptionTimeline.Build(subscription.Price, subscription.RunningTrialEnd, subscription.ShopOrdinal, planned, Lookup(lists));
            if (check.Problem is { } problem)
            {
                if (changed)
                {
                    await EnqueueComposeAsync(transaction, tenantId, subscription, pending, lists, ct).ConfigureAwait(false);
                }

                return await RefuseAsync(transaction, tenantId, subscription, problem, now, ct).ConfigureAwait(false);
            }

            foreach (var old in existing)
            {
                await CancelAsync(transaction, tenantId, subscription, old, "replaced", now, ct).ConfigureAwait(false);
            }

            var step = check.Steps.Single(s => s.Change.Id == candidate.Id);
            if (step.From.Amount == step.To.Amount)
            {
                // The new target keeps the amount, so nothing announces it: the merchant learns the announced one is off.
                foreach (var old in existing.Where(c => c.Status == "notified"))
                {
                    await NotifyCanceledAsync(transaction, tenantId, subscription.Id, subscription.ShopId, old, ct).ConfigureAwait(false);
                }
            }
            await BillingSql.ExecuteAsync(transaction,
                """
                INSERT INTO billing.subscription_changes (id, tenant_id, subscription_id, kind, "from", "to", effective_at, status, reason_code, created_at, updated_at)
                VALUES ($1, $2, $3, $4, $5, $6, $7, 'scheduled', $8, $9, $9)
                """, ct, candidate.Id, tenantId, subscription.Id, candidate.Kind, BillingSql.Json(step.From.ToJson()), BillingSql.Json(Merge(step.To.ToJson(), note)),
                effective, reason, now).ConfigureAwait(false);
            await BillingSql.AuditAsync(transaction, tenantId, null, BillingSql.System, "subscription.change_scheduled", "subscription_change", candidate.Id.ToString("D"),
                new JsonObject { ["subscription_id"] = subscription.Id.ToString("D"), ["kind"] = candidate.Kind, ["reason"] = reason }, now, ct).ConfigureAwait(false);
            pending = [.. planned.Select(c => c.Id == candidate.Id ? c with { From = step.From.ToJson(), To = Merge(step.To.ToJson(), note) } : c)];
            changed = true;
            result = new PlanResult(PlanResult.Scheduled, candidate.Id, effective, reason);
        }

        changed |= await NormalizeAsync(transaction, tenantId, subscription, pending, lists, result.Outcome == PlanResult.Scheduled ? result.ChangeId : null, now, ct)
            .ConfigureAwait(false);
        if (changed)
        {
            await EnqueueComposeAsync(transaction, tenantId, subscription, pending, lists, ct).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// The higher of two tiers of a price list (by the lower bound of products); a tier the list lacks counts as the lowest.
    /// </summary>
    public static int Rank(PriceListSnapshot list, string? code)
    {
        ArgumentNullException.ThrowIfNull(list);
        return code is null ? -1 : list.Tier(code)?.MinProducts ?? -1;
    }

    /// <summary>
    /// Stripe switched the Price or the coupon of a subscription (task 8.6): the newest pending change due by now (within
    /// <see cref="SubscriptionChangeSql.MissedAfter"/>) whose target is the new price is applied with every change before it, in
    /// the audit <c>subscription.price_changed</c>, and the rest of the schedule is composed again. A switch no planned change
    /// explains is only written by the synchronization. Returns the applied changes.
    /// </summary>
    public async Task<int> AppliedAsync(NpgsqlTransaction transaction, Guid tenantId, SyncedSubscription synced, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(synced);
        if (synced.Created || (string.Equals(synced.PreviousPriceId, synced.PriceId, StringComparison.Ordinal)
            && string.Equals(synced.PreviousCouponId, synced.CouponId, StringComparison.Ordinal)))
        {
            return 0;
        }

        var now = time.GetUtcNow();
        var pending = await SubscriptionChangeSql.PendingAsync(transaction, synced.Id, ct).ConfigureAwait(false);
        var due = pending.Where(c => c.EffectiveAt <= now + SubscriptionChangeSql.MissedAfter).ToList();
        var last = due.FindLastIndex(c => string.Equals((string?)c.To?["stripe_price_id"], synced.PriceId, StringComparison.Ordinal)
            && string.Equals(c.CouponId, synced.CouponId, StringComparison.Ordinal));
        if (last < 0 || await SubscriptionChangeSql.SubscriptionAsync(transaction, synced.Id, ct).ConfigureAwait(false) is not { } subscription)
        {
            return 0;
        }

        var applied = due.Take(last + 1).ToList();
        foreach (var change in applied)
        {
            await BillingSql.ExecuteAsync(transaction,
                "UPDATE billing.subscription_changes SET status = 'applied', applied_at = $2, updated_at = $2 WHERE id = $1", ct, change.Id, now).ConfigureAwait(false);
        }

        await BillingSql.AuditAsync(transaction, tenantId, null, BillingSql.System, "subscription.price_changed", "subscription", synced.Id.ToString("D"),
            new JsonObject
            {
                ["from_price"] = synced.PreviousPriceId,
                ["to_price"] = synced.PriceId,
                ["from_coupon"] = synced.PreviousCouponId,
                ["to_coupon"] = synced.CouponId,
                ["changes"] = new JsonArray([.. applied.Select(c => (JsonNode)c.Id.ToString("D"))]),
            }, now, ct).ConfigureAwait(false);
        var remaining = pending.Except(applied).ToList();
        var lists = await SubscriptionChangeSql.ListsAsync(transaction, [subscription.PriceListId, .. remaining.Select(c => c.PriceListId)], ct).ConfigureAwait(false);
        await EnqueueComposeAsync(transaction, tenantId, subscription, remaining, lists, ct).ConfigureAwait(false);
        logger.LogInformation("billing.change_applied {SubscriptionId} {TenantId} {Changes}", synced.Id, tenantId, applied.Count);
        return applied.Count;
    }

    /// <summary>
    /// A subscription of the tenant ended (task 8.4): its pending changes are canceled (<c>subscription_ended</c>, without e-mail,
    /// the end has its own), the running subscriptions are numbered again by their creation and each plans its volume discount.
    /// </summary>
    public async Task EndedAsync(NpgsqlTransaction transaction, Guid tenantId, Guid subscriptionId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var now = time.GetUtcNow();
        foreach (var change in await SubscriptionChangeSql.PendingAsync(transaction, subscriptionId, ct).ConfigureAwait(false))
        {
            await SubscriptionChangeSql.SetStatusAsync(transaction, change.Id, "canceled", "subscription_ended", now, ct).ConfigureAwait(false);
        }

        await BillingSql.ExecuteAsync(transaction,
            """
            WITH ordered AS (
                SELECT id, row_number() OVER (ORDER BY created_at, id)::int AS ordinal FROM billing.subscriptions WHERE status IN ('trialing', 'active', 'past_due'))
            UPDATE billing.subscriptions s SET shop_ordinal = o.ordinal, updated_at = $1
            FROM ordered o WHERE s.id = o.id AND s.shop_ordinal IS DISTINCT FROM o.ordinal
            """, ct, now).ConfigureAwait(false);
        var running = await BillingSql.ListAsync(transaction,
            "SELECT id FROM billing.subscriptions WHERE status IN ('trialing', 'active', 'past_due') ORDER BY created_at, id", r => r.GetGuid(0), ct).ConfigureAwait(false);
        foreach (var id in running)
        {
            await PlanAsync(transaction, tenantId, id, ChangeRequest.Discount(), ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The earliest moment a change may take effect (the date is the first start of a period at or after it) and the reason code.
    /// </summary>
    internal (DateTimeOffset Threshold, string Reason) Threshold(
        PlannedSubscription subscription, PriceListSnapshot current, IReadOnlyDictionary<Guid, PriceListSnapshot> lists, IReadOnlyList<PendingChange> pending,
        ChangeRequest request, DateTimeOffset? founderUntil, DateTimeOffset now)
    {
        switch (request.Kind)
        {
            case ChangeKinds.Tier:
                return Rank(current, request.TierCode) > Rank(current, subscription.TierCode)
                    ? (now.AddDays(options.Value.TierChangeNoticeDays), "tier_up")
                    : (now, "tier_down");

            case ChangeKinds.PriceList:
            {
                var list = lists[request.PriceListId!.Value];
                var tier = pending.LastOrDefault(c => c.Kind == ChangeKinds.Tier)?.TierCode ?? subscription.TierCode;
                var (_, before) = SubscriptionTimeline.PriceOf(current, tier, subscription.Interval);
                var (_, after) = SubscriptionTimeline.PriceOf(list, tier, subscription.Interval);
                if (before is not { } old || after is not { } next || next <= old)
                {
                    return (list.List.ValidFrom, before == after ? "price_unchanged" : "price_decrease");
                }

                var notice = list.List.NoticeDays;
                var threshold = Max(list.List.ValidFrom, now.AddDays(notice));
                if (list.List.PublishedAt is { } published)
                {
                    threshold = Max(threshold, published.AddDays(notice));
                }

                if (founderUntil is { } founder && founder > now)
                {
                    threshold = Max(threshold, founder);
                }

                return (threshold, "price_increase");
            }

            default:
            {
                var discount = VolumeDiscountResolver.Resolve(current.Discounts, subscription.ShopOrdinal ?? 0)?.Percent;
                return (now, subscription.DiscountPercent is null ? "discount_gained" : discount is null ? "discount_lost" : "discount_changed");
            }
        }
    }

    private static (bool AtCurrent, Func<PendingChange, bool> Same)? Target(
        PlannedSubscription subscription, PriceListSnapshot current, IReadOnlyDictionary<Guid, PriceListSnapshot> lists, List<PendingChange> existing,
        ChangeRequest request)
    {
        switch (request.Kind)
        {
            case ChangeKinds.Tier when request.TierCode is { } code:
                if (request.LowerBound && Rank(current, code) <= Rank(current, existing.LastOrDefault()?.TierCode ?? subscription.TierCode))
                {
                    return null;
                }

                return (code == subscription.TierCode, c => c.TierCode == code);

            case ChangeKinds.PriceList when request.PriceListId is { } id && lists.ContainsKey(id):
                return (id == subscription.PriceListId, c => c.PriceListId == id);

            case ChangeKinds.Discount when subscription.ShopOrdinal is { } ordinal:
            {
                var discount = VolumeDiscountResolver.Resolve(current.Discounts, ordinal);
                var coupon = discount?.StripeCouponId;
                return (coupon == subscription.StripeCouponId && discount?.Percent == subscription.DiscountPercent, c => c.CouponId == coupon);
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// Writes the prices of every pending change from the timeline and notifies each change of the amount the merchant has not
    /// been told yet (the new one, or a notified one whose amount moved). Returns whether a row changed.
    /// </summary>
    private async Task<bool> NormalizeAsync(
        NpgsqlTransaction transaction, Guid tenantId, PlannedSubscription subscription, List<PendingChange> pending,
        IReadOnlyDictionary<Guid, PriceListSnapshot> lists, Guid? created, DateTimeOffset now, CancellationToken ct)
    {
        if (pending.Count == 0)
        {
            return false;
        }

        var timeline = SubscriptionTimeline.Build(subscription.Price, subscription.RunningTrialEnd, subscription.ShopOrdinal, pending, Lookup(lists));
        if (timeline.Problem is not null)
        {
            return false;
        }

        var founderUntil = await FounderUntilAsync(transaction, tenantId, ct).ConfigureAwait(false);
        var changed = false;
        foreach (var step in timeline.Steps)
        {
            var change = step.Change;
            var from = step.From.ToJson();
            var to = Merge(step.To.ToJson(), Note(change.To));
            var rewritten = !JsonNode.DeepEquals(from, change.From) || !JsonNode.DeepEquals(to, change.To);
            if (rewritten)
            {
                await BillingSql.ExecuteAsync(transaction,
                    """UPDATE billing.subscription_changes SET "from" = $2, "to" = $3, updated_at = $4 WHERE id = $1""",
                    ct, change.Id, BillingSql.Json(from), BillingSql.Json(to), now).ConfigureAwait(false);
                changed = true;
            }

            var told = PriceState.FromJson(change.To, subscription.Interval)?.Amount;
            var fresh = change.Id == created;
            var notify = step.From.Amount != step.To.Amount && (fresh || change.NotifiedAt is null || told != step.To.Amount);
            if (fresh && step.From.Amount == step.To.Amount && change.Kind != ChangeKinds.Tier)
            {
                await BillingSql.ExecuteAsync(transaction, "UPDATE billing.subscription_changes SET reason_code = 'price_unchanged' WHERE id = $1", ct, change.Id)
                    .ConfigureAwait(false);
            }

            if (!notify)
            {
                continue;
            }

            await NotifyAsync(transaction, tenantId, subscription, step, lists, founderUntil, now, ct).ConfigureAwait(false);
            await BillingSql.ExecuteAsync(transaction,
                "UPDATE billing.subscription_changes SET status = 'notified', notified_at = $2, updated_at = $2 WHERE id = $1", ct, change.Id, now).ConfigureAwait(false);
            changed = true;
        }

        return changed;
    }

    private async Task NotifyAsync(
        NpgsqlTransaction transaction, Guid tenantId, PlannedSubscription subscription, TimelineStep step, IReadOnlyDictionary<Guid, PriceListSnapshot> lists,
        DateTimeOffset? founderUntil, DateTimeOffset now, CancellationToken ct)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(localization.Value.TimeZone);
        var change = step.Change;
        var date = SubscriptionChangeSql.Day(SubscriptionChangeSql.LocalDay(change.EffectiveAt, zone));
        var parameters = new JsonObject
        {
            ["subscription_id"] = subscription.Id.ToString("D"),
            ["change_id"] = change.Id.ToString("D"),
            ["oldAmount"] = Amount(step.From.Amount),
            ["newAmount"] = Amount(step.To.Amount),
            ["currency"] = lists.TryGetValue(step.To.PriceListId, out var list) ? list.List.Currency : null,
            ["date"] = date,
            ["decrease"] = step.To.Amount < step.From.Amount,
        };
        NotificationKind kind;
        if (change.Kind == ChangeKinds.Tier)
        {
            kind = NotificationKinds.TierChange;
            parameters["products"] = (int?)Note(change.To)?["products"];
        }
        else
        {
            kind = NotificationKinds.PriceChange;
            var founder = change.Kind == ChangeKinds.PriceList && step.To.Amount > step.From.Amount && founderUntil is { } until && until > now;
            parameters["founder"] = founder;
            parameters["lockedUntil"] = founder ? SubscriptionChangeSql.Day(SubscriptionChangeSql.LocalDay(founderUntil!.Value, zone)) : date;
        }

        await notifications.NotifyAsync(transaction, new NotificationRequest(tenantId, subscription.ShopId, kind, parameters, NotificationRoutes.Billing, new JsonObject()), ct)
            .ConfigureAwait(false);
    }

    /// <summary>Cancels a pending change; a notified one is called off by e-mail unless another change replaces it.</summary>
    private async Task CancelAsync(
        NpgsqlTransaction transaction, Guid tenantId, PlannedSubscription subscription, PendingChange change, string reason, DateTimeOffset now, CancellationToken ct)
    {
        await SubscriptionChangeSql.SetStatusAsync(transaction, change.Id, "canceled", reason, now, ct).ConfigureAwait(false);
        await BillingSql.AuditAsync(transaction, tenantId, null, BillingSql.System, "subscription.change_canceled", "subscription_change", change.Id.ToString("D"),
            new JsonObject { ["subscription_id"] = subscription.Id.ToString("D"), ["kind"] = change.Kind, ["reason"] = reason }, now, ct).ConfigureAwait(false);
        if (change.Status == "notified" && reason != "replaced")
        {
            await NotifyCanceledAsync(transaction, tenantId, subscription.Id, subscription.ShopId, change, ct).ConfigureAwait(false);
        }
    }

    /// <summary>The e-mail <c>price_change_canceled</c> of a notified change that will not happen.</summary>
    internal async Task NotifyCanceledAsync(NpgsqlTransaction transaction, Guid tenantId, Guid subscriptionId, Guid shopId, PendingChange change, CancellationToken ct)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(localization.Value.TimeZone);
        await notifications.NotifyAsync(transaction, new NotificationRequest(tenantId, shopId, NotificationKinds.PriceChangeCanceled,
            new JsonObject
            {
                ["subscription_id"] = subscriptionId.ToString("D"),
                ["change_id"] = change.Id.ToString("D"),
                ["date"] = SubscriptionChangeSql.Day(SubscriptionChangeSql.LocalDay(change.EffectiveAt, zone)),
            }, NotificationRoutes.Billing, new JsonObject()), ct).ConfigureAwait(false);
    }

    private async Task EnqueueComposeAsync(
        NpgsqlTransaction transaction, Guid tenantId, PlannedSubscription subscription, List<PendingChange> pending, IReadOnlyDictionary<Guid, PriceListSnapshot> lists,
        CancellationToken ct)
    {
        var timeline = SubscriptionTimeline.Build(subscription.Price, subscription.RunningTrialEnd, subscription.ShopOrdinal, pending, Lookup(lists));
        await SubscriptionChangeSql.EnqueueComposeAsync(queue, transaction, tenantId, subscription, timeline.Hash, ct).ConfigureAwait(false);
    }

    private async Task<PlanResult> RefuseAsync(NpgsqlTransaction transaction, Guid tenantId, PlannedSubscription subscription, string problem, DateTimeOffset now, CancellationToken ct)
    {
        await BillingAlerts.RaiseAsync(transaction, tenantId, "billing.alert.change_refused", "subscription", subscription.Id.ToString("D"), now, logger, ct)
            .ConfigureAwait(false);
        logger.LogWarning("billing.change_refused {SubscriptionId} {TenantId} {Problem}", subscription.Id, tenantId, problem);
        return new PlanResult(PlanResult.Refused, Code: problem);
    }

    private static Func<Guid, PriceListSnapshot?> Lookup(IReadOnlyDictionary<Guid, PriceListSnapshot> lists) => id => lists.GetValueOrDefault(id);

    /// <summary>The notes of a target beyond its price (the counted products of a change of the tier).</summary>
    private static JsonObject? Note(JsonObject? to) => to?["products"] is JsonValue products ? new JsonObject { ["products"] = products.DeepClone() } : null;

    private static JsonObject Merge(JsonObject target, JsonObject? note)
    {
        foreach (var (name, value) in note ?? [])
        {
            target[name] = value?.DeepClone();
        }

        return target;
    }

    private static string Amount(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    /// <summary>The end of the locked price of a founder (Npgsql reads <c>timestamptz</c> as a UTC <see cref="DateTime"/>).</summary>
    private static async Task<DateTimeOffset?> FounderUntilAsync(NpgsqlTransaction transaction, Guid tenantId, CancellationToken ct) =>
        await BillingSql.ScalarAsync<DateTime?>(transaction, "SELECT founder_until FROM iam.tenants WHERE id = $1", ct, tenantId).ConfigureAwait(false) is { } until
            ? new DateTimeOffset(DateTime.SpecifyKind(until, DateTimeKind.Utc))
            : null;
}
