namespace EshopGuard.Billing.Stripe;

/// <summary>Stripe switched off (<c>Billing:Stripe:Mode = disabled</c>): every call throws <see cref="BillingUnavailableException"/>.</summary>
internal sealed class DisabledStripeGateway : IStripeGateway
{
    public Task<string> CreateProductAsync(StripeProductRequest request, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<string> CreatePriceAsync(StripePriceRequest request, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task TransferLookupKeyAsync(string priceId, string lookupKey, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task SetPriceActiveAsync(string priceId, bool active, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<string> CreateCouponAsync(StripeCouponRequest request, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<string> CreateCustomerAsync(StripeCustomerRequest request, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task UpdateCustomerAsync(string customerId, StripeCustomerRequest request, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<StripeCustomerState> GetCustomerAsync(string customerId, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<StripeTaxIdState> CreateTaxIdAsync(string customerId, string type, string value, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<StripeCheckoutSession> CreateCheckoutSessionAsync(StripeCheckoutRequest request, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<StripeCheckoutSession> GetCheckoutSessionAsync(string sessionId, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<StripeSubscriptionState> CreateSubscriptionAsync(StripeSubscriptionRequest request, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<StripeSubscriptionState> GetSubscriptionAsync(string subscriptionId, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<StripeSubscriptionState> SetCancelAtPeriodEndAsync(string subscriptionId, bool cancel, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<StripeSubscriptionState> CancelSubscriptionAsync(string subscriptionId, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task SetSubscriptionPaymentMethodAsync(string subscriptionId, string paymentMethodId, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<StripeScheduleState> CreateScheduleFromSubscriptionAsync(string subscriptionId, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<StripeScheduleState> UpdateScheduleAsync(string scheduleId, IReadOnlyList<StripeSchedulePhase> phases, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task ReleaseScheduleAsync(string scheduleId, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<StripeScheduleState> GetScheduleAsync(string scheduleId, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<StripeInvoiceState> GetInvoiceAsync(string invoiceId, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<StripeChargeState> GetChargeAsync(string chargeId, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<StripePaymentMethodState?> GetPaymentMethodAsync(string paymentMethodId, CancellationToken ct) => throw new BillingUnavailableException();

    public Task SetCustomerDefaultPaymentMethodAsync(string customerId, string paymentMethodId, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task DetachPaymentMethodAsync(string paymentMethodId, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<string> CreatePortalSessionAsync(string customerId, string returnUrl, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<string> CreateSetupIntentAsync(string customerId, string idempotencyKey, CancellationToken ct) => throw new BillingUnavailableException();

    public Task<IReadOnlyList<StripeEventEnvelope>> ListEventsAsync(DateTimeOffset since, IReadOnlyCollection<string> types, CancellationToken ct) => throw new BillingUnavailableException();
}
