using System.Text.Json.Nodes;
using EshopGuard.Billing.Jobs;
using EshopGuard.Billing.Stripe;
using EshopGuard.Billing.Webhooks;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Runs;
using EshopGuard.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using static EshopGuard.Billing.Tests.BillingTestHost;
using static EshopGuard.Billing.Tests.StripeScenario;

namespace EshopGuard.Billing.Tests;

/// <summary>
/// The processing of the events of Stripe by the worker (change 12, tasks 6.2–6.4; requirements „Příjem webhooků Stripe
/// s odstraněním duplicit“, „Platba úvodní analýzy a sledování přes Stripe Checkout“ and „Jedna platební karta na účet“): every
/// handler reads the current object from <see cref="FakeStripeGateway"/>, an event processed twice changes nothing twice.
/// </summary>
[Trait("Category", "Db")]
public sealed class StripeEventProcessorTests
{
    [Fact]
    public async Task CheckoutCompleted_PaysTheOrder_StoresTheSubscriptionAndTheCard_AndReleasesTheRunOnce()
    {
        await using var host = await HostAsync();
        var paying = await PayingAsync(host);

        var first = await StoreAsync(host, "checkout.session.completed", "checkout.session", paying.SessionId);
        var again = await StoreAsync(host, "checkout.session.completed", "checkout.session", paying.SessionId);
        var outcome = await ProcessAsync(host, first);
        var repeated = await ProcessAsync(host, first);
        await ProcessAsync(host, again);

        Assert.Equal(("processed", paying.TenantId), (outcome.Status, outcome.TenantId));
        Assert.Equal("processed", repeated.Status);
        Assert.Equal("paid", await AdminScalarAsync<string>("SELECT status FROM billing.orders WHERE id = $1", paying.OrderId));
        Assert.Equal(paying.SubscriptionId, await AdminScalarAsync<string>("SELECT stripe_subscription_id FROM billing.orders WHERE id = $1", paying.OrderId));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'order.paid' AND entity_id = $1", paying.OrderId.ToString("D")));
        var subscription = Assert.Single(await AdminRowsAsync(
            "SELECT status, tier_code, unit_price, interval, order_id, shop_ordinal, trial_end FROM billing.subscriptions WHERE shop_id = $1", paying.ShopId));
        Assert.Equal(new object?[] { "trialing", "t2000", 19m, "month", paying.OrderId, 1 }, subscription[..6]);
        Assert.Equal(paying.TrialEnd, new DateTimeOffset((DateTime)subscription[6]!), TimeSpan.FromSeconds(1));
        Assert.Equal(new object?[] { "visa", "4242", true }, Assert.Single(await AdminRowsAsync(
            "SELECT brand, last4, is_default FROM billing.payment_methods WHERE tenant_id = $1 AND detached_at IS NULL", paying.TenantId)));
        Assert.Equal("analyzing", await AdminScalarAsync<string>("SELECT status FROM shop.shops WHERE id = $1", paying.ShopId));
        Assert.Equal("EUR", (await AdminScalarAsync<string>("SELECT currency FROM iam.tenants WHERE id = $1", paying.TenantId))?.Trim());
        Assert.Equal("profiling", await AdminScalarAsync<string>("SELECT status FROM checks.runs WHERE id = $1", paying.RunId));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE run_id = $1 AND kind = 'run.profile'", paying.RunId));
        Assert.Equal(["processed", "processed"], (await AdminRowsAsync("SELECT status FROM billing.stripe_events WHERE id = ANY($1) ORDER BY id", (object)new[] { first, again }))
            .Select(r => (string)r[0]!));
    }

    [Fact]
    public async Task UnpaidSession_DoesNotReleaseTheRun_AndTheRunServiceRefusesAnUnpaidOrder()
    {
        await using var host = await HostAsync();
        var paying = await PayingAsync(host);
        host.Stripe.Sessions[paying.SessionId] = host.Stripe.Sessions[paying.SessionId] with { PaymentStatus = "unpaid" };

        var outcome = await ProcessAsync(host, await StoreAsync(host, "checkout.session.completed", "checkout.session", paying.SessionId));
        var released = await host.RunAsync(s =>
        {
            s.GetRequiredService<ITenantContext>().Set(paying.TenantId);
            return s.GetRequiredService<IRunService>().MarkOrderPaidAsync(paying.OrderId, Ct);
        });

        Assert.Equal("ignored", outcome.Status);
        Assert.Equal(RunCodes.OrderNotPaid, released.ErrorCode);
        Assert.Equal("checkout_open", await AdminScalarAsync<string>("SELECT status FROM billing.orders WHERE id = $1", paying.OrderId));
        Assert.Equal("awaiting_payment", await AdminScalarAsync<string>("SELECT status FROM checks.runs WHERE id = $1", paying.RunId));
    }

