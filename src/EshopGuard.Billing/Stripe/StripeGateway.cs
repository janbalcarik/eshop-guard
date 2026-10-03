using System.Net;
using Microsoft.Extensions.Options;
using S = global::Stripe;

namespace EshopGuard.Billing.Stripe;

/// <summary>
/// <see cref="IStripeGateway"/> over Stripe.net in its pinned API version (<c>StripeConfiguration.ApiVersion</c>). Every write
/// carries its idempotency key; the client has no logging of bodies or headers (its own HTTP client, no
/// <c>IHttpClientFactory</c> logging, no telemetry). Errors become <see cref="StripeGatewayException"/> with the code of Stripe.
/// </summary>
internal sealed class StripeGateway : IStripeGateway, IDisposable
{
    private readonly HttpClient http;
    private readonly S.StripeClient client;

    public StripeGateway(IOptions<BillingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        http = new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
        client = new S.StripeClient(
            apiKey: options.Value.Stripe.SecretKey,
            httpClient: new S.SystemNetHttpClient(http, 2, null, false));
    }

    public void Dispose() => http.Dispose();

    public Task<string> CreateProductAsync(StripeProductRequest request, string idempotencyKey, CancellationToken ct) =>
        CallAsync(async () => (await client.V1.Products.CreateAsync(
            new S.ProductCreateOptions { Name = request.Name, Metadata = Map(request.Metadata) }, Key(idempotencyKey), ct).ConfigureAwait(false)).Id);

    public Task<string> CreatePriceAsync(StripePriceRequest request, string idempotencyKey, CancellationToken ct) =>
        CallAsync(async () => (await client.V1.Prices.CreateAsync(
            new S.PriceCreateOptions
            {
                Product = request.ProductId,
                Currency = request.Currency.ToLowerInvariant(),
                UnitAmount = request.UnitAmount,
                Recurring = request.RecurringInterval is { } interval ? new S.PriceRecurringOptions { Interval = interval } : null,
                LookupKey = request.LookupKey,
                TaxBehavior = "exclusive",
                Metadata = Map(request.Metadata),
            }, Key(idempotencyKey), ct).ConfigureAwait(false)).Id);

    public Task TransferLookupKeyAsync(string priceId, string lookupKey, string idempotencyKey, CancellationToken ct) =>
        CallAsync(() => client.V1.Prices.UpdateAsync(priceId, new S.PriceUpdateOptions { LookupKey = lookupKey, TransferLookupKey = true }, Key(idempotencyKey), ct));

    public Task SetPriceActiveAsync(string priceId, bool active, string idempotencyKey, CancellationToken ct) =>
        CallAsync(() => client.V1.Prices.UpdateAsync(priceId, new S.PriceUpdateOptions { Active = active }, Key(idempotencyKey), ct));

    public Task<string> CreateCouponAsync(StripeCouponRequest request, string idempotencyKey, CancellationToken ct) =>
        CallAsync(async () => (await client.V1.Coupons.CreateAsync(
            new S.CouponCreateOptions
            {
                PercentOff = request.PercentOff,
                Duration = "forever",
                AppliesTo = new S.CouponAppliesToOptions { Products = [request.AppliesToProductId] },
                Name = request.Name,
                Metadata = Map(request.Metadata),
            }, Key(idempotencyKey), ct).ConfigureAwait(false)).Id);

    public Task<string> CreateCustomerAsync(StripeCustomerRequest request, string idempotencyKey, CancellationToken ct) =>
        CallAsync(async () => (await client.V1.Customers.CreateAsync(
            new S.CustomerCreateOptions
            {
                Name = request.Name,
                Email = request.Email,
                Address = Address(request.Address),
                PreferredLocales = [request.PreferredLocale],
                Metadata = Map(request.Metadata),
            }, Key(idempotencyKey), ct).ConfigureAwait(false)).Id);

    public Task UpdateCustomerAsync(string customerId, StripeCustomerRequest request, string idempotencyKey, CancellationToken ct) =>
        CallAsync(() => client.V1.Customers.UpdateAsync(customerId, new S.CustomerUpdateOptions
        {
            Name = request.Name,
            Email = request.Email,
            Address = Address(request.Address),
            PreferredLocales = [request.PreferredLocale],
            Metadata = Map(request.Metadata),
        }, Key(idempotencyKey), ct));

    public Task<StripeCustomerState> GetCustomerAsync(string customerId, CancellationToken ct) =>
        CallAsync(async () =>
        {
            var customer = await client.V1.Customers.GetAsync(customerId, new S.CustomerGetOptions { Expand = ["tax_ids"] }, null, ct).ConfigureAwait(false);
            return new StripeCustomerState(
                customer.Id,
                customer.InvoiceSettings?.DefaultPaymentMethodId,
                customer.TaxIds?.Data.Select(TaxId).ToList() ?? [],
                customer.Deleted == true,
                customer.Metadata ?? []);
        });

