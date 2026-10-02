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

/// <summary>Mode of Stripe a price list or a coupon was created in (change 12).</summary>
public enum StripeMode
{
    Test,
    Live,
}

/// <summary>State of the synchronization of a price list to Stripe (change 12).</summary>
public enum PriceListSyncStatus
{
    Pending,
    Synced,
    Failed,
}

/// <summary>State of a quote (change 12): payable, only an individual offer, or no price list.</summary>
public enum PriceQuoteStatus
{
    Offer,
    IndividualOffer,
    Unavailable,
}

/// <summary>Tax treatment of the buyer (change 12, design „Daňový režim“).</summary>
public enum TaxTreatment
{
    DomesticVat,
    ReverseCharge,
    PendingVerification,
    Undetermined,
}

/// <summary>State of a scheduled change of a subscription (change 12).</summary>
public enum SubscriptionChangeStatus
{
    Scheduled,
    Notified,
    Applied,
    Canceled,
}

/// <summary>State of an invoice in SuperFaktúra (change 12).</summary>
public enum InvoiceStatus
{
    Creating,
    Issued,
    NeedsReview,
    Failed,
}

/// <summary>What an invoice was issued for: a paid invoice of Stripe, or a refund (credit note).</summary>
public enum InvoiceSourceKind
{
    StripeInvoice,
    StripeRefund,
}

/// <summary>Delivery of an invoice by e-mail (the e-invoice has <see cref="EinvoiceStatus"/>).</summary>
public enum InvoiceEmailStatus
{
    NotRequired,
    Pending,
    Sent,
    Failed,
}

/// <summary>State of a received event of Stripe (change 12).</summary>
public enum StripeEventStatus
{
    Received,
    Processed,
    Ignored,
    Failed,
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
