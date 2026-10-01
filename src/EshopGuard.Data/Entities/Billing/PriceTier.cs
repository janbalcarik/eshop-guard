using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Billing;

/// <summary>Tier of a price list (500 / 2 000 / 5 000 / 20 000 products). Table <c>billing.price_tiers</c>.</summary>
public sealed class PriceTier : GlobalEntity
{
    public Guid PriceListId { get; set; }

    public required string Code { get; set; }

    public int MinProducts { get; set; }

    public int? MaxProducts { get; set; }

    public decimal AnalysisPrice { get; set; }

    public decimal MonitoringMonthly { get; set; }

    public decimal? MonitoringYearly { get; set; }

    public string? StripePriceAnalysis { get; set; }

    public string? StripePriceMonthly { get; set; }

    public string? StripePriceYearly { get; set; }

    public string? LookupKey { get; set; }
}