    public Task<StripeTaxIdState> CreateTaxIdAsync(string customerId, string type, string value, string idempotencyKey, CancellationToken ct) =>
        CallAsync(async () => TaxId(await client.V1.Customers.TaxIds.CreateAsync(
            customerId, new S.CustomerTaxIdCreateOptions { Type = type, Value = value }, Key(idempotencyKey), ct).ConfigureAwait(false)));

    public Task<StripeCheckoutSession> CreateCheckoutSessionAsync(StripeCheckoutRequest request, string idempotencyKey, CancellationToken ct) =>
        CallAsync(async () => Session(await client.V1.Checkout.Sessions.CreateAsync(
            new S.Checkout.SessionCreateOptions
            {
                Mode = "subscription",
                Customer = request.CustomerId,
                LineItems =
                [
                    new S.Checkout.SessionLineItemOptions { Price = request.AnalysisPriceId, Quantity = 1 },
                    new S.Checkout.SessionLineItemOptions { Price = request.MonitoringPriceId, Quantity = 1 },
                ],
                SubscriptionData = new S.Checkout.SessionSubscriptionDataOptions
                {
                    TrialEnd = request.TrialEnd.UtcDateTime,
                    Metadata = Map(request.Metadata),
                },
                Discounts = request.CouponId is { } coupon ? [new S.Checkout.SessionDiscountOptions { Coupon = coupon }] : null,
                AutomaticTax = new S.Checkout.SessionAutomaticTaxOptions { Enabled = true },
                CustomerUpdate = new S.Checkout.SessionCustomerUpdateOptions { Address = "auto", Name = "auto" },
                Locale = request.Locale,
                CustomText = new S.Checkout.SessionCustomTextOptions
                {
                    Submit = new S.Checkout.SessionCustomTextSubmitOptions { Message = request.SubmitMessage },
                },
                SuccessUrl = request.SuccessUrl,
                CancelUrl = request.CancelUrl,
                ExpiresAt = request.ExpiresAt.UtcDateTime,
                Metadata = Map(request.Metadata),
            }, Key(idempotencyKey), ct).ConfigureAwait(false)));

    public Task<StripeCheckoutSession> GetCheckoutSessionAsync(string sessionId, CancellationToken ct) =>
        CallAsync(async () => Session(await client.V1.Checkout.Sessions.GetAsync(sessionId, null, null, ct).ConfigureAwait(false)));

    public Task<StripeCheckoutSession> ExpireCheckoutSessionAsync(string sessionId, string idempotencyKey, CancellationToken ct) =>
        CallAsync(async () => Session(await client.V1.Checkout.Sessions.ExpireAsync(
            sessionId, new S.Checkout.SessionExpireOptions(), Key(idempotencyKey), ct).ConfigureAwait(false)));

    public Task<StripeSubscriptionState> CreateSubscriptionAsync(StripeSubscriptionRequest request, string idempotencyKey, CancellationToken ct) =>
        CallAsync(async () =>
        {
            var options = new S.SubscriptionCreateOptions
            {
                Customer = request.CustomerId,
                Items = [new S.SubscriptionItemOptions { Price = request.MonitoringPriceId, Quantity = 1 }],
                AddInvoiceItems = request.AnalysisPriceId is { } analysis ? [new S.SubscriptionAddInvoiceItemOptions { Price = analysis, Quantity = 1 }] : null,
                Discounts = request.CouponId is { } coupon ? [new S.SubscriptionDiscountOptions { Coupon = coupon }] : null,
                DefaultPaymentMethod = request.DefaultPaymentMethodId,
                PaymentBehavior = "default_incomplete",
                ProrationBehavior = "none",
                AutomaticTax = new S.SubscriptionAutomaticTaxOptions { Enabled = true },
                Metadata = Map(request.Metadata),
                Expand = ["latest_invoice.confirmation_secret"],
            };
            if (request.TrialEnd is { } trialEnd)
            {
                options.TrialEnd = trialEnd.UtcDateTime;
            }

            return Subscription(await client.V1.Subscriptions.CreateAsync(options, Key(idempotencyKey), ct).ConfigureAwait(false));
        });

    public Task<StripeSubscriptionState> GetSubscriptionAsync(string subscriptionId, CancellationToken ct) =>
        CallAsync(async () => Subscription(await client.V1.Subscriptions.GetAsync(
            subscriptionId, new S.SubscriptionGetOptions { Expand = ["latest_invoice.confirmation_secret"] }, null, ct).ConfigureAwait(false)));

