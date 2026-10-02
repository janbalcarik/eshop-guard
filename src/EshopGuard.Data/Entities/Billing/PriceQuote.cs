using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Billing;

/// <summary>
/// A price the customer saw for a scope (change 12, AD 3): one row per e-shop, <c>scope_hash</c> and price list, so the
/// guaranteed price of an order can be traced. Table <c>billing.price_quotes</c>.
/// </summary>
public sealed class PriceQuote : TenantEntity
{
    public Guid ShopId { get; set; }

    /// <summary>The free sample the scope is based on.</summary>
    public Guid? BasisRunId { get; set; }

    public required string ScopeHash { get; set; }

    /// <summary>Null when no price list is active for the market and currency (<see cref="PriceQuoteStatus.Unavailable"/>).</summary>
    public Guid? PriceListId { get; set; }

    public string? TierCode { get; set; }

    /// <summary><c>products</c> or <c>pages</c> (the unit of <see cref="CountedProducts"/>).</summary>
    public string? PriceUnit { get; set; }

    public int? CountedProducts { get; set; }

    public int? OtherPages { get; set; }

    /// <summary>The checked and not checked versions of the scope (codes and numbers only).</summary>
    public required JsonDocument Versions { get; set; }

    public string[] Markets { get; set; } = [];

    public decimal? AnalysisPrice { get; set; }

    public decimal? MonitoringMonthly { get; set; }

    public decimal? DiscountPercent { get; set; }

    public required string Currency { get; set; }

    public JsonDocument? VatPreview { get; set; }

    public JsonDocument? FairUse { get; set; }

    public PriceQuoteStatus Status { get; set; }

    public string? ReasonCode { get; set; }

    public Guid? CreatedBy { get; set; }
}