    [Fact]
    public async Task CheckoutExpired_ExpiresTheOrder_TheRunStillWaits_NothingIsCharged()
    {
        await using var host = await HostAsync();
        var paying = await PayingAsync(host);
        host.Stripe.Sessions[paying.SessionId] = host.Stripe.Sessions[paying.SessionId] with { Status = "expired", PaymentStatus = "unpaid", SubscriptionId = null };

        var outcome = await ProcessAsync(host, await StoreAsync(host, "checkout.session.expired", "checkout.session", paying.SessionId));

        Assert.Equal("processed", outcome.Status);
        Assert.Equal("expired", await AdminScalarAsync<string>("SELECT status FROM billing.orders WHERE id = $1", paying.OrderId));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'order.expired' AND entity_id = $1", paying.OrderId.ToString("D")));
        Assert.Equal("awaiting_payment", await AdminScalarAsync<string>("SELECT status FROM checks.runs WHERE id = $1", paying.RunId));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM billing.payments WHERE tenant_id = $1", paying.TenantId));
        Assert.DoesNotContain(host.Stripe.Calls, c => c.Key is not null);
    }

    [Fact]
    public async Task Out_of_order_updates_keep_latest_state()
    {
        await using var host = await HostAsync();
        var paying = await PayingAsync(host);
        await ProcessAsync(host, await StoreAsync(host, "checkout.session.completed", "checkout.session", paying.SessionId));
        var older = await StoreAsync(host, "customer.subscription.updated", "subscription", paying.SubscriptionId, new JsonObject { ["status"] = "trialing" },
            host.Time.GetUtcNow().AddMinutes(-10));
        host.Stripe.Subscriptions[paying.SubscriptionId] = host.Stripe.Subscriptions[paying.SubscriptionId] with { Status = "active", TrialEnd = host.Time.GetUtcNow() };
        var newer = await StoreAsync(host, "customer.subscription.updated", "subscription", paying.SubscriptionId, new JsonObject { ["status"] = "active" });

        await ProcessAsync(host, newer);
        await ProcessAsync(host, older);

        Assert.Equal("active", await AdminScalarAsync<string>("SELECT status FROM billing.subscriptions WHERE stripe_subscription_id = $1", paying.SubscriptionId));
    }

    [Fact]
    public async Task InvoicePaid_StoresThePayment_AndOneInvoiceJob()
    {
        await using var host = await HostAsync();
        var paying = await PayingAsync(host);
        await ProcessAsync(host, await StoreAsync(host, "checkout.session.completed", "checkout.session", paying.SessionId));
        var invoice = Invoice(host, paying, 8487, "subscription_create");

        var first = await StoreAsync(host, "invoice.paid", "invoice", invoice);
        await ProcessAsync(host, first);
        await ProcessAsync(host, await StoreAsync(host, "invoice.paid", "invoice", invoice));

        var payment = Assert.Single(await AdminRowsAsync(
            "SELECT amount_gross, currency, status, order_id, card_brand, card_last4 FROM billing.payments WHERE stripe_invoice_id = $1", invoice));
        Assert.Equal(new object?[] { 84.87m, "EUR", "succeeded", paying.OrderId, "visa", "4242" }, payment);
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind = 'billing.issue_invoice' AND dedupe_key = $1", "invoice:" + invoice));
        Assert.Equal(paying.ShopId, await AdminScalarAsync<Guid>("SELECT shop_id FROM ops.jobs WHERE dedupe_key = $1", "invoice:" + invoice));
    }

