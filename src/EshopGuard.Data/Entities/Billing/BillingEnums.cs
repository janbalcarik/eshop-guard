namespace EshopGuard.Data.Entities.Billing;

/// <summary>Values of <c>PriceListStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum PriceListStatus
{
    Draft,
    Published,
    Retired,
}

/// <summary>Values of <c>OrderKind</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum OrderKind
{
    AnalysisWithTrial,
    Custom,
}

/// <summary>Values of <c>OrderStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum OrderStatus
{
    Created,
    CheckoutOpen,
    Paid,
    Expired,
    Canceled,
    Refunded,
}

/// <summary>Values of <c>SubscriptionStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum SubscriptionStatus
{
    Trialing,
    Active,
    PastDue,
    Canceled,
    Incomplete,
    Paused,
}

/// <summary>Values of <c>BillingInterval</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum BillingInterval
{
    Month,
    Year,
}

/// <summary>Values of <c>SubscriptionChangeKind</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum SubscriptionChangeKind
{
    PriceList,
    Tier,
    Discount,
    Interval,
    Cancel,
}

/// <summary>Values of <c>PaymentStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum PaymentStatus
{
    Succeeded,
    Failed,
    Refunded,
    PartiallyRefunded,
}

/// <summary>Values of <c>InvoiceKind</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum InvoiceKind
{
    Invoice,
    CreditNote,
    Proforma,
}

/// <summary>Values of <c>EinvoiceStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum EinvoiceStatus
{
    NotRequired,
    Queued,
    Sent,
    Delivered,
    Failed,
}
