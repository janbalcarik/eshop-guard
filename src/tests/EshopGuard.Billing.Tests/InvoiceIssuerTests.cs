using EshopGuard.Billing.Invoicing;
using EshopGuard.Billing.Jobs;
using EshopGuard.Billing.Stripe;
using EshopGuard.Jobs.Processing;
using Microsoft.Extensions.DependencyInjection;
using static EshopGuard.Billing.Tests.BillingTestHost;
using static EshopGuard.Billing.Tests.StripeScenario;

namespace EshopGuard.Billing.Tests;

/// <summary>
/// The invoice of a paid invoice of Stripe and its tax check (change 12, task 9.3; scenario „Nesoulad daně ve Stripe“): one row per
/// invoice of Stripe, a mismatch stops it for review with an alert of operations, a matching one waits for SuperFaktúra.
/// </summary>
[Trait("Category", "Db")]
public sealed class InvoiceIssuerTests
{
    [Fact]
    public async Task MatchingTax_OneInvoiceAwaitingSuperFaktura_EvenWhenTheJobRunsTwice()
    {
        await using var host = await HostAsync();
        var paying = await PaidCheckoutAsync(host);
        var invoice = await PaidAsync(host, paying, 6900, 1587);

        Assert.Same(JobResult.Done, await IssueAsync(host, paying.TenantId, paying.ShopId, invoice));
        Assert.Same(JobResult.Done, await IssueAsync(host, paying.TenantId, paying.ShopId, invoice));

        var row = Assert.Single(await AdminRowsAsync(
            """
            SELECT i.status, i.kind, i.source_kind, i.shop_id, i.payment_id = p.id, i.amount_net, i.vat_amount, i.amount_gross, i.currency, i.reverse_charge,
                   i.needs_review_reason, i.buyer->>'ico', i.buyer->>'name', i.buyer->>'country_code', i.buyer->'billing_email' IS NULL, jsonb_array_length(i.items),
                   i.items->0->>'price_id'
            FROM billing.invoices i JOIN billing.payments p ON p.stripe_invoice_id = i.source_key
            WHERE i.source_key = $1
            """, invoice));
        Assert.Equal(new object?[]
        {
            "creating", "invoice", "stripe_invoice", paying.ShopId, true, 69m, 15.87m, 84.87m, "EUR", false, null, "12345678", "Bylinkovo s.r.o.", "SK", true, 1,
            "price_line",
        }, row);
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE tenant_id = $1 AND action LIKE 'billing.alert.%'", paying.TenantId));
    }

    [Fact]
    public async Task TaxMismatchInStripe_StopsTheInvoiceForReview_WithOneAlertOfOperations()
    {
        await using var host = await HostAsync();
        var paying = await PaidCheckoutAsync(host);
        var invoice = await PaidAsync(host, paying, 6900, 0, reverse: true);

        Assert.Same(JobResult.Done, await IssueAsync(host, paying.TenantId, paying.ShopId, invoice));
        Assert.Same(JobResult.Done, await IssueAsync(host, paying.TenantId, paying.ShopId, invoice));

        var row = Assert.Single(await AdminRowsAsync("SELECT id, status, needs_review_reason, reverse_charge FROM billing.invoices WHERE source_key = $1", invoice));
        Assert.Equal(new object?[] { "needs_review", "tax_mismatch", true }, row[1..]);
        var id = ((Guid)row[0]!).ToString("D");
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.audit_log WHERE action = 'billing.alert.tax_mismatch' AND entity_type = 'invoice' AND entity_id = $1", id));
        Assert.Equal(new object?[] { "tax_mismatch", "domestic_vat", "order", "reverse_charge", 1587L, invoice }, Assert.Single(await AdminRowsAsync(
            """
            SELECT data->>'reason', data->>'expected', data->>'expected_source', data->>'actual', (data->>'expected_tax_amount')::bigint, data->>'stripe_invoice_id'
            FROM ops.audit_log WHERE action = 'invoice.needs_review' AND entity_id = $1
            """, id)));
    }