    [Fact]
    public async Task Reconciliation_AddsTheLostInvoicePaid_AndTheLateWebhookAddsNoSecondInvoice()
    {
        await using var host = await HostAsync();
        var paying = await PayingAsync(host);
        await ProcessAsync(host, await StoreAsync(host, "checkout.session.completed", "checkout.session", paying.SessionId));
        var invoice = Invoice(host, paying, 2337, "subscription_cycle");
        var lost = "evt_" + Guid.NewGuid().ToString("N");
        var json = StripeTestEvents.Json(lost, "invoice.paid", "invoice", invoice, created: host.Time.GetUtcNow().AddHours(-3));
        host.Stripe.Events.Enqueue(new StripeEventEnvelope(lost, "invoice.paid", invoice, false, host.Time.GetUtcNow().AddHours(-3), json));

        var result = await host.RunAsync(s => s.GetServices<IJobHandler>().OfType<ReconcileStripeHandler>().Single().RunAsync(Ct));
        await ProcessAsync(host, lost);
        var late = await host.RunAsync(s => s.GetRequiredService<StripeEventIntake>().StoreAsync(
            new StripeEventEnvelope(lost, "invoice.paid", invoice, false, host.Time.GetUtcNow().AddHours(-3), json), Ct));
        await ProcessAsync(host, lost);

        Assert.Equal(JobResult.Done, result);
        Assert.False(late);
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM billing.payments WHERE stripe_invoice_id = $1", invoice));
        Assert.Null(await AdminScalarAsync<Guid?>("SELECT order_id FROM billing.payments WHERE stripe_invoice_id = $1", invoice));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind = 'billing.issue_invoice' AND dedupe_key = $1", "invoice:" + invoice));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE dedupe_key = $1", "stripe:" + lost));
    }

    [Fact]
    public async Task PaymentFailed_IsPastDue_NotifiesTheMembers_AndIssuesNoInvoice()
    {
        await using var host = await HostAsync();
        var paying = await PayingAsync(host);
        var owner = await MemberAsync(paying.TenantId, "owner");
        await ProcessAsync(host, await StoreAsync(host, "checkout.session.completed", "checkout.session", paying.SessionId));
        host.Stripe.Subscriptions[paying.SubscriptionId] = host.Stripe.Subscriptions[paying.SubscriptionId] with { Status = "past_due" };
        var invoice = Invoice(host, paying, 0, "subscription_cycle", total: 2337, status: "open");

        await ProcessAsync(host, await StoreAsync(host, "invoice.payment_failed", "invoice", invoice));

        Assert.Equal("past_due", await AdminScalarAsync<string>("SELECT status FROM billing.subscriptions WHERE stripe_subscription_id = $1", paying.SubscriptionId));
        var notification = Assert.Single(await AdminRowsAsync("SELECT kind, params->>'amount', params->>'currency', route->>'key' FROM iam.notifications WHERE user_id = $1", owner));
        Assert.Equal(new object?[] { "payment_failed", "23.37", "EUR", "billing.overview" }, notification);
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.outbox WHERE tenant_id = $1 AND kind = 'email' AND payload->>'template' = 'payment_failed'", paying.TenantId));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE dedupe_key = $1", "invoice:" + invoice));
    }

    [Fact]
    public async Task SubscriptionDeleted_StopsTheMonitoring_AndTheDataStay()
    {
        await using var host = await HostAsync();
        var paying = await PayingAsync(host);
        await MemberAsync(paying.TenantId, "admin");
        await ProcessAsync(host, await StoreAsync(host, "checkout.session.completed", "checkout.session", paying.SessionId));
        await AdminAsync("UPDATE shop.shops SET status = 'active' WHERE id = $1", paying.ShopId);
        host.Stripe.Subscriptions[paying.SubscriptionId] = host.Stripe.Subscriptions[paying.SubscriptionId] with
        {
            Status = "canceled", CanceledAt = host.Time.GetUtcNow(), EndedAt = host.Time.GetUtcNow(),
        };

        var deleted = await StoreAsync(host, "customer.subscription.deleted", "subscription", paying.SubscriptionId);
        await ProcessAsync(host, deleted);
        await ProcessAsync(host, await StoreAsync(host, "customer.subscription.deleted", "subscription", paying.SubscriptionId));

        Assert.Equal(new object?[] { "canceled", "canceled" }, Assert.Single(await AdminRowsAsync(
            "SELECT status, pause_reason FROM billing.subscriptions WHERE stripe_subscription_id = $1", paying.SubscriptionId)));
        Assert.Equal("canceled", await AdminScalarAsync<string>("SELECT status FROM shop.shops WHERE id = $1", paying.ShopId));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'subscription.ended' AND tenant_id = $1", paying.TenantId));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM iam.notifications WHERE tenant_id = $1 AND kind = 'subscription_ended'", paying.TenantId));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM checks.runs WHERE id = $1", paying.RunId));
    }

    [Fact]
    public async Task EndAfterAFailedPayment_PausesTheActiveShop()
    {
        await using var host = await HostAsync();
        var paying = await PayingAsync(host);
        await ProcessAsync(host, await StoreAsync(host, "checkout.session.completed", "checkout.session", paying.SessionId));
        await AdminAsync("UPDATE shop.shops SET status = 'active' WHERE id = $1", paying.ShopId);
        host.Stripe.Subscriptions[paying.SubscriptionId] = host.Stripe.Subscriptions[paying.SubscriptionId] with { Status = "past_due" };
        await ProcessAsync(host, await StoreAsync(host, "customer.subscription.updated", "subscription", paying.SubscriptionId));
        host.Stripe.Subscriptions[paying.SubscriptionId] = host.Stripe.Subscriptions[paying.SubscriptionId] with { Status = "canceled", EndedAt = host.Time.GetUtcNow() };

        await ProcessAsync(host, await StoreAsync(host, "customer.subscription.deleted", "subscription", paying.SubscriptionId));

        Assert.Equal("payment_failed", await AdminScalarAsync<string>("SELECT pause_reason FROM billing.subscriptions WHERE stripe_subscription_id = $1", paying.SubscriptionId));
        Assert.Equal("paused", await AdminScalarAsync<string>("SELECT status FROM shop.shops WHERE id = $1", paying.ShopId));
    }

    [Fact]
    public async Task NewDefaultCard_AppliesToEveryRunningSubscription_AndDetachesTheOldOne()
    {
        await using var host = await HostAsync();
        var paying = await PayingAsync(host);
        await ProcessAsync(host, await StoreAsync(host, "checkout.session.completed", "checkout.session", paying.SessionId));
        var others = new List<string>();
        foreach (var (tier, price) in new[] { ("t500", 9m), ("t5000", 29m) })
        {
            var shop = await BillingData.ShopAsync(paying.TenantId);
            var other = host.Stripe.NewId("sub");
            host.Stripe.Subscriptions[other] = host.Stripe.Subscriptions[paying.SubscriptionId] with { Id = other, Status = "active" };
            await BillingData.SubscriptionAsync(paying.TenantId, shop, paying.PriceListId, tier, price, host.Time.GetUtcNow().AddDays(20), stripeSubscriptionId: other);
            others.Add(other);
        }

        var card = host.Stripe.NewId("pm");
        host.Stripe.PaymentMethods[card] = new StripePaymentMethodState(card, paying.CustomerId, "mastercard", "1881", 3, 2031);
        host.Stripe.Customers[paying.CustomerId] = host.Stripe.Customers[paying.CustomerId] with { DefaultPaymentMethodId = card };

        await ProcessAsync(host, await StoreAsync(host, "customer.updated", "customer", paying.CustomerId));

        Assert.All(others.Prepend(paying.SubscriptionId), id => Assert.Equal(card, host.Stripe.Subscriptions[id].DefaultPaymentMethodId));
        Assert.Equal(new object?[] { "mastercard", "1881" }, Assert.Single(await AdminRowsAsync(
            "SELECT brand, last4 FROM billing.payment_methods WHERE tenant_id = $1 AND is_default AND detached_at IS NULL", paying.TenantId)));
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM billing.payment_methods WHERE stripe_payment_method_id = $1 AND detached_at IS NOT NULL AND NOT is_default", paying.CardId));
        Assert.Equal(1, host.Stripe.Count(nameof(IStripeGateway.DetachPaymentMethodAsync)));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'billing.card_changed' AND tenant_id = $1", paying.TenantId));
    }

    [Fact]
    public async Task SetupIntentOfTheAccountCard_BecomesTheDefaultCard_AForeignOneIsIgnored()
    {
        await using var host = await HostAsync();
        var paying = await PayingAsync(host);
        await ProcessAsync(host, await StoreAsync(host, "checkout.session.completed", "checkout.session", paying.SessionId));
        var card = host.Stripe.NewId("pm");
        host.Stripe.PaymentMethods[card] = new StripePaymentMethodState(card, paying.CustomerId, "mastercard", "1881", 3, 2031);
        var foreign = host.Stripe.NewId("seti");
        host.Stripe.SetupIntents[foreign] = new StripeSetupIntentState(foreign, paying.CustomerId, "succeeded", card, new Dictionary<string, string>());
        var intent = host.Stripe.NewId("seti");
        host.Stripe.SetupIntents[intent] = new StripeSetupIntentState(intent, paying.CustomerId, "succeeded", card,
            new Dictionary<string, string> { ["purpose"] = StripeSetupIntentState.AccountCard });
        var defaults = host.Stripe.Count(nameof(IStripeGateway.SetCustomerDefaultPaymentMethodAsync));

        var ignored = await ProcessAsync(host, await StoreAsync(host, "setup_intent.succeeded", "setup_intent", foreign));

        Assert.Equal("ignored", ignored.Status);
        Assert.Equal(defaults, host.Stripe.Count(nameof(IStripeGateway.SetCustomerDefaultPaymentMethodAsync)));
        Assert.NotEqual(card, host.Stripe.Customers[paying.CustomerId].DefaultPaymentMethodId);

        var outcome = await ProcessAsync(host, await StoreAsync(host, "setup_intent.succeeded", "setup_intent", intent));

        Assert.Equal(("processed", paying.TenantId), (outcome.Status, outcome.TenantId));
        Assert.Equal(card, host.Stripe.Customers[paying.CustomerId].DefaultPaymentMethodId);
        Assert.Equal(card, host.Stripe.Subscriptions[paying.SubscriptionId].DefaultPaymentMethodId);
        Assert.Equal(new object?[] { "mastercard", "1881" }, Assert.Single(await AdminRowsAsync(
            "SELECT brand, last4 FROM billing.payment_methods WHERE tenant_id = $1 AND is_default AND detached_at IS NULL", paying.TenantId)));
    }

    [Fact]
    public async Task SecondRunningSubscriptionOfAShop_IsAnAlert_AndIsNotStored()
    {
        await using var host = await HostAsync();
        var paying = await PayingAsync(host);
        await ProcessAsync(host, await StoreAsync(host, "checkout.session.completed", "checkout.session", paying.SessionId));
        var second = host.Stripe.NewId("sub");
        host.Stripe.Subscriptions[second] = host.Stripe.Subscriptions[paying.SubscriptionId] with
        {
            Id = second, Status = "active", TrialEnd = null, Metadata = ShopMetadata(paying),
        };

        var outcome = await ProcessAsync(host, await StoreAsync(host, "customer.subscription.created", "subscription", second));

        Assert.Equal("processed", outcome.Status);
        Assert.Equal(new object?[] { paying.SubscriptionId, "trialing" }, Assert.Single(await AdminRowsAsync(
            "SELECT stripe_subscription_id, status FROM billing.subscriptions WHERE shop_id = $1", paying.ShopId)));
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.audit_log WHERE action = 'billing.alert.second_running_subscription' AND entity_type = 'subscription' AND entity_id = $1", second));
    }

    [Fact]
    public async Task SubscriptionStartedAgain_MakesTheEndedMonitoringActive_AndKeepsTheHistory()
    {
        await using var host = await HostAsync();
        var paying = await PayingAsync(host);
        await ProcessAsync(host, await StoreAsync(host, "checkout.session.completed", "checkout.session", paying.SessionId));
        var now = host.Time.GetUtcNow();
        host.Stripe.Subscriptions[paying.SubscriptionId] = host.Stripe.Subscriptions[paying.SubscriptionId] with { Status = "canceled", CanceledAt = now, EndedAt = now };
        await ProcessAsync(host, await StoreAsync(host, "customer.subscription.deleted", "subscription", paying.SubscriptionId));
        Assert.Equal("canceled", await AdminScalarAsync<string>("SELECT status FROM shop.shops WHERE id = $1", paying.ShopId));
        var again = host.Stripe.NewId("sub");
        host.Stripe.Subscriptions[again] = host.Stripe.Subscriptions[paying.SubscriptionId] with
        {
            Id = again, Status = "active", TrialEnd = null, CanceledAt = null, EndedAt = null, Metadata = ShopMetadata(paying),
        };

        await ProcessAsync(host, await StoreAsync(host, "customer.subscription.created", "subscription", again));
        await ProcessAsync(host, await StoreAsync(host, "customer.subscription.updated", "subscription", again));

        Assert.Equal("active", await AdminScalarAsync<string>("SELECT status FROM shop.shops WHERE id = $1", paying.ShopId));
        Assert.Equal(["active", "canceled"], (await AdminRowsAsync("SELECT status FROM billing.subscriptions WHERE shop_id = $1 ORDER BY status", paying.ShopId))
            .Select(r => (string)r[0]!));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'subscription.restarted' AND tenant_id = $1", paying.TenantId));
    }

    [Fact]
    public async Task SubscriptionThatNeverStarted_EndsWithoutTouchingTheShop()
    {
        await using var host = await HostAsync();
        var paying = await PayingAsync(host);
        await MemberAsync(paying.TenantId, "owner");
        host.Stripe.Subscriptions[paying.SubscriptionId] = host.Stripe.Subscriptions[paying.SubscriptionId] with { Status = "incomplete", TrialEnd = null };
        await ProcessAsync(host, await StoreAsync(host, "customer.subscription.created", "subscription", paying.SubscriptionId));
        Assert.Equal("incomplete", await AdminScalarAsync<string>("SELECT status FROM billing.subscriptions WHERE stripe_subscription_id = $1", paying.SubscriptionId));
        var now = host.Time.GetUtcNow();
        host.Stripe.Subscriptions[paying.SubscriptionId] = host.Stripe.Subscriptions[paying.SubscriptionId] with { Status = "canceled", CanceledAt = now, EndedAt = now };

        await ProcessAsync(host, await StoreAsync(host, "customer.subscription.deleted", "subscription", paying.SubscriptionId));

        Assert.Equal(new object?[] { "canceled", "not_started" }, Assert.Single(await AdminRowsAsync(
            "SELECT status, pause_reason FROM billing.subscriptions WHERE stripe_subscription_id = $1", paying.SubscriptionId)));
        Assert.Equal("awaiting_payment", await AdminScalarAsync<string>("SELECT status FROM shop.shops WHERE id = $1", paying.ShopId));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'subscription.ended' AND tenant_id = $1", paying.TenantId));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM iam.notifications WHERE tenant_id = $1", paying.TenantId));
    }

    [Fact]
    public async Task VerifiedVatId_IsStoredForTheTenant()
    {
        await using var host = await HostAsync();
        var paying = await PayingAsync(host);
        await AdminAsync("UPDATE iam.tenants SET country_code = 'CZ', ic_dph = 'CZ12345678', tax_id_status = 'pending' WHERE id = $1", paying.TenantId);
        var taxId = host.Stripe.NewId("txi");
        host.Stripe.Customers[paying.CustomerId] = host.Stripe.Customers[paying.CustomerId] with
        {
            TaxIds = [new StripeTaxIdState(taxId, "eu_vat", "CZ 12345678", "verified")],
        };

        var outcome = await ProcessAsync(host, await StoreAsync(host, "customer.tax_id.updated", "tax_id", taxId, new JsonObject { ["customer"] = paying.CustomerId }));

        Assert.Equal("processed", outcome.Status);
        var row = Assert.Single(await AdminRowsAsync("SELECT tax_id_status, tax_id_verified_at IS NOT NULL FROM iam.tenants WHERE id = $1", paying.TenantId));
        Assert.Equal(new object?[] { "verified", true }, row);
    }

    [Fact]
    public async Task Refund_IsRecorded_WithOneCreditNotePerRefund()
    {
        await using var host = await HostAsync();
        var paying = await PayingAsync(host);
        await ProcessAsync(host, await StoreAsync(host, "checkout.session.completed", "checkout.session", paying.SessionId));
        var invoice = Invoice(host, paying, 8487, "subscription_create");
        await ProcessAsync(host, await StoreAsync(host, "invoice.paid", "invoice", invoice));
        var charge = host.Stripe.NewId("ch");
        var refund = host.Stripe.NewId("re");
        host.Stripe.Charges[charge] = new StripeChargeState(charge, paying.CustomerId, null, invoice, "eur", 8487, 1000,
            [new StripeRefundState(refund, 1000, "succeeded", host.Time.GetUtcNow())]);

        await ProcessAsync(host, await StoreAsync(host, "charge.refunded", "charge", charge));
        await ProcessAsync(host, await StoreAsync(host, "charge.refunded", "charge", charge));

        Assert.Equal(new object?[] { "partially_refunded", 10m, charge }, Assert.Single(await AdminRowsAsync(
            "SELECT status, refunded_amount, stripe_charge_id FROM billing.payments WHERE stripe_invoice_id = $1", invoice)));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind = 'billing.issue_credit_note' AND dedupe_key = $1", "credit:" + refund));
    }

    [Fact]
    public async Task TransientErrorOfStripe_FailsTheEventForARetry_ThenItIsProcessed()
    {
        await using var host = await HostAsync();
        var paying = await PayingAsync(host);
        var id = await StoreAsync(host, "checkout.session.completed", "checkout.session", paying.SessionId);
        host.Stripe.FailNext = new StripeGatewayException("stripe.api_error", 500, true);

        var failed = await ProcessAsync(host, id);
        var status = await AdminScalarAsync<string>("SELECT status FROM billing.stripe_events WHERE id = $1", id);
        var retried = await ProcessAsync(host, id);

        Assert.Equal(("failed", "stripe.api_error", true), (failed.Status, failed.ErrorCode, failed.Transient));
        Assert.Equal("failed", status);
        Assert.Equal("processed", retried.Status);
        Assert.Equal(2, await AdminScalarAsync<int>("SELECT attempts FROM billing.stripe_events WHERE id = $1", id));
        Assert.Equal("paid", await AdminScalarAsync<string>("SELECT status FROM billing.orders WHERE id = $1", paying.OrderId));
    }

    [Fact]
    public async Task UnknownTypesAndForeignObjects_AreIgnored()
    {
        await using var host = await HostAsync();
        var paying = await PayingAsync(host);
        var foreign = host.Stripe.NewId("cs_test");
        host.Stripe.Sessions[foreign] = host.Stripe.Sessions[paying.SessionId] with { Id = foreign, CustomerId = "cus_someone_else" };

        var unknown = await ProcessAsync(host, await StoreAsync(host, "product.created", "product", "prod_x"));
        var mismatch = await ProcessAsync(host, await StoreAsync(host, "checkout.session.completed", "checkout.session", foreign));

        Assert.Equal("ignored", unknown.Status);
        Assert.Equal("ignored", mismatch.Status);
        Assert.Equal("checkout_open", await AdminScalarAsync<string>("SELECT status FROM billing.orders WHERE id = $1", paying.OrderId));
    }

    [Fact]
    public async Task CustomerOfTheTenant_IsCreatedOnce()
    {
        await using var host = await HostAsync();
        var market = await BillingData.MarketAsync();
        var tenant = await BillingData.TenantAsync(market, icDph: "SK2020123456");

        var first = await host.RunAsync(s => EnsureAsync(s, tenant));
        var second = await host.RunAsync(s => EnsureAsync(s, tenant));

        Assert.Equal(first, second);
        Assert.Equal(1, host.Stripe.Count(nameof(IStripeGateway.CreateCustomerAsync)));
        Assert.Equal(first, await AdminScalarAsync<string>("SELECT stripe_customer_id FROM iam.tenants WHERE id = $1", tenant));
        var request = host.Stripe.CustomerRequests[first];
        Assert.Equal(("Bylinkovo s.r.o.", "fakturacia@bylinkovo.test", "sk", "SK"), (request.Name, request.Email, request.PreferredLocale, request.Address.Country));
        Assert.Equal("SK2020123456", Assert.Single(host.Stripe.Customers[first].TaxIds).Value);

        static Task<string> EnsureAsync(IServiceProvider services, Guid tenant)
        {
            services.GetRequiredService<ITenantContext>().Set(tenant);
            return services.GetRequiredService<StripeCustomers>().EnsureAsync(tenant, Ct);
        }
    }
}
