using System.Collections.Concurrent;
using System.Globalization;
using EshopGuard.Billing.Stripe;

namespace EshopGuard.Tests.Shared;

/// <summary>
/// Stripe without network for the tests (design of change 12): objects in memory, every call recorded with its idempotency key,
/// the same key returns the same object (as Stripe does for 24 hours), writes can fail on demand (<see cref="FailWritesAfter"/>,
/// <see cref="FailNext"/>). Tests change the objects directly to play what happened in Stripe (a payment, a new card, a refund).
/// </summary>
internal sealed class FakeStripeGateway : IStripeGateway
{
    private readonly ConcurrentDictionary<string, object> byKey = new(StringComparer.Ordinal);
    private readonly string run = Guid.NewGuid().ToString("N")[..8];
    private int sequence;
    private int writes;

    /// <summary>Every call: the method, the idempotency key (null for reads) and the main argument.</summary>
    public ConcurrentQueue<(string Method, string? Key, string? Argument)> Calls { get; } = new();

    /// <summary>After this many successful new writes every write fails with a 500 of Stripe (null = never).</summary>
    public int? FailWritesAfter { get; set; }

    /// <summary>After this many prices every new price fails with a 500 of Stripe (null = never).</summary>
    public int? FailCreatePriceAfter { get; set; }

    /// <summary>The next call (read or write) fails with this error.</summary>
    public StripeGatewayException? FailNext { get; set; }