    [Fact]
    public async Task Renewal_IsCheckedAgainstTheCurrentTreatmentOfTheTenant()
    {
        await using var host = await HostAsync();
        var paying = await PaidCheckoutAsync(host);
        await AdminAsync("UPDATE iam.tenants SET country_code = 'CZ', ic_dph = 'CZ12345678', tax_id_status = 'verified' WHERE id = $1", paying.TenantId);
        var domestic = await PaidAsync(host, paying, 1900, 437, "subscription_cycle");
        var reverse = await PaidAsync(host, paying, 1900, 0, "subscription_cycle", reverse: true);

        await IssueAsync(host, paying.TenantId, paying.ShopId, domestic);
        await IssueAsync(host, paying.TenantId, paying.ShopId, reverse);

        Assert.Equal(new object?[] { "needs_review", false }, Assert.Single(await AdminRowsAsync(
            "SELECT status, reverse_charge FROM billing.invoices WHERE source_key = $1", domestic)));
        Assert.Equal(new object?[] { "reverse_charge", "tenant", "domestic_vat" }, Assert.Single(await AdminRowsAsync(
            """
            SELECT a.data->>'expected', a.data->>'expected_source', a.data->>'actual'
            FROM ops.audit_log a JOIN billing.invoices i ON a.entity_id = i.id::text
            WHERE a.action = 'invoice.needs_review' AND i.source_key = $1
            """, domestic)));
        Assert.Equal(new object?[] { "creating", true, 19m, 0m }, Assert.Single(await AdminRowsAsync(
            "SELECT status, reverse_charge, amount_net, vat_amount FROM billing.invoices WHERE source_key = $1", reverse)));
    }

    [Fact]
    public async Task UnpaidInvoice_IsSkipped_WithoutARow()
    {
        await using var host = await HostAsync();
        var paying = await PaidCheckoutAsync(host);
        var invoice = Invoice(host, paying, 0, "subscription_create", total: 0);

        Assert.Same(JobResult.Done, await IssueAsync(host, paying.TenantId, paying.ShopId, invoice));

        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM billing.invoices WHERE source_key = $1", invoice));
    }

    [Fact]
    public async Task InvoiceOfNoKnownShop_FailsWithAnAlert_AndNoRow()
    {
        await using var host = await HostAsync();
        var paying = await PayingAsync(host);
        var invoice = Invoice(host, paying, 1900, "manual");
        host.Stripe.Invoices[invoice] = host.Stripe.Invoices[invoice] with { SubscriptionId = null };

        var result = await IssueAsync(host, paying.TenantId, null, invoice);

        Assert.Equal(new JobResult.Fail(InvoiceIssuer.ShopUnknown), result);
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM billing.invoices WHERE source_key = $1", invoice));
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.audit_log WHERE action = 'billing.alert.invoice_shop_unknown' AND entity_type = 'stripe_invoice' AND entity_id = $1", invoice));
    }

    [Fact]
    public async Task ErrorsOfStripe_ThatMayPassAreTriedAgain_OthersFail()
    {
        await using var host = await HostAsync();
        var paying = await PaidCheckoutAsync(host);
        var invoice = await PaidAsync(host, paying, 6900, 1587);

        host.Stripe.FailNext = new StripeGatewayException("stripe.api_error", 500, true);
        Assert.Equal(new JobResult.Retry("stripe.api_error"), await IssueAsync(host, paying.TenantId, paying.ShopId, invoice));
        host.Stripe.FailNext = new StripeGatewayException("stripe.resource_missing", 404, false);
        Assert.Equal(new JobResult.Fail("stripe.resource_missing"), await IssueAsync(host, paying.TenantId, paying.ShopId, invoice));

        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM billing.invoices WHERE source_key = $1", invoice));
    }

    private static async Task<Paying> PaidCheckoutAsync(BillingTestHost host)
    {
        var paying = await PayingAsync(host);
        await ProcessAsync(host, await StoreAsync(host, "checkout.session.completed", "checkout.session", paying.SessionId));
        return paying;
    }

    /// <summary>A paid invoice of Stripe with one line and its tax, processed as <c>invoice.paid</c> (the payment and the job).</summary>
    private static async Task<string> PaidAsync(BillingTestHost host, Paying paying, long net, long tax, string reason = "subscription_create", bool reverse = false)
    {
        var id = Invoice(host, paying, net + tax, reason);
        var now = host.Time.GetUtcNow();
        host.Stripe.Invoices[id] = host.Stripe.Invoices[id] with
        {
            Subtotal = net,
            TotalExcludingTax = net,
            TaxAmount = tax,
            ReverseCharge = reverse,
            Lines = [new StripeInvoiceLine(host.Stripe.NewId("il"), null, net, 1, "price_line", now, now.AddMonths(1), 0, tax)],
        };
        await ProcessAsync(host, await StoreAsync(host, "invoice.paid", "invoice", id));
        return id;
    }

    private static Task<JobResult> IssueAsync(BillingTestHost host, Guid tenantId, Guid? shopId, string stripeInvoiceId) =>
        host.RunAsync(s => s.GetServices<IJobHandler>().OfType<IssueInvoiceHandler>().Single().RunAsync(tenantId, shopId, stripeInvoiceId, Ct));
}
