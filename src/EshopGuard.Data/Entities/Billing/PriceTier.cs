using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Billing;

/// <summary>Tier of a price list (500 / 2 000 / 5 000 / 20 000 products). Table <c>billing.price_tiers</c>.</summary>
public sealed class PriceTier : GlobalEntity
{
    public Guid PriceListId { get; set; }

    public required string Code { get; set; }

    public int MinProducts { get; set; }

    public int? MaxProducts { get; set; }

    /// <summary>Null in the tier <c>custom</c> (price by agreement, no prices in Stripe).</summary>
    public decimal? AnalysisPrice { get; set; }

    public decimal? MonitoringMonthly { get; set; }

    public decimal? MonitoringYearly { get; set; }

    public string? StripePriceAnalysis { get; set; }

    public string? StripePriceMonthly { get; set; }

    public string? StripePriceYearly { get; set; }

    /// <summary>Lookup keys of the three prices, e.g. <c>sk_eur_t2000_monthly</c> (change 12: one price, one key).</summary>
    public string? LookupKeyAnalysis { get; set; }

    public string? LookupKeyMonthly { get; set; }

    public string? LookupKeyYearly { get; set; }

    /// <summary>When its prices were archived in Stripe (no running subscription, open order or scheduled change uses them).</summary>
    public DateTimeOffset? ArchivedAt { get; set; }
}
