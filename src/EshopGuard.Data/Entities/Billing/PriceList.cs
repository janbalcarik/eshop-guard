using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Billing;

/// <summary>Version of the price list of a market. Table <c>billing.price_lists</c>.</summary>
public sealed class PriceList : GlobalEntity
{
    public required string Name { get; set; }

    public required string MarketCode { get; set; }

    public required string Currency { get; set; }

    public DateTimeOffset ValidFrom { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public PriceListStatus Status { get; set; }

    public int NoticeDays { get; set; }
}
