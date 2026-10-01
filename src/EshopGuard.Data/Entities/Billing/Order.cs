using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Billing;

/// <summary>Order of an analysis ("Dnes zaplatíte"). Table <c>billing.orders</c>.</summary>
public sealed class Order : TenantEntity
{
    public Guid ShopId { get; set; }

    public OrderKind Kind { get; set; }

    public Guid PriceListId { get; set; }

    public string? TierCode { get; set; }

    public decimal AmountNet { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal VatRate { get; set; }

    public decimal VatAmount { get; set; }

    public decimal AmountGross { get; set; }

    public required string Currency { get; set; }

    public OrderStatus Status { get; set; }

    public string? StripeCheckoutSessionId { get; set; }

    public string? StripePaymentIntentId { get; set; }

    public Guid? RunId { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? PaidAt { get; set; }
}