    public Task<StripeSubscriptionState> SetCancelAtPeriodEndAsync(string subscriptionId, bool cancel, string idempotencyKey, CancellationToken ct) =>
        CallAsync(async () => Subscription(await client.V1.Subscriptions.UpdateAsync(
            subscriptionId, new S.SubscriptionUpdateOptions { CancelAtPeriodEnd = cancel }, Key(idempotencyKey), ct).ConfigureAwait(false)));

    public Task<StripeSubscriptionState> CancelSubscriptionAsync(string subscriptionId, string idempotencyKey, CancellationToken ct) =>
        CallAsync(async () => Subscription(await client.V1.Subscriptions.CancelAsync(
            subscriptionId, new S.SubscriptionCancelOptions { Prorate = false, InvoiceNow = false }, Key(idempotencyKey), ct).ConfigureAwait(false)));

    public Task SetSubscriptionPaymentMethodAsync(string subscriptionId, string paymentMethodId, string idempotencyKey, CancellationToken ct) =>
        CallAsync(() => client.V1.Subscriptions.UpdateAsync(
            subscriptionId, new S.SubscriptionUpdateOptions { DefaultPaymentMethod = paymentMethodId }, Key(idempotencyKey), ct));

    public Task<StripeScheduleState> CreateScheduleFromSubscriptionAsync(string subscriptionId, string idempotencyKey, CancellationToken ct) =>
        CallAsync(async () => Schedule(await client.V1.SubscriptionSchedules.CreateAsync(
            new S.SubscriptionScheduleCreateOptions { FromSubscription = subscriptionId }, Key(idempotencyKey), ct).ConfigureAwait(false)));

    public Task<StripeScheduleState> UpdateScheduleAsync(string scheduleId, IReadOnlyList<StripeSchedulePhase> phases, string idempotencyKey, CancellationToken ct) =>
        CallAsync(async () => Schedule(await client.V1.SubscriptionSchedules.UpdateAsync(
            scheduleId,
            new S.SubscriptionScheduleUpdateOptions
            {
                EndBehavior = "release",
                ProrationBehavior = "none",
                Phases = phases.Select(Phase).ToList(),
            }, Key(idempotencyKey), ct).ConfigureAwait(false)));

    public Task ReleaseScheduleAsync(string scheduleId, string idempotencyKey, CancellationToken ct) =>
        CallAsync(() => client.V1.SubscriptionSchedules.ReleaseAsync(scheduleId, new S.SubscriptionScheduleReleaseOptions { PreserveCancelDate = true }, Key(idempotencyKey), ct));

    public Task<StripeScheduleState> GetScheduleAsync(string scheduleId, CancellationToken ct) =>
        CallAsync(async () => Schedule(await client.V1.SubscriptionSchedules.GetAsync(scheduleId, null, null, ct).ConfigureAwait(false)));

    public Task<StripeInvoiceState> GetInvoiceAsync(string invoiceId, CancellationToken ct) =>
        CallAsync(async () =>
        {
            var invoice = await client.V1.Invoices.GetAsync(invoiceId, new S.InvoiceGetOptions { Expand = ["payments"] }, null, ct).ConfigureAwait(false);
            var taxes = invoice.TotalTaxes ?? [];
            return new StripeInvoiceState(
                invoice.Id,
                invoice.CustomerId,
                invoice.Parent?.SubscriptionDetails?.SubscriptionId,
                invoice.Status,
                invoice.Currency,
                invoice.AmountPaid,
                invoice.Subtotal,
                invoice.Total,
                invoice.TotalExcludingTax ?? invoice.Total - taxes.Sum(t => t.Amount),
                taxes.Sum(t => t.Amount),
                taxes.Any(t => t.TaxabilityReason == "reverse_charge"),
                invoice.CustomerTaxExempt,
                invoice.BillingReason,
                Utc(invoice.Created),
                invoice.StatusTransitions?.PaidAt is { } paid ? Utc(paid) : null,
                invoice.Payments?.Data.FirstOrDefault(p => p.IsDefault)?.Payment?.PaymentIntentId ?? invoice.Payments?.Data.FirstOrDefault()?.Payment?.PaymentIntentId,
                invoice.Lines?.Data.Select(l => new StripeInvoiceLine(
                    l.Id,
                    l.Description,
                    l.Amount,
                    l.Quantity,
                    l.Pricing?.PriceDetails?.PriceId,
                    l.Period is { } period ? Utc(period.Start) : null,
                    l.Period is { } end ? Utc(end.End) : null,
                    l.DiscountAmounts?.Sum(d => d.Amount) ?? 0,
                    l.Taxes?.Sum(t => t.Amount) ?? 0)).ToList() ?? [],
                invoice.Metadata ?? []);
        });

