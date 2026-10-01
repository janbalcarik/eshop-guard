using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Shops;

/// <summary>Place of sale of an e-shop (including the home country). Table <c>shop.shop_markets</c>.</summary>
public sealed class ShopMarket : ITenantOwned, IHasTimestamps
{
    public Guid TenantId { get; set; }

    public Guid ShopId { get; set; }

    public required string CountryCode { get; set; }

    public bool IsHome { get; set; }

    public ShopMarketStatus Status { get; set; }

    public EvidenceLevel? EvidenceLevel { get; set; }

    public MarketSource Source { get; set; }

    public JsonDocument? Evidence { get; set; }

    public Guid? DetectionRunId { get; set; }

    public Guid? ConfirmedBy { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
