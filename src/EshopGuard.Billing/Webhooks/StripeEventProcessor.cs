using System.Text.Json.Nodes;
using EshopGuard.Billing.Jobs;
using EshopGuard.Billing.Stripe;
using EshopGuard.Billing.Subscriptions;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Notifications;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Runs;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EshopGuard.Billing.Webhooks;

/// <summary>The outcome of an event: processed, ignored, or an error of Stripe (transient: the job tries again).</summary>
public sealed record StripeEventOutcome(string Status, Guid? TenantId, string? ErrorCode = null, bool Transient = false)
{
    public static StripeEventOutcome Ignored(Guid? tenantId = null) => new("ignored", tenantId);

    public static StripeEventOutcome Processed(Guid? tenantId) => new("processed", tenantId);
}

/// <summary>
/// The processing of a stored event of Stripe (design „Zpracování“, task 6.2): by its type, always over the current object read
/// from Stripe (never the body of the event, whose order Stripe does not guarantee). The event row ends <c>processed</c>,
/// <c>ignored</c> or <c>failed</c>; an event already processed is not processed again. Handlers:
/// <list type="bullet">
/// <item><c>checkout.session.completed</c> / <c>expired</c>: the order paid (only from <c>created</c>/<c>checkout_open</c>) with its
/// subscription, the card of the account and the run released (change 8), or expired;</item>
/// <item><c>invoice.paid</c>: the payment and, when something was paid, <c>billing.issue_invoice</c> (one per invoice of Stripe);
/// <c>invoice.payment_failed</c> / <c>payment_action_required</c>: the state of the subscription and a notification, no invoice;</item>
/// <item><c>customer.subscription.*</c>: the subscription; <c>deleted</c> stops the monitoring of the e-shop, its data stay;</item>
/// <item><c>customer.updated</c>, <c>payment_method.*</c>, <c>setup_intent.succeeded</c>: the card of the account; <c>customer.tax_id.*</c>: the verification of the VAT id;</item>
/// <item><c>charge.refunded</c>: the refunded amount and a credit note per refund; anything else is ignored.</item>
/// </list>
/// </summary>
public sealed class StripeEventProcessor(
    EshopGuardDataSource dataSource,
    IStripeGateway stripe,
    SubscriptionSync subscriptions,
    AccountCardService cards,
    NotificationDispatcher notifications,
    IJobQueue queue,
    IServiceProvider services,
    ITenantContext tenantContext,
    TimeProvider time,
    ILogger<StripeEventProcessor> logger)
{
    public async Task<StripeEventOutcome> ProcessAsync(string eventId, CancellationToken ct)
    {
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        (string Type, string Status, string? ObjectId) row;
        await using (var read = await connection.BeginTransactionAsync(ct).ConfigureAwait(false))
        {
            var rows = await BillingSql.ListAsync(read, "SELECT type, status, object_id FROM billing.stripe_events WHERE id = $1",
                r => (r.GetString(0), r.GetString(1), r.Get<string>(2)), ct, eventId).ConfigureAwait(false);
            await read.CommitAsync(ct).ConfigureAwait(false);
            if (rows.Count == 0)
            {
                return StripeEventOutcome.Ignored();
            }

            row = rows[0];
        }

        if (row.Status is "processed" or "ignored")
        {
            return new StripeEventOutcome(row.Status, null);
        }

        StripeEventOutcome outcome;
        try
        {
            outcome = row.ObjectId is not { } objectId ? StripeEventOutcome.Ignored() : row.Type switch
            {
                "checkout.session.completed" => await CheckoutCompletedAsync(objectId, ct).ConfigureAwait(false),
                "checkout.session.expired" => await CheckoutExpiredAsync(objectId, ct).ConfigureAwait(false),
                "invoice.paid" => await InvoicePaidAsync(objectId, ct).ConfigureAwait(false),
                "invoice.payment_failed" or "invoice.payment_action_required" => await InvoiceFailedAsync(objectId, ct).ConfigureAwait(false),
                "customer.subscription.created" or "customer.subscription.updated" or "customer.subscription.deleted" =>
                    await SubscriptionChangedAsync(objectId, ct).ConfigureAwait(false),
                "customer.updated" => await CardChangedAsync(objectId, ct).ConfigureAwait(false),
                "payment_method.attached" or "payment_method.detached" => await PaymentMethodChangedAsync(objectId, ct).ConfigureAwait(false),
                "setup_intent.succeeded" => await SetupIntentSucceededAsync(objectId, ct).ConfigureAwait(false),
                "customer.tax_id.created" or "customer.tax_id.updated" => await TaxIdChangedAsync(eventId, ct).ConfigureAwait(false),
                "charge.refunded" => await ChargeRefundedAsync(objectId, ct).ConfigureAwait(false),
                _ => StripeEventOutcome.Ignored(),
            };
        }
        catch (StripeGatewayException e)
        {
            outcome = new StripeEventOutcome("failed", null, e.Code, e.Transient);
        }

        await using (var write = await connection.BeginTransactionAsync(ct).ConfigureAwait(false))
        {
            await BillingSql.ExecuteAsync(write,
                """
                UPDATE billing.stripe_events SET status = $2, tenant_id = coalesce($3, tenant_id), processed_at = CASE WHEN $2 = 'failed' THEN processed_at ELSE $4 END,
                    attempts = attempts + 1, error = $5, updated_at = $4
                WHERE id = $1
                """, ct, eventId, outcome.Status, outcome.TenantId, time.GetUtcNow(), outcome.ErrorCode).ConfigureAwait(false);
            await write.CommitAsync(ct).ConfigureAwait(false);
        }

        logger.LogInformation("stripe.event_processed {EventId} {Type} {Status} {TenantId} {Code}", eventId, row.Type, outcome.Status, outcome.TenantId, outcome.ErrorCode);
        return outcome;
    }

    private async Task<StripeEventOutcome> CheckoutCompletedAsync(string sessionId, CancellationToken ct)
    {
        var session = await stripe.GetCheckoutSessionAsync(sessionId, ct).ConfigureAwait(false);
        if (SubscriptionSync.Meta(session.Metadata, "order_id") is not { } orderId || await TenantAsync(session.Metadata, session.CustomerId, ct).ConfigureAwait(false) is not { } tenantId)
        {
            return StripeEventOutcome.Ignored();
        }

        if (session.PaymentStatus is not ("paid" or "no_payment_required"))
        {
            return StripeEventOutcome.Ignored(tenantId);
        }

        var subscription = session.SubscriptionId is { } subscriptionId ? await stripe.GetSubscriptionAsync(subscriptionId, ct).ConfigureAwait(false) : null;
        var paid = await PayOrderAsync(tenantId, orderId, session.Id, subscription, ct).ConfigureAwait(false);
        if (session.CustomerId is { } customer)
        {
            await cards.ApplyDefaultAsync(tenantId, customer, subscription?.DefaultPaymentMethodId, ct).ConfigureAwait(false);
        }

        if (paid)
        {
            await ReleaseRunAsync(tenantId, orderId, ct).ConfigureAwait(false);
        }

        return StripeEventOutcome.Processed(tenantId);
    }

    /// <summary>
    /// The order becomes <c>paid</c> (only from <c>created</c>/<c>checkout_open</c>, so twice is once), the subscription of its
    /// e-shop is stored, the e-shop starts the analysis and the currency of the tenant is fixed. True when the order is paid.
    /// </summary>
    public async Task<bool> PayOrderAsync(Guid tenantId, Guid orderId, string? sessionId, StripeSubscriptionState? subscription, CancellationToken ct) =>
        await PayAsync(tenantId, orderId, sessionId, subscription, savedCard: false, ct).ConfigureAwait(false) is not null;

    /// <summary>
    /// An order paid with the saved card (task 7.3): its subscription, created by the API, is running in Stripe (<c>trialing</c> or
    /// <c>active</c>, so the first invoice is paid). Only the subscription the order waits for pays it; the run is released.
    /// From the webhooks of the subscription and its invoice and from the job <c>billing.expire_order</c>. True when the order is paid.
    /// </summary>
    public async Task<bool> SettleSavedCardOrderAsync(Guid tenantId, StripeSubscriptionState subscription, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        if (subscription.Status is not ("trialing" or "active") || SubscriptionSync.OrderOf(subscription) is not { } orderId)
        {
            return false;
        }

        if (await PayAsync(tenantId, orderId, null, subscription, savedCard: true, ct).ConfigureAwait(false) != "paid")
        {
            return false;
        }

        await ReleaseRunAsync(tenantId, orderId, ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// The status of the order after the payment, or null when there is no such order (or, with <paramref name="savedCard"/>, the
    /// order does not wait for this subscription).
    /// </summary>
    private async Task<string?> PayAsync(Guid tenantId, Guid orderId, string? sessionId, StripeSubscriptionState? subscription, bool savedCard, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, null, ct).ConfigureAwait(false);
        var order = (await BillingSql.ListAsync(transaction, "SELECT shop_id, status, currency, stripe_subscription_id FROM billing.orders WHERE id = $1 FOR UPDATE",
            r => (Shop: r.GetGuid(0), Status: r.GetString(1), Currency: r.GetString(2).Trim(), Subscription: r.Get<string>(3)), ct, orderId).ConfigureAwait(false)).FirstOrDefault();
        if (order == default || (savedCard && (subscription is null || order.Subscription != subscription.Id)))
        {
            return null;
        }

        var status = order.Status;
        if (order.Status is "created" or "checkout_open")
        {
            await BillingSql.ExecuteAsync(transaction,
                """
                UPDATE billing.orders SET status = 'paid', paid_at = $2, stripe_checkout_session_id = coalesce($3, stripe_checkout_session_id),
                    stripe_subscription_id = coalesce($4, stripe_subscription_id), updated_at = $2
                WHERE id = $1
                """, ct, orderId, now, sessionId, subscription?.Id).ConfigureAwait(false);
            await BillingSql.ExecuteAsync(transaction, "UPDATE shop.shops SET status = 'analyzing', updated_at = $2 WHERE id = $1 AND status = 'awaiting_payment'", ct, order.Shop, now)
                .ConfigureAwait(false);
            await BillingSql.AuditAsync(transaction, tenantId, null, BillingSql.System, "order.paid", "order", orderId.ToString("D"),
                new JsonObject { ["shop_id"] = order.Shop.ToString("D"), ["method"] = savedCard ? "saved_card" : "checkout" }, now, ct).ConfigureAwait(false);
            status = "paid";
        }

        if (subscription is not null)
        {
            await StoreSubscriptionAsync(transaction, tenantId, subscription, order.Shop, orderId, ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);

        // The currency is fixed by the first payment (change 9): column privilege of the worker, no RLS on tenants.
        await using (var currency = await connection.BeginTransactionAsync(ct).ConfigureAwait(false))
        {
            await BillingSql.ExecuteAsync(currency, "UPDATE iam.tenants SET currency = $2 WHERE id = $1 AND currency IS NULL", ct, tenantId, order.Currency).ConfigureAwait(false);
            await currency.CommitAsync(ct).ConfigureAwait(false);
        }

        return status;
    }

    private async Task<StripeEventOutcome> CheckoutExpiredAsync(string sessionId, CancellationToken ct)
    {
        var session = await stripe.GetCheckoutSessionAsync(sessionId, ct).ConfigureAwait(false);
        if (SubscriptionSync.Meta(session.Metadata, "order_id") is not { } orderId || await TenantAsync(session.Metadata, session.CustomerId, ct).ConfigureAwait(false) is not { } tenantId)
        {
            return StripeEventOutcome.Ignored();
        }

        var now = time.GetUtcNow();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, null, ct).ConfigureAwait(false);
        var expired = await BillingSql.ExecuteAsync(transaction,
            "UPDATE billing.orders SET status = 'expired', updated_at = $3 WHERE id = $1 AND stripe_checkout_session_id = $2 AND status IN ('created', 'checkout_open')",
            ct, orderId, session.Id, now).ConfigureAwait(false);
        if (expired == 1)
        {
            await BillingSql.AuditAsync(transaction, tenantId, null, BillingSql.System, "order.expired", "order", orderId.ToString("D"), null, now, ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return StripeEventOutcome.Processed(tenantId);
    }

    private async Task<StripeEventOutcome> InvoicePaidAsync(string invoiceId, CancellationToken ct)
    {
        var invoice = await stripe.GetInvoiceAsync(invoiceId, ct).ConfigureAwait(false);
        if (await TenantOfCustomerAsync(invoice.CustomerId, ct).ConfigureAwait(false) is not { } tenantId)
        {
            return StripeEventOutcome.Ignored();
        }

        var subscription = invoice.SubscriptionId is { } subscriptionId ? await stripe.GetSubscriptionAsync(subscriptionId, ct).ConfigureAwait(false) : null;
        var now = time.GetUtcNow();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, null, ct).ConfigureAwait(false);
        var synced = subscription is null ? null : await StoreSubscriptionAsync(transaction, tenantId, subscription, null, null, ct).ConfigureAwait(false);
        var shopId = synced?.ShopId ?? await BillingSql.ScalarAsync<Guid?>(transaction,
            "SELECT shop_id FROM billing.subscriptions WHERE stripe_subscription_id = $1", ct, invoice.SubscriptionId).ConfigureAwait(false);
        var orderId = invoice.BillingReason == "subscription_create"
            ? await BillingSql.ScalarAsync<Guid?>(transaction, "SELECT id FROM billing.orders WHERE stripe_subscription_id = $1", ct, invoice.SubscriptionId).ConfigureAwait(false)
            : null;
        var card = (await BillingSql.ListAsync(transaction, "SELECT brand, last4 FROM billing.payment_methods WHERE is_default AND detached_at IS NULL",
            r => (r.Get<string>(0), r.Get<string>(1)), ct).ConfigureAwait(false)).FirstOrDefault();
        await BillingSql.ExecuteAsync(transaction,
            """
            INSERT INTO billing.payments (tenant_id, order_id, subscription_id, stripe_invoice_id, stripe_payment_intent_id, amount_gross, currency, status, paid_at,
                card_brand, card_last4, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, 'succeeded', $8, $9, $10, $11, $11)
            ON CONFLICT (stripe_invoice_id) DO NOTHING
            """, ct, tenantId, orderId, synced?.Id, invoice.Id, invoice.PaymentIntentId, Money.FromMinor(invoice.AmountPaid), invoice.Currency.ToUpperInvariant(),
            invoice.PaidAt ?? now, card.Item1, card.Item2, now).ConfigureAwait(false);
        if (invoice.AmountPaid > 0)
        {
            await queue.EnqueueAsync(BillingJobs.IssueInvoice(tenantId, shopId, invoice.Id), transaction, ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        if (subscription is not null)
        {
            await SettleSavedCardOrderAsync(tenantId, subscription, ct).ConfigureAwait(false);
        }

        return StripeEventOutcome.Processed(tenantId);
    }

    private async Task<StripeEventOutcome> InvoiceFailedAsync(string invoiceId, CancellationToken ct)
    {
        var invoice = await stripe.GetInvoiceAsync(invoiceId, ct).ConfigureAwait(false);
        if (await TenantOfCustomerAsync(invoice.CustomerId, ct).ConfigureAwait(false) is not { } tenantId || invoice.SubscriptionId is not { } subscriptionId)
        {
            return StripeEventOutcome.Ignored();
        }

        var subscription = await stripe.GetSubscriptionAsync(subscriptionId, ct).ConfigureAwait(false);
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, null, ct).ConfigureAwait(false);
        var synced = await StoreSubscriptionAsync(transaction, tenantId, subscription, null, null, ct).ConfigureAwait(false);
        if (synced is not null)
        {
            await notifications.NotifyAsync(transaction, new NotificationRequest(tenantId, synced.ShopId, NotificationKinds.PaymentFailed,
                new JsonObject
                {
                    ["amount"] = Money.FromMinor(invoice.Total).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    ["currency"] = invoice.Currency.ToUpperInvariant(),
                    ["invoice_id"] = invoice.Id,
                },
                NotificationRoutes.Billing, new JsonObject()), ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return StripeEventOutcome.Processed(tenantId);
    }

    private async Task<StripeEventOutcome> SubscriptionChangedAsync(string subscriptionId, CancellationToken ct)
    {
        var subscription = await stripe.GetSubscriptionAsync(subscriptionId, ct).ConfigureAwait(false);
        if (await TenantAsync(subscription.Metadata, subscription.CustomerId, ct).ConfigureAwait(false) is not { } tenantId)
        {
            return StripeEventOutcome.Ignored();
        }

        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, null, ct).ConfigureAwait(false);
        await StoreSubscriptionAsync(transaction, tenantId, subscription, null, null, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        await SettleSavedCardOrderAsync(tenantId, subscription, ct).ConfigureAwait(false);
        return StripeEventOutcome.Processed(tenantId);
    }

    /// <summary>
    /// The subscription as Stripe has it now, and what follows from its new state: a second running subscription of an e-shop is an
    /// alert of operations; the end stops the monitoring of the e-shop (paused after a failed payment, else canceled), its data stay.
    /// A subscription that never started (<c>incomplete</c>, e.g. 3-D Secure not confirmed) ends without touching the e-shop; one
    /// that starts again (task 7.5) makes the stopped monitoring of the e-shop active.
    /// </summary>
    private async Task<SyncedSubscription?> StoreSubscriptionAsync(
        NpgsqlTransaction transaction, Guid tenantId, StripeSubscriptionState subscription, Guid? shopId, Guid? orderId, CancellationToken ct)
    {
        SyncedSubscription? synced;
        await using (var savepoint = new SavepointScope(transaction, "subscription"))
        {
            await savepoint.StartAsync(ct).ConfigureAwait(false);
            try
            {
                synced = await subscriptions.UpsertAsync(transaction, tenantId, subscription, shopId, orderId, ct).ConfigureAwait(false);
                await savepoint.ReleaseAsync(ct).ConfigureAwait(false);
            }
            catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                await savepoint.RollbackAsync(ct).ConfigureAwait(false);
                await BillingAlerts.RaiseAsync(transaction, tenantId, "billing.alert.second_running_subscription", "subscription", subscription.Id, time.GetUtcNow(), logger, ct)
                    .ConfigureAwait(false);
                return null;
            }
        }

        if (synced is null)
        {
            return null;
        }

        var now = time.GetUtcNow();
        if (synced.Status is "trialing" or "active" && synced.PreviousStatus is not ("trialing" or "active" or "past_due"))
        {
            var restarted = await BillingSql.ExecuteAsync(transaction,
                "UPDATE shop.shops SET status = 'active', updated_at = $2 WHERE id = $1 AND status IN ('paused', 'canceled')", ct, synced.ShopId, now).ConfigureAwait(false);
            if (restarted == 1)
            {
                await BillingSql.AuditAsync(transaction, tenantId, null, BillingSql.System, "subscription.restarted", "subscription", synced.Id.ToString("D"),
                    new JsonObject { ["shop_id"] = synced.ShopId.ToString("D") }, now, ct).ConfigureAwait(false);
            }
        }

        if (synced.Status == "canceled" && synced.PreviousStatus == "incomplete")
        {
            await BillingSql.ExecuteAsync(transaction, "UPDATE billing.subscriptions SET pause_reason = 'not_started' WHERE id = $1", ct, synced.Id).ConfigureAwait(false);
        }
        else if (synced.Status == "canceled" && synced.PreviousStatus is not null and not "canceled")
        {
            var paymentFailed = synced.PreviousStatus is "past_due";
            await BillingSql.ExecuteAsync(transaction, "UPDATE billing.subscriptions SET pause_reason = $2 WHERE id = $1", ct, synced.Id,
                paymentFailed ? "payment_failed" : "canceled").ConfigureAwait(false);
            await BillingSql.ExecuteAsync(transaction,
                """
                UPDATE shop.shops SET status = CASE WHEN $2 AND status = 'active' THEN 'paused' ELSE 'canceled' END, updated_at = $3
                WHERE id = $1 AND status NOT IN ('canceled', 'paused')
                """, ct, synced.ShopId, paymentFailed, now).ConfigureAwait(false);
            await BillingSql.AuditAsync(transaction, tenantId, null, BillingSql.System, "subscription.ended", "subscription", synced.Id.ToString("D"),
                new JsonObject { ["payment_failed"] = paymentFailed }, now, ct).ConfigureAwait(false);
            await notifications.NotifyAsync(transaction, new NotificationRequest(tenantId, synced.ShopId, NotificationKinds.SubscriptionEnded,
                new JsonObject { ["date"] = (subscription.EndedAt ?? now).UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), ["paymentFailed"] = paymentFailed },
                NotificationRoutes.Billing, new JsonObject()), ct).ConfigureAwait(false);
        }

        return synced;
    }

    private async Task<StripeEventOutcome> CardChangedAsync(string customerId, CancellationToken ct)
    {
        if (await TenantOfCustomerAsync(customerId, ct).ConfigureAwait(false) is not { } tenantId)
        {
            return StripeEventOutcome.Ignored();
        }

        await cards.ApplyDefaultAsync(tenantId, customerId, null, ct).ConfigureAwait(false);
        return StripeEventOutcome.Processed(tenantId);
    }

    private async Task<StripeEventOutcome> PaymentMethodChangedAsync(string paymentMethodId, CancellationToken ct)
    {
        var method = await stripe.GetPaymentMethodAsync(paymentMethodId, ct).ConfigureAwait(false);
        var customer = method?.CustomerId;
        if (customer is null)
        {
            // Detached: the customer is no longer on the card; the default card of the tenant that had it is applied again.
            await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
            await using var read = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            var tenants = await BillingSql.ListAsync(read, "SELECT id, stripe_customer_id FROM iam.tenants WHERE stripe_customer_id IS NOT NULL AND id IN (SELECT tenant_id FROM billing.payment_methods WHERE stripe_payment_method_id = $1)",
                r => (r.GetGuid(0), r.GetString(1)), ct, paymentMethodId).ConfigureAwait(false);
            await read.CommitAsync(ct).ConfigureAwait(false);
            return tenants.Count == 0 ? StripeEventOutcome.Ignored() : await CardChangedAsync(tenants[0].Item2, ct).ConfigureAwait(false);
        }

        return await CardChangedAsync(customer, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The fallback of the portal (task 7.4): the card saved in the Payment Element becomes the default card of the customer and so
    /// the card of every running subscription. Only a SetupIntent of the application (<c>purpose = account_card</c>).
    /// </summary>
    private async Task<StripeEventOutcome> SetupIntentSucceededAsync(string setupIntentId, CancellationToken ct)
    {
        var intent = await stripe.GetSetupIntentAsync(setupIntentId, ct).ConfigureAwait(false);
        if (intent is not { Status: "succeeded", CustomerId: { } customerId, PaymentMethodId: { } paymentMethodId }
            || !intent.Metadata.TryGetValue("purpose", out var purpose) || purpose != StripeSetupIntentState.AccountCard
            || await TenantOfCustomerAsync(customerId, ct).ConfigureAwait(false) is not { } tenantId)
        {
            return StripeEventOutcome.Ignored();
        }

        await stripe.SetCustomerDefaultPaymentMethodAsync(customerId, paymentMethodId, $"default-card:{customerId}:{paymentMethodId}", ct).ConfigureAwait(false);
        await cards.ApplyDefaultAsync(tenantId, customerId, null, ct).ConfigureAwait(false);
        return StripeEventOutcome.Processed(tenantId);
    }

    private async Task<StripeEventOutcome> TaxIdChangedAsync(string eventId, CancellationToken ct)
    {
        // The object of the event is the tax id; its customer is read from the stored event (only its id).
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        string? customerId;
        await using (var read = await connection.BeginTransactionAsync(ct).ConfigureAwait(false))
        {
            customerId = await BillingSql.ScalarAsync<string>(read, "SELECT payload->'data'->'object'->>'customer' FROM billing.stripe_events WHERE id = $1", ct, eventId)
                .ConfigureAwait(false);
            await read.CommitAsync(ct).ConfigureAwait(false);
        }

        if (customerId is null || await TenantOfCustomerAsync(customerId, ct).ConfigureAwait(false) is not { } tenantId)
        {
            return StripeEventOutcome.Ignored();
        }

        var customer = await stripe.GetCustomerAsync(customerId, ct).ConfigureAwait(false);
        await using var write = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var icDph = await BillingSql.ScalarAsync<string>(write, "SELECT ic_dph FROM iam.tenants WHERE id = $1", ct, tenantId).ConfigureAwait(false);
        var normalized = icDph?.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        var taxId = customer.TaxIds.FirstOrDefault(t => t.Value.Replace(" ", string.Empty, StringComparison.Ordinal).Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (taxId is not null)
        {
            var status = taxId.VerificationStatus switch
            {
                "verified" => "verified",
                "unverified" => "unverified",
                _ => "pending",
            };
            await BillingSql.ExecuteAsync(write,
                "UPDATE iam.tenants SET tax_id_status = $2, tax_id_verified_at = CASE WHEN $2 = 'verified' THEN $3 ELSE NULL END WHERE id = $1",
                ct, tenantId, status, time.GetUtcNow()).ConfigureAwait(false);
        }

        await write.CommitAsync(ct).ConfigureAwait(false);
        return StripeEventOutcome.Processed(tenantId);
    }

    private async Task<StripeEventOutcome> ChargeRefundedAsync(string chargeId, CancellationToken ct)
    {
        var charge = await stripe.GetChargeAsync(chargeId, ct).ConfigureAwait(false);
        if (charge.CustomerId is null || await TenantOfCustomerAsync(charge.CustomerId, ct).ConfigureAwait(false) is not { } tenantId)
        {
            return StripeEventOutcome.Ignored();
        }

        var now = time.GetUtcNow();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, null, ct).ConfigureAwait(false);
        var payment = (await BillingSql.ListAsync(transaction,
            """
            SELECT p.id, s.shop_id, o.shop_id FROM billing.payments p
            LEFT JOIN billing.subscriptions s ON s.id = p.subscription_id LEFT JOIN billing.orders o ON o.id = p.order_id
            WHERE p.stripe_invoice_id = $1 OR p.stripe_charge_id = $2 OR p.stripe_payment_intent_id = $3 LIMIT 1
            """, r => (Id: r.GetGuid(0), Shop: r.Get<Guid?>(1) ?? r.Get<Guid?>(2)), ct, charge.InvoiceId, charge.Id, charge.PaymentIntentId).ConfigureAwait(false)).FirstOrDefault();
        if (payment == default)
        {
            await BillingAlerts.RaiseAsync(transaction, tenantId, "billing.alert.refund_without_payment", "charge", charge.Id, now, logger, ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return StripeEventOutcome.Processed(tenantId);
        }

        var full = charge.AmountRefunded >= charge.Amount;
        await BillingSql.ExecuteAsync(transaction,
            "UPDATE billing.payments SET refunded_amount = $2, status = $3, stripe_charge_id = $4, updated_at = $5 WHERE id = $1",
            ct, payment.Id, Money.FromMinor(charge.AmountRefunded), full ? "refunded" : "partially_refunded", charge.Id, now).ConfigureAwait(false);
        foreach (var refund in charge.Refunds.Where(r => r.Status == "succeeded"))
        {
            await queue.EnqueueAsync(BillingJobs.IssueCreditNote(tenantId, payment.Shop, refund.Id, charge.Id), transaction, ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return StripeEventOutcome.Processed(tenantId);
    }

    /// <summary>The run waiting for the payment of the order starts downloading (change 8; a second call changes nothing).</summary>
    private async Task ReleaseRunAsync(Guid tenantId, Guid orderId, CancellationToken ct)
    {
        if (tenantContext.TenantId is null)
        {
            tenantContext.Set(tenantId);
        }

        if (services.GetService(typeof(IRunService)) is not IRunService runs)
        {
            logger.LogWarning("order.run_service_missing {OrderId} {TenantId}", orderId, tenantId);
            return;
        }

        var result = await runs.MarkOrderPaidAsync(orderId, ct).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            logger.LogWarning("order.run_not_released {OrderId} {TenantId} {Code}", orderId, tenantId, result.ErrorCode);
        }
    }

    /// <summary>
    /// The tenant of the customer of the object, which the metadata must not contradict (fail-closed: a customer no tenant has, or
    /// metadata of another tenant, is ignored and logged); only an object without a customer is assigned by its metadata.
    /// </summary>
    private async Task<Guid?> TenantAsync(IReadOnlyDictionary<string, string> metadata, string? customerId, CancellationToken ct)
    {
        var byMetadata = SubscriptionSync.Meta(metadata, "tenant_id");
        if (customerId is null)
        {
            return byMetadata;
        }

        if (await TenantOfCustomerAsync(customerId, ct).ConfigureAwait(false) is not { } byCustomer)
        {
            logger.LogWarning("stripe.customer_unknown {Metadata}", byMetadata);
            return null;
        }

        if (byMetadata is { } meta && meta != byCustomer)
        {
            logger.LogError("stripe.tenant_mismatch {Metadata} {Customer}", meta, byCustomer);
            return null;
        }

        return byCustomer;
    }

    private async Task<Guid?> TenantOfCustomerAsync(string customerId, CancellationToken ct)
    {
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var read = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var id = await BillingSql.ScalarAsync<Guid?>(read, "SELECT id FROM iam.tenants WHERE stripe_customer_id = $1", ct, customerId).ConfigureAwait(false);
        await read.CommitAsync(ct).ConfigureAwait(false);
        return id;
    }
}

/// <summary>A savepoint in a transaction (a failed statement does not abort the rest of it).</summary>
internal sealed class SavepointScope(NpgsqlTransaction transaction, string name) : IAsyncDisposable
{
    private bool started;
    private bool done;

    public async Task StartAsync(CancellationToken ct)
    {
        await transaction.SaveAsync(name, ct).ConfigureAwait(false);
        started = true;
    }

    public async Task ReleaseAsync(CancellationToken ct)
    {
        if (started && !done)
        {
            await transaction.ReleaseAsync(name, ct).ConfigureAwait(false);
            done = true;
        }
    }

    public async Task RollbackAsync(CancellationToken ct)
    {
        if (started && !done)
        {
            await transaction.RollbackAsync(name, ct).ConfigureAwait(false);
            done = true;
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Alerts of operations of billing: the code in the log (level error, the monitor of change 17 reads it) and in the audit of the
/// tenant (<c>billing.alert.*</c>), ids only.
/// </summary>
public static class BillingAlerts
{
    public static async Task RaiseAsync(
        NpgsqlTransaction transaction, Guid tenantId, string code, string entityType, string entityId, DateTimeOffset at, ILogger logger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.LogError("billing.alert {Code} {TenantId} {EntityType} {EntityId}", code, tenantId, entityType, entityId);
        await BillingSql.AuditAsync(transaction, tenantId, null, BillingSql.System, code, entityType, entityId, null, at, ct).ConfigureAwait(false);
    }
}