    public ConcurrentDictionary<string, StripePriceRequest> Prices { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, bool> PriceActive { get; } = new(StringComparer.Ordinal);

    /// <summary>lookup_key → id of the price that has it.</summary>
    public ConcurrentDictionary<string, string> LookupKeys { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, StripeCouponRequest> Coupons { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, StripeProductRequest> Products { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, StripeCustomerState> Customers { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, StripeCustomerRequest> CustomerRequests { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, StripeCheckoutSession> Sessions { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, StripeCheckoutRequest> SessionRequests { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, StripeSubscriptionState> Subscriptions { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, StripeSubscriptionRequest> SubscriptionRequests { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, StripeScheduleState> Schedules { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, StripeInvoiceState> Invoices { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, StripeChargeState> Charges { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, StripePaymentMethodState> PaymentMethods { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, StripeSetupIntentState> SetupIntents { get; } = new(StringComparer.Ordinal);

    public ConcurrentQueue<StripeEventEnvelope> Events { get; } = new();

    /// <summary>The status a new subscription created through the API gets (<c>trialing</c>, or <c>incomplete</c> for 3-D Secure).</summary>
    public string NewSubscriptionStatus { get; set; } = "trialing";

    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

    public int Count(string method) => Calls.Count(c => c.Method == method);

    public Task<string> CreateProductAsync(StripeProductRequest request, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(CreateProductAsync), idempotencyKey, request.Name, () =>
        {
            var id = NewId("prod");
            Products[id] = request;
            return id;
        });

    public Task<string> CreatePriceAsync(StripePriceRequest request, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(CreatePriceAsync), idempotencyKey, request.LookupKey, () =>
        {
            if (FailCreatePriceAfter is { } limit && Prices.Count >= limit)
            {
                throw new StripeGatewayException("stripe.api_error", 500, true);
            }

            var id = NewId("price");
            if (request.LookupKey is { } key && LookupKeys.ContainsKey(key))
            {
                // Stripe refuses a lookup key another price has (without transfer_lookup_key).
                throw new StripeGatewayException("stripe.lookup_key_in_use", 400, false);
            }

            Prices[id] = request;
            PriceActive[id] = true;
            if (request.LookupKey is { } lookup)
            {
                LookupKeys[lookup] = id;
            }

            return id;
        });

    public Task TransferLookupKeyAsync(string priceId, string lookupKey, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(TransferLookupKeyAsync), idempotencyKey, priceId, () =>
        {
            LookupKeys[lookupKey] = priceId;
            return true;
        });

    public Task SetPriceActiveAsync(string priceId, bool active, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(SetPriceActiveAsync), idempotencyKey, priceId, () =>
        {
            PriceActive[priceId] = active;
            return true;
        });

    public Task<string> CreateCouponAsync(StripeCouponRequest request, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(CreateCouponAsync), idempotencyKey, request.Name, () =>
        {
            var id = NewId("coupon");
            Coupons[id] = request;
            return id;
        });

    public Task<string> CreateCustomerAsync(StripeCustomerRequest request, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(CreateCustomerAsync), idempotencyKey, request.Name, () =>
        {
            var id = NewId("cus");
            Customers[id] = new StripeCustomerState(id, null, [], false, request.Metadata);
            CustomerRequests[id] = request;
            return id;
        });

    public Task UpdateCustomerAsync(string customerId, StripeCustomerRequest request, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(UpdateCustomerAsync), idempotencyKey, customerId, () =>
        {
            CustomerRequests[customerId] = request;
            return true;
        });

    public Task<StripeCustomerState> GetCustomerAsync(string customerId, CancellationToken ct) =>
        ReadAsync(nameof(GetCustomerAsync), customerId, () => Customers[customerId]);

    public Task<StripeTaxIdState> CreateTaxIdAsync(string customerId, string type, string value, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(CreateTaxIdAsync), idempotencyKey, customerId, () =>
        {
            var taxId = new StripeTaxIdState(NewId("txi"), type, value, "pending");
            var customer = Customers[customerId];
            Customers[customerId] = customer with { TaxIds = [.. customer.TaxIds, taxId] };
            return taxId;
        });

    public Task DeleteTaxIdAsync(string customerId, string taxIdId, CancellationToken ct)
    {
        Calls.Enqueue((nameof(DeleteTaxIdAsync), null, taxIdId));
        ThrowIfAsked();
        var customer = Customers[customerId];
        Customers[customerId] = customer with { TaxIds = [.. customer.TaxIds.Where(t => t.Id != taxIdId)] };
        return Task.CompletedTask;
    }

    public Task<StripeCheckoutSession> CreateCheckoutSessionAsync(StripeCheckoutRequest request, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(CreateCheckoutSessionAsync), idempotencyKey, request.CustomerId, () =>
        {
            var id = NewId("cs_test");
            var session = new StripeCheckoutSession(id, "https://checkout.stripe.test/c/pay/" + id, "open", "unpaid", request.CustomerId, null, request.ExpiresAt, request.Metadata);
            Sessions[id] = session;
            SessionRequests[id] = request;
            return session;
        });

    public Task<StripeCheckoutSession> GetCheckoutSessionAsync(string sessionId, CancellationToken ct) =>
        ReadAsync(nameof(GetCheckoutSessionAsync), sessionId, () => Sessions[sessionId]);

    public Task<StripeCheckoutSession> ExpireCheckoutSessionAsync(string sessionId, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(ExpireCheckoutSessionAsync), idempotencyKey, sessionId, () =>
            Sessions[sessionId] = Sessions[sessionId] with { Status = "expired" });

    public Task<StripeSubscriptionState> CreateSubscriptionAsync(StripeSubscriptionRequest request, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(CreateSubscriptionAsync), idempotencyKey, request.CustomerId, () =>
        {
            var id = NewId("sub");
            var start = Now;
            var end = request.TrialEnd ?? start.AddMonths(1);
            var subscription = new StripeSubscriptionState(
                id, request.CustomerId, request.TrialEnd is null && NewSubscriptionStatus == "trialing" ? "active" : NewSubscriptionStatus, request.MonitoringPriceId,
                request.CouponId, request.TrialEnd, start, end, false, null, null, null, request.DefaultPaymentMethodId, NewId("in"),
                NewSubscriptionStatus == "incomplete" ? "pi_secret_" + id : null, start, request.Metadata);
            Subscriptions[id] = subscription;
            SubscriptionRequests[id] = request;
            return subscription;
        });

    public Task<StripeSubscriptionState> GetSubscriptionAsync(string subscriptionId, CancellationToken ct) =>
        ReadAsync(nameof(GetSubscriptionAsync), subscriptionId, () => Subscriptions[subscriptionId]);

    public Task<StripeSubscriptionState> SetCancelAtPeriodEndAsync(string subscriptionId, bool cancel, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(SetCancelAtPeriodEndAsync), idempotencyKey, subscriptionId, () =>
            Subscriptions[subscriptionId] = Subscriptions[subscriptionId] with { CancelAtPeriodEnd = cancel });

    public Task<StripeSubscriptionState> CancelSubscriptionAsync(string subscriptionId, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(CancelSubscriptionAsync), idempotencyKey, subscriptionId, () =>
            Subscriptions[subscriptionId] = Subscriptions[subscriptionId] with { Status = "canceled", CanceledAt = Now, EndedAt = Now });

    public Task SetSubscriptionPaymentMethodAsync(string subscriptionId, string paymentMethodId, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(SetSubscriptionPaymentMethodAsync), idempotencyKey, subscriptionId, () =>
            Subscriptions[subscriptionId] = Subscriptions[subscriptionId] with { DefaultPaymentMethodId = paymentMethodId });

    public Task<StripeScheduleState> CreateScheduleFromSubscriptionAsync(string subscriptionId, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(CreateScheduleFromSubscriptionAsync), idempotencyKey, subscriptionId, () =>
        {
            var subscription = Subscriptions[subscriptionId];
            var id = NewId("sub_sched");
            var schedule = new StripeScheduleState(id, "active", subscriptionId,
                [new StripeSchedulePhase(subscription.CurrentPeriodStart ?? Now, subscription.CurrentPeriodEnd, subscription.PriceId ?? string.Empty, subscription.CouponId,
                    subscription.Status == "trialing" ? subscription.TrialEnd : null)]);
            Schedules[id] = schedule;
            Subscriptions[subscriptionId] = subscription with { ScheduleId = id };
            return schedule;
        });

    public Task<StripeScheduleState> UpdateScheduleAsync(string scheduleId, IReadOnlyList<StripeSchedulePhase> phases, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(UpdateScheduleAsync), idempotencyKey, scheduleId, () => Schedules[scheduleId] = Schedules[scheduleId] with { Phases = phases.ToList() });

    public Task ReleaseScheduleAsync(string scheduleId, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(ReleaseScheduleAsync), idempotencyKey, scheduleId, () =>
        {
            var schedule = Schedules[scheduleId] = Schedules[scheduleId] with { Status = "released" };
            if (schedule.SubscriptionId is { } subscription && Subscriptions.TryGetValue(subscription, out var state))
            {
                Subscriptions[subscription] = state with { ScheduleId = null };
            }

            return true;
        });

    public Task<StripeScheduleState> GetScheduleAsync(string scheduleId, CancellationToken ct) =>
        ReadAsync(nameof(GetScheduleAsync), scheduleId, () => Schedules[scheduleId]);

    public Task<StripeInvoiceState> GetInvoiceAsync(string invoiceId, CancellationToken ct) =>
        ReadAsync(nameof(GetInvoiceAsync), invoiceId, () => Invoices[invoiceId]);

    public Task<StripeChargeState> GetChargeAsync(string chargeId, CancellationToken ct) =>
        ReadAsync(nameof(GetChargeAsync), chargeId, () => Charges[chargeId]);

    public Task<StripePaymentMethodState?> GetPaymentMethodAsync(string paymentMethodId, CancellationToken ct) =>
        ReadAsync(nameof(GetPaymentMethodAsync), paymentMethodId, () => PaymentMethods.TryGetValue(paymentMethodId, out var method) ? method : null);

    public Task SetCustomerDefaultPaymentMethodAsync(string customerId, string paymentMethodId, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(SetCustomerDefaultPaymentMethodAsync), idempotencyKey, customerId, () =>
            Customers[customerId] = Customers[customerId] with { DefaultPaymentMethodId = paymentMethodId });

    public Task DetachPaymentMethodAsync(string paymentMethodId, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(DetachPaymentMethodAsync), idempotencyKey, paymentMethodId, () =>
            PaymentMethods.TryGetValue(paymentMethodId, out var method) ? PaymentMethods[paymentMethodId] = method with { CustomerId = null } : null!);

    public Task<string> CreatePortalSessionAsync(string customerId, string returnUrl, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(CreatePortalSessionAsync), idempotencyKey, customerId, () => "https://billing.stripe.test/p/session/" + NewId("bps"));

    public Task<string> CreateSetupIntentAsync(string customerId, string idempotencyKey, CancellationToken ct) =>
        WriteAsync(nameof(CreateSetupIntentAsync), idempotencyKey, customerId, () =>
        {
            var id = NewId("seti");
            SetupIntents[id] = new StripeSetupIntentState(id, customerId, "requires_payment_method", null,
                new Dictionary<string, string> { ["purpose"] = StripeSetupIntentState.AccountCard });
            return id + "_secret_test";
        });

    public Task<StripeSetupIntentState> GetSetupIntentAsync(string setupIntentId, CancellationToken ct) =>
        ReadAsync(nameof(GetSetupIntentAsync), setupIntentId, () => SetupIntents[setupIntentId]);

    public Task<IReadOnlyList<StripeEventEnvelope>> ListEventsAsync(DateTimeOffset since, IReadOnlyCollection<string> types, CancellationToken ct) =>
        ReadAsync(nameof(ListEventsAsync), null, () => (IReadOnlyList<StripeEventEnvelope>)Events.Where(e => e.Created >= since && types.Contains(e.Type)).ToList());

    /// <summary>
    /// A new id with the prefix of Stripe (<c>price_3f9a1c2b7</c>, <c>sub_3f9a1c2b12</c>): unique also across the fakes of other
    /// tests, since the test database is shared and some ids of Stripe have unique indexes (sessions, subscriptions).
    /// </summary>
    public string NewId(string prefix) => prefix + "_" + run + Interlocked.Increment(ref sequence).ToString(CultureInfo.InvariantCulture);

    private Task<T> WriteAsync<T>(string method, string idempotencyKey, string? argument, Func<T> create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        Calls.Enqueue((method, idempotencyKey, argument));
        ThrowIfAsked();
        if (byKey.TryGetValue(method + "|" + idempotencyKey, out var existing))
        {
            return Task.FromResult((T)existing);
        }

        if (FailWritesAfter is { } limit && writes >= limit)
        {
            throw new StripeGatewayException("stripe.api_error", 500, true);
        }

        writes++;
        var created = create();
        byKey[method + "|" + idempotencyKey] = created!;
        return Task.FromResult(created);
    }

    private Task<T> ReadAsync<T>(string method, string? argument, Func<T> read)
    {
        Calls.Enqueue((method, null, argument));
        ThrowIfAsked();
        try
        {
            return Task.FromResult(read());
        }
        catch (KeyNotFoundException)
        {
            throw new StripeGatewayException("stripe.resource_missing", 404, false);
        }
    }

    private void ThrowIfAsked()
    {
        if (FailNext is { } error)
        {
            FailNext = null;
            throw error;
        }
    }
}