    public Task<StripeChargeState> GetChargeAsync(string chargeId, CancellationToken ct) =>
        CallAsync(async () =>
        {
            var charge = await client.V1.Charges.GetAsync(chargeId, new S.ChargeGetOptions { Expand = ["refunds"] }, null, ct).ConfigureAwait(false);
            string? invoiceId = null;
            if (charge.PaymentIntentId is { } intent)
            {
                var payments = await client.V1.InvoicePayments.ListAsync(
                    new S.InvoicePaymentListOptions { Payment = new S.InvoicePaymentPaymentOptions { Type = "payment_intent", PaymentIntent = intent } }, null, ct)
                    .ConfigureAwait(false);
                invoiceId = payments.Data.FirstOrDefault()?.InvoiceId;
            }

            return new StripeChargeState(
                charge.Id, charge.CustomerId, charge.PaymentIntentId, invoiceId, charge.Currency, charge.Amount, charge.AmountRefunded,
                charge.Refunds?.Data.Select(r => new StripeRefundState(r.Id, r.Amount, r.Status, Utc(r.Created))).ToList() ?? []);
        });

    public Task<StripePaymentMethodState?> GetPaymentMethodAsync(string paymentMethodId, CancellationToken ct) =>
        CallAsync(async () =>
        {
            try
            {
                var method = await client.V1.PaymentMethods.GetAsync(paymentMethodId, null, null, ct).ConfigureAwait(false);
                return (StripePaymentMethodState?)new StripePaymentMethodState(
                    method.Id, method.CustomerId, method.Card?.Brand, method.Card?.Last4, (int?)method.Card?.ExpMonth, (int?)method.Card?.ExpYear);
            }
            catch (S.StripeException e) when (e.HttpStatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        });

    public Task SetCustomerDefaultPaymentMethodAsync(string customerId, string paymentMethodId, string idempotencyKey, CancellationToken ct) =>
        CallAsync(() => client.V1.Customers.UpdateAsync(customerId, new S.CustomerUpdateOptions
        {
            InvoiceSettings = new S.CustomerInvoiceSettingsOptions { DefaultPaymentMethod = paymentMethodId },
        }, Key(idempotencyKey), ct));

    public Task DetachPaymentMethodAsync(string paymentMethodId, string idempotencyKey, CancellationToken ct) =>
        CallAsync(() => client.V1.PaymentMethods.DetachAsync(paymentMethodId, null, Key(idempotencyKey), ct));

    public Task<string> CreatePortalSessionAsync(string customerId, string returnUrl, string idempotencyKey, CancellationToken ct) =>
        CallAsync(async () => (await client.V1.BillingPortal.Sessions.CreateAsync(
            new S.BillingPortal.SessionCreateOptions
            {
                Customer = customerId,
                ReturnUrl = returnUrl,
                Configuration = string.IsNullOrEmpty(portalConfiguration) ? null : portalConfiguration,
                FlowData = new S.BillingPortal.SessionFlowDataOptions { Type = "payment_method_update" },
            }, Key(idempotencyKey), ct).ConfigureAwait(false)).Url);

    public Task<string> CreateSetupIntentAsync(string customerId, string idempotencyKey, CancellationToken ct) =>
        CallAsync(async () => (await client.V1.SetupIntents.CreateAsync(
            new S.SetupIntentCreateOptions
            {
                Customer = customerId,
                Usage = "off_session",
                Metadata = new Dictionary<string, string> { ["purpose"] = StripeSetupIntentState.AccountCard },
            }, Key(idempotencyKey), ct)
            .ConfigureAwait(false)).ClientSecret);

    public Task<StripeSetupIntentState> GetSetupIntentAsync(string setupIntentId, CancellationToken ct) =>
        CallAsync(async () =>
        {
            var intent = await client.V1.SetupIntents.GetAsync(setupIntentId, null, null, ct).ConfigureAwait(false);
            return new StripeSetupIntentState(intent.Id, intent.CustomerId, intent.Status, intent.PaymentMethodId,
                intent.Metadata ?? new Dictionary<string, string>());
        });

    public Task<IReadOnlyList<StripeEventEnvelope>> ListEventsAsync(DateTimeOffset since, IReadOnlyCollection<string> types, CancellationToken ct) =>
        CallAsync(async () =>
        {
            var result = new List<StripeEventEnvelope>();
            var options = new S.EventListOptions
            {
                Created = new S.DateRangeOptions { GreaterThanOrEqual = since.UtcDateTime },
                Types = types.ToList(),
                Limit = 100,
            };
            await foreach (var e in client.V1.Events.ListAutoPagingAsync(options, null, ct).ConfigureAwait(false))
            {
                result.Add(StripeWebhookVerifier.Envelope(e));
            }

            return (IReadOnlyList<StripeEventEnvelope>)result;
        });

    private string? portalConfiguration;

    /// <summary>Set by the registration from <c>Billing:Stripe:PortalConfigurationId</c>.</summary>
    internal StripeGateway WithPortalConfiguration(string? id)
    {
        portalConfiguration = id;
        return this;
    }

    private static S.RequestOptions Key(string idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        return new S.RequestOptions { IdempotencyKey = idempotencyKey };
    }

    private static Dictionary<string, string> Map(IReadOnlyDictionary<string, string> metadata) => metadata.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    private static S.AddressOptions Address(StripeAddress address) => new()
    {
        Line1 = address.Line1,
        City = address.City,
        PostalCode = address.PostalCode,
        Country = address.Country,
    };

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static StripeTaxIdState TaxId(S.TaxId taxId) =>
        new(taxId.Id, taxId.Type, taxId.Value, taxId.Verification?.Status ?? "unavailable");

    private static StripeCheckoutSession Session(S.Checkout.Session session) => new(
        session.Id, session.Url, session.Status, session.PaymentStatus, session.CustomerId, session.SubscriptionId, Utc(session.ExpiresAt), session.Metadata ?? []);

    private static StripeSubscriptionState Subscription(S.Subscription subscription)
    {
        var item = subscription.Items?.Data.FirstOrDefault();
        return new StripeSubscriptionState(
            subscription.Id,
            subscription.CustomerId,
            subscription.Status,
            item?.Price?.Id,
            subscription.Discounts?.FirstOrDefault()?.Source?.CouponId,
            subscription.TrialEnd is { } trialEnd ? Utc(trialEnd) : null,
            item is null ? null : Utc(item.CurrentPeriodStart),
            item is null ? null : Utc(item.CurrentPeriodEnd),
            subscription.CancelAtPeriodEnd,
            subscription.CanceledAt is { } canceled ? Utc(canceled) : null,
            subscription.EndedAt is { } ended ? Utc(ended) : null,
            subscription.ScheduleId,
            subscription.DefaultPaymentMethodId,
            subscription.LatestInvoiceId,
            subscription.Status == "incomplete" ? subscription.LatestInvoice?.ConfirmationSecret?.ClientSecret : null,
            Utc(subscription.Created),
            subscription.Metadata ?? []);
    }

    private static S.SubscriptionSchedulePhaseOptions Phase(StripeSchedulePhase phase)
    {
        var options = new S.SubscriptionSchedulePhaseOptions
        {
            Items = [new S.SubscriptionSchedulePhaseItemOptions { Price = phase.PriceId, Quantity = 1 }],
            Discounts = phase.CouponId is { } coupon ? [new S.SubscriptionSchedulePhaseDiscountOptions { Coupon = coupon }] : [],
            ProrationBehavior = "none",
            StartDate = phase.StartDate.UtcDateTime,
        };
        if (phase.EndDate is { } end)
        {
            options.EndDate = end.UtcDateTime;
        }

        return options;
    }

    private static StripeScheduleState Schedule(S.SubscriptionSchedule schedule) => new(
        schedule.Id,
        schedule.Status,
        schedule.SubscriptionId,
        schedule.Phases?.Select(p => new StripeSchedulePhase(
            Utc(p.StartDate),
            p.EndDate == default ? null : Utc(p.EndDate),
            p.Items?.FirstOrDefault()?.PriceId ?? string.Empty,
            p.Discounts?.FirstOrDefault()?.CouponId)).ToList() ?? []);

    private static async Task<T> CallAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        catch (S.StripeException e)
        {
            var status = (int)e.HttpStatusCode;
            var transient = status is 0 or 409 or 429 or >= 500;
            throw new StripeGatewayException("stripe." + (e.StripeError?.Code ?? e.StripeError?.Type ?? "error"), status == 0 ? null : status, transient);
        }
        catch (HttpRequestException)
        {
            throw new StripeGatewayException("stripe.network", null, true);
        }
        catch (TaskCanceledException e) when (e.InnerException is TimeoutException)
        {
            throw new StripeGatewayException("stripe.timeout", null, true);
        }
    }

    private static Task CallAsync(Func<Task> call) => CallAsync(async () =>
    {
        await call().ConfigureAwait(false);
        return true;
    });
}
