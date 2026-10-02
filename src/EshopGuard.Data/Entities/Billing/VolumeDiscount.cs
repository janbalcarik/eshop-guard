using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Billing;

/// <summary>Volume discount from the n-th e-shop. Table <c>billing.volume_discounts</c>.</summary>
public sealed class VolumeDiscount : GlobalEntity
{
    public Guid PriceListId { get; set; }

    public int FromShopNumber { get; set; }

    public decimal Percent { get; set; }

    public string? StripeCouponId { get; set; }

    public StripeMode? StripeMode { get; set; }
}
