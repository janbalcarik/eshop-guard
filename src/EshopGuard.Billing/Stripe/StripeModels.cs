namespace EshopGuard.Billing.Stripe;

// The objects of Stripe as this change needs them: the gateway maps the types of Stripe.net to these records, so the logic and
// its tests never see Stripe.net. Amounts are in minor units (cents, haléře) as in Stripe.

/// <summary>A product of the catalog (analysis, monitoring).</summary>
public sealed record StripeProductRequest(string Name, IReadOnlyDictionary<string, string> Metadata);

/// <summary>
/// A price; <paramref name="RecurringInterval"/> <c>month</c>, <c>year</c> or null (one-off). Always <c>tax_behavior = exclusive</c>.
/// Without a lookup key: Stripe refuses a key another price has, so the key moves only at the activation (<c>transfer_lookup_key</c>).
/// </summary>
public sealed record StripePriceRequest(
    string ProductId, string Currency, long UnitAmount, string? RecurringInterval, string? LookupKey, IReadOnlyDictionary<string, string> Metadata);

/// <summary>A coupon <c>percent_off</c>, <c>duration = forever</c>, only for <paramref name="AppliesToProductId"/>.</summary>
public sealed record StripeCouponRequest(decimal PercentOff, string AppliesToProductId, string Name, IReadOnlyDictionary<string, string> Metadata);

public sealed record StripeAddress(string? Line1, string? City, string? PostalCode, string Country);

/// <summary>The customer of a tenant (one per tenant).</summary>
public sealed record StripeCustomerRequest(string Name, string? Email, StripeAddress Address, string PreferredLocale, IReadOnlyDictionary<string, string> Metadata);

/// <summary>A tax id of a customer; <paramref name="VerificationStatus"/> <c>pending</c>, <c>verified</c>, <c>unverified</c> or <c>unavailable</c>.</summary>
public sealed record StripeTaxIdState(string Id, string Type, string Value, string VerificationStatus);

public sealed record StripeCustomerState(
    string Id, string? DefaultPaymentMethodId, IReadOnlyList<StripeTaxIdState> TaxIds, bool Deleted, IReadOnlyDictionary<string, string> Metadata);

/// <summary>
/// A Checkout Session in the mode <c>subscription</c>: the one-off analysis and the monitoring with a trial, automatic tax, the
/// language of the tenant, the sentence about the monthly payment and canceling.
/// </summary>
public sealed record StripeCheckoutRequest(
    string CustomerId,
    string AnalysisPriceId,
    string MonitoringPriceId,
    string? CouponId,
    DateTimeOffset TrialEnd,
    string Locale,
    string SubmitMessage,
    string SuccessUrl,
    string CancelUrl,
    DateTimeOffset ExpiresAt,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>A Checkout Session; <paramref name="Status"/> <c>open</c>, <c>complete</c> or <c>expired</c>, <paramref name="PaymentStatus"/> <c>paid</c>, <c>unpaid</c>, <c>no_payment_required</c>.</summary>
public sealed record StripeCheckoutSession(
    string Id, string? Url, string Status, string? PaymentStatus, string? CustomerId, string? SubscriptionId, DateTimeOffset ExpiresAt,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>A subscription created through the API (a saved card, or monitoring started again without a trial).</summary>
public sealed record StripeSubscriptionRequest(
    string CustomerId,
    string MonitoringPriceId,
    string? AnalysisPriceId,
    string? CouponId,
    DateTimeOffset? TrialEnd,
    string DefaultPaymentMethodId,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>
/// A subscription as Stripe has it now. <paramref name="PaymentClientSecret"/> is the secret of the first payment when the bank
/// asks for 3-D Secure (a subscription <c>incomplete</c> created with <c>default_incomplete</c>).
/// </summary>
public sealed record StripeSubscriptionState(
    string Id,
    string CustomerId,
    string Status,
    string? PriceId,
    string? CouponId,
    DateTimeOffset? TrialEnd,
    DateTimeOffset? CurrentPeriodStart,
    DateTimeOffset? CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    DateTimeOffset? CanceledAt,
    DateTimeOffset? EndedAt,
    string? ScheduleId,
    string? DefaultPaymentMethodId,
    string? LatestInvoiceId,
    string? PaymentClientSecret,
    DateTimeOffset Created,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>
/// A phase of a Subscription Schedule: from <paramref name="StartDate"/> the price and the coupon; the last phase has no end.
/// <paramref name="TrialEnd"/> keeps the trial of the current phase (a phase without it would end the trial).
/// </summary>
public sealed record StripeSchedulePhase(DateTimeOffset StartDate, DateTimeOffset? EndDate, string PriceId, string? CouponId, DateTimeOffset? TrialEnd = null);

public sealed record StripeScheduleState(string Id, string Status, string? SubscriptionId, IReadOnlyList<StripeSchedulePhase> Phases);

/// <summary>A line of an invoice of Stripe.</summary>
public sealed record StripeInvoiceLine(
    string Id, string? Description, long Amount, long? Quantity, string? PriceId, DateTimeOffset? PeriodStart, DateTimeOffset? PeriodEnd,
    long DiscountAmount, long TaxAmount);

/// <summary>
/// An invoice of Stripe; <paramref name="ReverseCharge"/> when a tax line has the reason <c>reverse_charge</c>,
/// <paramref name="TaxAmount"/> the sum of the taxes.
/// </summary>
public sealed record StripeInvoiceState(
    string Id,
    string CustomerId,
    string? SubscriptionId,
    string Status,
    string Currency,
    long AmountPaid,
    long Subtotal,
    long Total,
    long TotalExcludingTax,
    long TaxAmount,
    bool ReverseCharge,
    string? CustomerTaxExempt,
    string? BillingReason,
    DateTimeOffset Created,
    DateTimeOffset? PaidAt,
    string? PaymentIntentId,
    IReadOnlyList<StripeInvoiceLine> Lines,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record StripeRefundState(string Id, long Amount, string Status, DateTimeOffset Created);

/// <summary>A charge with its refunds and the invoice it paid.</summary>
public sealed record StripeChargeState(
    string Id, string? CustomerId, string? PaymentIntentId, string? InvoiceId, string Currency, long Amount, long AmountRefunded,
    IReadOnlyList<StripeRefundState> Refunds);

/// <summary>A card.</summary>
public sealed record StripePaymentMethodState(string Id, string? CustomerId, string? Brand, string? Last4, int? ExpMonth, int? ExpYear);

/// <summary>A SetupIntent: the card saved by the customer in the Payment Element once it <c>succeeded</c>.</summary>
public sealed record StripeSetupIntentState(string Id, string? CustomerId, string Status, string? PaymentMethodId, IReadOnlyDictionary<string, string> Metadata)
{
    /// <summary>The metadata <c>purpose</c> of a SetupIntent that changes the card of the account.</summary>
    public const string AccountCard = "account_card";
}

/// <summary>An event (webhook or the list of events): its id, type, the id of its object and the raw JSON.</summary>
public sealed record StripeEventEnvelope(string Id, string Type, string? ObjectId, bool Livemode, DateTimeOffset Created, string Json);
