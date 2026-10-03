using EshopGuard.Data.Entities.Billing;

namespace EshopGuard.Billing.Pricing;

/// <summary>The tier of a count of products (or pages to check): its row, or <c>custom</c> (by agreement, no price).</summary>
public sealed record TierResolution(PriceTier? Tier, bool IsCustom)
{
    public string? Code => Tier?.Code ?? (IsCustom ? TierResolver.CustomCode : null);
}

/// <summary>
/// The tier by the number of counted products (task 3.1): the bounds come from <c>billing.price_tiers</c> and include both ends
/// (500 is still <c>t500</c>, 501 is <c>t2000</c>). A tier without a price, or a count above the last bound, is <c>custom</c>:
/// an individual offer that cannot be paid.
/// </summary>
public static class TierResolver
{
    public const string CustomCode = "custom";

    public static TierResolution Resolve(IReadOnlyCollection<PriceTier> tiers, int count)
    {
        ArgumentNullException.ThrowIfNull(tiers);
        var tier = tiers.OrderBy(t => t.MinProducts).FirstOrDefault(t => t.MinProducts <= count && (t.MaxProducts is not { } max || count <= max));
        return tier is null || IsCustom(tier) ? new TierResolution(tier, true) : new TierResolution(tier, false);
    }

    /// <summary>A tier without the price of the analysis or of the monitoring (the tier <c>custom</c>).</summary>
    public static bool IsCustom(PriceTier tier)
    {
        ArgumentNullException.ThrowIfNull(tier);
        return tier.AnalysisPrice is null || tier.MonitoringMonthly is null;
    }

    /// <summary>
    /// The tiers of a price list are valid: the first starts at 0, each next one right after the previous one (no gap, no
    /// overlap), only the last may be open, codes are unique, a tier with a price has both prices and they are not negative,
    /// a tier without a price can only be the last. Returns the code of the first problem or null.
    /// </summary>
    public static string? Problem(IReadOnlyList<PriceTier> tiers)
    {
        ArgumentNullException.ThrowIfNull(tiers);
        if (tiers.Count == 0)
        {
            return "empty";
        }

        var ordered = tiers.OrderBy(t => t.MinProducts).ToList();
        if (ordered.Select(t => t.Code).Distinct(StringComparer.Ordinal).Count() != ordered.Count || ordered.Any(t => string.IsNullOrWhiteSpace(t.Code)))
        {
            return "duplicate_code";
        }

        if (ordered[0].MinProducts != 0)
        {
            return "first_not_zero";
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            var tier = ordered[i];
            var last = i == ordered.Count - 1;
            if (tier.MaxProducts is { } max && max < tier.MinProducts)
            {
                return "bounds_reversed";
            }

            if (!last && (tier.MaxProducts is not { } upper || ordered[i + 1].MinProducts != upper + 1))
            {
                return "gap_or_overlap";
            }

            if ((tier.AnalysisPrice is null) != (tier.MonitoringMonthly is null))
            {
                return "price_incomplete";
            }

            if (tier.AnalysisPrice < 0 || tier.MonitoringMonthly < 0 || tier.MonitoringYearly < 0)
            {
                return "price_negative";
            }

            if (IsCustom(tier) && !last)
            {
                return "custom_not_last";
            }
        }

        return null;
    }
}

/// <summary>Fair use (task 4.2, K rozhodnutí 10): other pages above the factor × the counted products are an individual offer.</summary>
public static class FairUsePolicy
{
    /// <summary>The limit of other pages and whether it is exceeded; with the unit <c>pages</c> the other pages are in the count, no limit.</summary>
    public static (int? Limit, bool Exceeded) Evaluate(string? priceUnit, int countedProducts, int? otherPages, decimal factor)
    {
        if (priceUnit == Application.Shops.Scope.PriceUnits.Pages)
        {
            return (null, false);
        }

        var limit = (int)Math.Floor(factor * Math.Max(countedProducts, 1));
        return (limit, otherPages is { } pages && pages > limit);
    }
}

/// <summary>The volume discount by the order of an e-shop in the account (from the n-th e-shop, a coupon only on the monitoring).</summary>
public static class VolumeDiscountResolver
{
    /// <summary>The highest discount whose <c>from_shop_number</c> the order reaches, or null (K rozhodnutí 4: none by default).</summary>
    public static VolumeDiscount? Resolve(IReadOnlyCollection<VolumeDiscount> discounts, int shopOrdinal)
    {
        ArgumentNullException.ThrowIfNull(discounts);
        return discounts.Where(d => d.FromShopNumber <= shopOrdinal).OrderByDescending(d => d.FromShopNumber).FirstOrDefault();
    }
}

/// <summary>Monthly periods anchored at the day of the analysis (31. 1. → 28. 2. → 31. 3.).</summary>
public static class BillingPeriods
{
    /// <summary>
    /// The first start of a period at or after <paramref name="threshold"/> and not before <paramref name="nextStart"/>: the starts
    /// are <paramref name="anchor"/> + whole periods of <paramref name="stepMonths"/> months (12 for a yearly subscription), so a
    /// period of the 31st stays on the 31st (or the last day of a shorter month).
    /// </summary>
    public static DateTimeOffset FirstStartOnOrAfter(DateTimeOffset anchor, DateTimeOffset nextStart, DateTimeOffset threshold, int stepMonths = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stepMonths, 1);
        var floor = threshold > nextStart ? threshold : nextStart;
        for (var months = 0; ; months += stepMonths)
        {
            var start = anchor.AddMonths(months);
            if (start >= floor)
            {
                return start;
            }
        }
    }

    /// <summary>The end of the trial by <c>Billing:TrialMode</c>: the same day and hour of the next month (clamped), or 30 days.</summary>
    public static DateTimeOffset TrialEnd(DateTimeOffset start, string trialMode) =>
        trialMode == TrialModes.Days30 ? start.AddDays(30) : start.AddMonths(1);
}
