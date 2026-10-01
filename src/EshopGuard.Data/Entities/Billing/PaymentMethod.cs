using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Billing;

/// <summary>Saved payment card of a tenant ("VISA •••• 4242"). Table <c>billing.payment_methods</c>.</summary>
public sealed class PaymentMethod : TenantEntity
{
    public required string StripePaymentMethodId { get; set; }

    public string? Brand { get; set; }

    public string? Last4 { get; set; }

    public int? ExpMonth { get; set; }

    public int? ExpYear { get; set; }

    public bool IsDefault { get; set; }

    public DateTimeOffset? DetachedAt { get; set; }
}
