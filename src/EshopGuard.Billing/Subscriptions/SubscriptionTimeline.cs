using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using EshopGuard.Billing.Pricing;

namespace EshopGuard.Billing.Subscriptions;

/// <summary>The kinds of <c>billing.subscription_changes</c> planned by change 12 (group 8).</summary>
public static class ChangeKinds
{
    public const string PriceList = "price_list";
    public const string Tier = "tier";
    public const string Discount = "discount";
}

/// <summary>
/// The price of a subscription at a point of its timeline: the price list, the tier, the Price of Stripe with its unit price and
/// the coupon of the volume discount. <see cref="ToJson"/> is the <c>from</c> / <c>to</c> of a change (design „Plánování“).
/// </summary>
public sealed record PriceState(Guid PriceListId, string TierCode, string Interval, string? StripePriceId, decimal UnitPrice, string? CouponId, decimal? DiscountPercent)
{
    /// <summary>The amount of one period after the discount.</summary>
    public decimal Amount => PriceQuoteService.DiscountedMonthly(UnitPrice, DiscountPercent) ?? UnitPrice;

    public bool SamePrice(PriceState other) =>
        other is not null && string.Equals(StripePriceId, other.StripePriceId, StringComparison.Ordinal) && string.Equals(CouponId, other.CouponId, StringComparison.Ordinal);

    public JsonObject ToJson() => new()
    {
        ["price_list_id"] = PriceListId.ToString("D"),
        ["tier_code"] = TierCode,
        ["stripe_price_id"] = StripePriceId,
        ["coupon_id"] = CouponId,
        ["unit_price"] = UnitPrice,
        ["discount_percent"] = DiscountPercent,
    };

    /// <summary>The state written in a change, null when the JSON lacks the price list or the tier.</summary>
    public static PriceState? FromJson(JsonObject? json, string interval)
    {
        if (json is null || !Guid.TryParse((string?)json["price_list_id"], out var list) || (string?)json["tier_code"] is not { } tier)
        {
            return null;
        }

        return new PriceState(list, tier, interval, (string?)json["stripe_price_id"], json["unit_price"] is JsonValue unit ? unit.GetValue<decimal>() : 0m,
            (string?)json["coupon_id"], json["discount_percent"] is JsonValue percent ? percent.GetValue<decimal>() : null);
    }
}

/// <summary>
/// A change waiting for its period: the kind, the target (the tier, or the price list; a discount follows from the order of the
/// e-shop) and the date. <paramref name="To"/> is the stored target (with its notes such as <c>products</c>).
/// </summary>
public sealed record PendingChange(
    Guid Id, string Kind, DateTimeOffset EffectiveAt, DateTimeOffset CreatedAt, string? TierCode, Guid? PriceListId,
    string Status = "scheduled", DateTimeOffset? NotifiedAt = null, JsonObject? To = null, JsonObject? From = null)
{
    /// <summary>The target of a change of the discount: the coupon it ends with.</summary>
    public string? CouponId => (string?)To?["coupon_id"];
}

/// <summary>A change on the timeline: the price before and after it.</summary>
public sealed record TimelineStep(PendingChange Change, PriceState From, PriceState To);

/// <summary>A phase of the schedule: from <see cref="Start"/> (null = the current phase) the Price and the coupon.</summary>
public sealed record SchedulePhaseSpec(DateTimeOffset? Start, string PriceId, string? CouponId, DateTimeOffset? TrialEnd);

/// <summary>The timeline of a subscription: every change with its prices, the phases of the schedule and their fingerprint.</summary>
public sealed record SubscriptionTimelineResult(IReadOnlyList<TimelineStep> Steps, IReadOnlyList<SchedulePhaseSpec> Phases, string Hash, string? Problem);

/// <summary>
/// The prices of a subscription from now on (design „Skládání“, task 8.2): the pending changes applied in the order of
/// <c>effective_at</c> (and of their creation on the same day) to the current price. A tier takes its Price from the price list
/// in force at that point, a new price list takes the same tier and the volume discount of the order of the e-shop, a change
/// of the discount takes the coupon of the order. Pure: the planner writes the <c>from</c> / <c>to</c> of the changes with it, the
/// composer the phases of the Subscription Schedule. Anything that cannot be charged (a tier missing or by agreement, a Price
/// or a coupon not in Stripe) is a problem: nothing is composed (fail-closed).
/// </summary>
public static class SubscriptionTimeline
{
    public const string TierUnavailable = "billing.schedule_tier_unavailable";
    public const string PriceMissing = "billing.schedule_price_missing";
    public const string CouponMissing = "billing.schedule_coupon_missing";
    public const string PriceListMissing = "billing.schedule_price_list_missing";

