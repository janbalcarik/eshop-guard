namespace EshopGuard.Billing.Stripe;

/// <summary>
/// The calls of Stripe this change needs (design of change 12, „Vnější služby za rozhraním“). Every call that writes has the
/// parameter <c>idempotencyKey</c> (a retried job never creates an object twice); reads have none. The implementation over
/// Stripe.net is <see cref="StripeGateway"/>, the tests use a fake. With <c>Billing:Stripe:Mode = disabled</c> every call
/// throws <see cref="BillingUnavailableException"/>.
/// </summary>
public interface IStripeGateway
{
    // Catalog: products, prices, coupons.
    Task<string> CreateProductAsync(StripeProductRequest request, string idempotencyKey, CancellationToken ct);

    Task<string> CreatePriceAsync(StripePriceRequest request, string idempotencyKey, CancellationToken ct);

    /// <summary>Moves <paramref name="lookupKey"/> to the price (<c>transfer_lookup_key = true</c>).</summary>
    Task TransferLookupKeyAsync(string priceId, string lookupKey, string idempotencyKey, CancellationToken ct);

    /// <summary>Archives (<c>active = false</c>) or activates a price.</summary>
    Task SetPriceActiveAsync(string priceId, bool active, string idempotencyKey, CancellationToken ct);

    Task<string> CreateCouponAsync(StripeCouponRequest request, string idempotencyKey, CancellationToken ct);

    // Customer of a tenant.
    Task<string> CreateCustomerAsync(StripeCustomerRequest request, string idempotencyKey, CancellationToken ct);

    Task UpdateCustomerAsync(string customerId, StripeCustomerRequest request, string idempotencyKey, CancellationToken ct);

    Task<StripeCustomerState> GetCustomerAsync(string customerId, CancellationToken ct);

    /// <summary>A tax id (<c>eu_vat</c>) from the data of the tenant, not from Checkout (AD 9).</summary>
    Task<StripeTaxIdState> CreateTaxIdAsync(string customerId, string type, string value, string idempotencyKey, CancellationToken ct);

    // Checkout.
    Task<StripeCheckoutSession> CreateCheckoutSessionAsync(StripeCheckoutRequest request, string idempotencyKey, CancellationToken ct);

    Task<StripeCheckoutSession> GetCheckoutSessionAsync(string sessionId, CancellationToken ct);

    // Subscriptions and their schedules.
    Task<StripeSubscriptionState> CreateSubscriptionAsync(StripeSubscriptionRequest request, string idempotencyKey, CancellationToken ct);

    Task<StripeSubscriptionState> GetSubscriptionAsync(string subscriptionId, CancellationToken ct);

    Task<StripeSubscriptionState> SetCancelAtPeriodEndAsync(string subscriptionId, bool cancel, string idempotencyKey, CancellationToken ct);

    /// <summary>Cancels at once (an <c>incomplete</c> subscription whose first payment was never confirmed).</summary>
    Task<StripeSubscriptionState> CancelSubscriptionAsync(string subscriptionId, string idempotencyKey, CancellationToken ct);

    Task SetSubscriptionPaymentMethodAsync(string subscriptionId, string paymentMethodId, string idempotencyKey, CancellationToken ct);

    /// <summary>A schedule from the running subscription (<c>from_subscription</c>) with its current phase.</summary>
    Task<StripeScheduleState> CreateScheduleFromSubscriptionAsync(string subscriptionId, string idempotencyKey, CancellationToken ct);

    /// <summary>The phases of the schedule (<c>end_behavior = release</c>, <c>proration_behavior = none</c>).</summary>
    Task<StripeScheduleState> UpdateScheduleAsync(string scheduleId, IReadOnlyList<StripeSchedulePhase> phases, string idempotencyKey, CancellationToken ct);

    /// <summary>Releases the schedule: the subscription keeps its current price and no phase follows.</summary>
    Task ReleaseScheduleAsync(string scheduleId, string idempotencyKey, CancellationToken ct);

    Task<StripeScheduleState> GetScheduleAsync(string scheduleId, CancellationToken ct);

    // Invoices, charges and refunds (read only: Stripe creates them).
    Task<StripeInvoiceState> GetInvoiceAsync(string invoiceId, CancellationToken ct);

    Task<StripeChargeState> GetChargeAsync(string chargeId, CancellationToken ct);

    // The card of the account.
    Task<StripePaymentMethodState?> GetPaymentMethodAsync(string paymentMethodId, CancellationToken ct);

    Task SetCustomerDefaultPaymentMethodAsync(string customerId, string paymentMethodId, string idempotencyKey, CancellationToken ct);

    Task DetachPaymentMethodAsync(string paymentMethodId, string idempotencyKey, CancellationToken ct);

    /// <summary>The customer portal with the flow <c>payment_method_update</c>; returns its URL.</summary>
    Task<string> CreatePortalSessionAsync(string customerId, string returnUrl, string idempotencyKey, CancellationToken ct);

    /// <summary>A SetupIntent for the Payment Element (the fallback of the portal); returns its client secret.</summary>
    Task<string> CreateSetupIntentAsync(string customerId, string idempotencyKey, CancellationToken ct);

    // Events (nightly reconciliation of lost webhooks).
    Task<IReadOnlyList<StripeEventEnvelope>> ListEventsAsync(DateTimeOffset since, IReadOnlyCollection<string> types, CancellationToken ct);
}

/// <summary>Stripe is switched off (<c>Billing:Stripe:Mode = disabled</c>): the API answers <c>503 billing.unavailable</c>.</summary>
public sealed class BillingUnavailableException() : Exception("billing.unavailable");

/// <summary>
/// An error of Stripe as a code: <paramref name="Transient"/> for network errors, 429 and 5xx (the job tries again), else
/// permanent. The message is the code and the HTTP status only (never the body, which may echo data).
/// </summary>
public sealed class StripeGatewayException(string code, int? httpStatus, bool transient)
    : Exception($"{code}{(httpStatus is { } s ? " " + s.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty)}")
{
    public string Code { get; } = code;

    public int? HttpStatus { get; } = httpStatus;

    public bool Transient { get; } = transient;
}
