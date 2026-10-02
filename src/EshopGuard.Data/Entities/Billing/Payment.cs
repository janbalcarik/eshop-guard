using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Billing;

/// <summary>Payment of an order or a subscription period. Table <c>billing.payments</c>.</summary>
public sealed class Payment : TenantEntity
{
    public Guid? OrderId { get; set; }

    public Guid? SubscriptionId { get; set; }

    public string? StripeInvoiceId { get; set; }

    public string? StripePaymentIntentId { get; set; }

    /// <summary>The charge of the payment (a refund names its charge, change 12).</summary>
    public string? StripeChargeId { get; set; }

    public decimal AmountGross { get; set; }

    public required string Currency { get; set; }

    public PaymentStatus Status { get; set; }

    public string? FailureCode { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    public decimal? RefundedAmount { get; set; }

    public string? CardBrand { get; set; }

    public string? CardLast4 { get; set; }
}
