using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Billing;

/// <summary>Promo code (tenant_id null = public). Table <c>billing.promo_codes</c>.</summary>
public sealed class PromoCode : GlobalEntity
{
    public required string Code { get; set; }

    public decimal Percent { get; set; }

    public int? DurationMonths { get; set; }

    public DateTimeOffset? ValidUntil { get; set; }

    public int? MaxRedemptions { get; set; }

    public string? StripePromotionCodeId { get; set; }

    public Guid? TenantId { get; set; }
}