    public static SubscriptionTimelineResult Build(
        PriceState current, DateTimeOffset? trialEnd, int? shopOrdinal, IEnumerable<PendingChange> changes, Func<Guid, PriceListSnapshot?> lists)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(lists);
        string? problem = current.StripePriceId is null ? PriceMissing : null;
        var steps = new List<TimelineStep>();
        var phases = new List<SchedulePhaseSpec> { new(null, current.StripePriceId ?? string.Empty, current.CouponId, trialEnd) };
        var state = current;
        foreach (var day in changes.OrderBy(c => c.EffectiveAt).ThenBy(c => c.CreatedAt).GroupBy(c => c.EffectiveAt))
        {
            foreach (var change in day)
            {
                var (next, error) = Apply(state, change, shopOrdinal, lists);
                problem ??= error;
                steps.Add(new TimelineStep(change, state, next));
                state = next;
            }

            if (state.StripePriceId is null)
            {
                problem ??= PriceMissing;
            }

            var last = phases[^1];
            if (!string.Equals(last.PriceId, state.StripePriceId, StringComparison.Ordinal) || !string.Equals(last.CouponId, state.CouponId, StringComparison.Ordinal))
            {
                phases.Add(new SchedulePhaseSpec(day.Key, state.StripePriceId ?? string.Empty, state.CouponId, null));
            }
        }

        return new SubscriptionTimelineResult(steps, phases, Hash(phases), problem);
    }

    /// <summary>The Price and the amount of the tier for the interval (yearly or monthly), null when it cannot be charged.</summary>
    public static (string? PriceId, decimal? Unit) PriceOf(PriceListSnapshot? list, string tierCode, string interval)
    {
        if (list?.Tier(tierCode) is not { } tier || TierResolver.IsCustom(tier))
        {
            return (null, null);
        }

        return interval == "year" ? (tier.StripePriceYearly, tier.MonitoringYearly) : (tier.StripePriceMonthly, tier.MonitoringMonthly);
    }

    /// <summary>
    /// The fingerprint of the phases (the key of idempotence of the schedule): the Price, the coupon, the trial and the start of
    /// every phase but the current one, whose start Stripe keeps.
    /// </summary>
    public static string Hash(IReadOnlyList<SchedulePhaseSpec> phases)
    {
        ArgumentNullException.ThrowIfNull(phases);
        var text = new StringBuilder();
        foreach (var phase in phases)
        {
            text.Append(phase.Start is { } start ? start.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) : "current").Append('|')
                .Append(phase.PriceId).Append('|').Append(phase.CouponId).Append('|')
                .Append(phase.TrialEnd is { } trial ? trial.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) : string.Empty).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..32];
    }

    private static (PriceState State, string? Problem) Apply(PriceState state, PendingChange change, int? ordinal, Func<Guid, PriceListSnapshot?> lists)
    {
        switch (change.Kind)
        {
            case ChangeKinds.Tier when change.TierCode is { } code:
            {
                var (price, unit) = PriceOf(lists(state.PriceListId), code, state.Interval);
                return price is null || unit is null ? (state with { TierCode = code }, TierUnavailable) : (state with { TierCode = code, StripePriceId = price, UnitPrice = unit.Value }, null);
            }

            case ChangeKinds.PriceList when change.PriceListId is { } id:
            {
                if (lists(id) is not { } list)
                {
                    return (state with { PriceListId = id }, PriceListMissing);
                }

                var (price, unit) = PriceOf(list, state.TierCode, state.Interval);
                var next = price is null || unit is null ? state with { PriceListId = id } : state with { PriceListId = id, StripePriceId = price, UnitPrice = unit.Value };
                var (discounted, problem) = ordinal is { } n ? WithDiscount(next, list, n) : (next, null);
                return (discounted, price is null || unit is null ? TierUnavailable : problem);
            }

            case ChangeKinds.Discount:
            {
                if (ordinal is not { } n)
                {
                    return (state, null);
                }

                return lists(state.PriceListId) is { } list ? WithDiscount(state, list, n) : (state, PriceListMissing);
            }

            default:
                return (state, null);
        }
    }

    private static (PriceState State, string? Problem) WithDiscount(PriceState state, PriceListSnapshot list, int ordinal)
    {
        var discount = VolumeDiscountResolver.Resolve(list.Discounts, ordinal);
        if (discount is null)
        {
            return (state with { CouponId = null, DiscountPercent = null }, null);
        }

        return discount.StripeCouponId is { } coupon
            ? (state with { CouponId = coupon, DiscountPercent = discount.Percent }, null)
            : (state with { DiscountPercent = discount.Percent }, CouponMissing);
    }
}
