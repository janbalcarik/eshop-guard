using System.Text.Json.Nodes;
using EshopGuard.Billing.Stripe;
using EshopGuard.Billing.Tax;
using EshopGuard.Billing.Webhooks;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Tenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EshopGuard.Billing.Invoicing;

/// <summary>
/// The state of the invoice of a paid invoice of Stripe after <see cref="InvoiceIssuer"/>: the row status (<c>creating</c>,
/// <c>needs_review</c>, <c>issued</c>, <c>failed</c>), <see cref="Skipped"/> when nothing was paid, or <see cref="NotCreated"/> with a
/// code when no row could be made.
/// </summary>
public sealed record InvoiceIssueOutcome(string Status, Guid? InvoiceId, string? Code = null)
{
    public const string Skipped = "skipped";
    public const string NotCreated = "not_created";
}

/// <summary>
/// The invoice of a paid invoice of Stripe (design „Doklady“, job <c>billing.issue_invoice</c>). One row of <c>billing.invoices</c> per
/// invoice of Stripe (<c>source_key</c>, unique; a repeated job finds it): the buyer from the tenant, the lines, amounts and reverse
/// charge as Stripe charged them (codes and numbers, the texts come with the invoice), and the tax check (task 9.3,
/// <see cref="InvoiceTaxCheck"/>) against the treatment of the order (<c>orders.tax_treatment</c>), for a renewal against the current
/// treatment of the tenant. A mismatch is <c>needs_review</c> with <c>tax_mismatch</c>, the expected and the charged treatment in the
/// audit and an alert of operations, and nothing goes to SuperFaktúra; a matching invoice stays <c>creating</c> for SuperFaktúra (group 10).
/// </summary>
public sealed class InvoiceIssuer(
    EshopGuardDataSource dataSource,
    IStripeGateway stripe,
    IOptions<BillingOptions> options,
    TimeProvider time,
    ILogger<InvoiceIssuer> logger)
{
    public const string ShopUnknown = "billing.invoice_shop_unknown";

    public async Task<InvoiceIssueOutcome> IssueAsync(Guid tenantId, Guid? shopId, string stripeInvoiceId, CancellationToken ct)
    {
        var invoice = await stripe.GetInvoiceAsync(stripeInvoiceId, ct).ConfigureAwait(false);
        if (invoice.AmountPaid <= 0)
        {
            logger.LogInformation("invoice.skipped_unpaid {StripeInvoiceId} {TenantId}", invoice.Id, tenantId);
            return new InvoiceIssueOutcome(InvoiceIssueOutcome.Skipped, null);
        }

        var now = time.GetUtcNow();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, null, ct).ConfigureAwait(false);
        if (await ExistingAsync(transaction, invoice.Id, ct).ConfigureAwait(false) is { } existing)
        {
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return existing;
        }

        var payment = (await BillingSql.ListAsync(transaction, "SELECT id, order_id FROM billing.payments WHERE stripe_invoice_id = $1",
            r => (Id: r.GetGuid(0), OrderId: r.IsDBNull(1) ? (Guid?)null : r.GetGuid(1)), ct, invoice.Id).ConfigureAwait(false)).FirstOrDefault();
        var orderId = payment.OrderId;
        if (orderId is null && invoice is { BillingReason: "subscription_create", SubscriptionId: { } created })
        {
            orderId = await BillingSql.ScalarAsync<Guid?>(transaction, "SELECT id FROM billing.orders WHERE stripe_subscription_id = $1", ct, created).ConfigureAwait(false);
        }

        var order = orderId is { } ordered
            ? (await BillingSql.ListAsync(transaction, "SELECT shop_id, tax_treatment FROM billing.orders WHERE id = $1",
                r => (ShopId: (Guid?)r.GetGuid(0), Treatment: r.Get<string>(1)), ct, ordered).ConfigureAwait(false)).FirstOrDefault()
            : default;
        shopId ??= invoice.SubscriptionId is { } subscriptionId
            ? await BillingSql.ScalarAsync<Guid?>(transaction, "SELECT shop_id FROM billing.subscriptions WHERE stripe_subscription_id = $1", ct, subscriptionId).ConfigureAwait(false)
            : null;
        shopId ??= order.ShopId;
        if (shopId is not { } shop)
        {
            await BillingAlerts.RaiseAsync(transaction, tenantId, "billing.alert.invoice_shop_unknown", "stripe_invoice", invoice.Id, now, logger, ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return new InvoiceIssueOutcome(InvoiceIssueOutcome.NotCreated, null, ShopUnknown);
        }

        var tenant = await TenantAsync(transaction, tenantId, ct).ConfigureAwait(false);
        var (expected, source) = order.Treatment is { } treatment
            ? (SnakeCaseEnumConverter<TaxTreatment>.FromText(treatment), "order")
            : (TaxTreatmentResolver.Resolve(tenant.Buyer, options.Value.Tax), "tenant");
        var mismatch = InvoiceTaxCheck.Check(expected, invoice, options.Value.Tax);
        var invoiceId = Guid.CreateVersion7();
        var status = mismatch is null ? InvoiceStatus.Creating : InvoiceStatus.NeedsReview;
        var inserted = await BillingSql.ExecuteAsync(transaction,
            """
            INSERT INTO billing.invoices (id, tenant_id, shop_id, payment_id, kind, buyer, items, amount_net, vat_amount, amount_gross, currency, reverse_charge,
                einvoice_required, einvoice_status, status, source_key, source_kind, needs_review_reason, created_at, updated_at)
            VALUES ($1, $2, $3, $4, 'invoice', $5, $6, $7, $8, $9, $10, $11, false, 'not_required', $12, $13, 'stripe_invoice', $14, $15, $15)
            ON CONFLICT (source_key) DO NOTHING
            """, ct, invoiceId, tenantId, shop, payment.Id == Guid.Empty ? (Guid?)null : payment.Id, BillingSql.Json(tenant.Snapshot), BillingSql.Json(Items(invoice)),
            Money.FromMinor(invoice.TotalExcludingTax), Money.FromMinor(invoice.TaxAmount), Money.FromMinor(invoice.Total), invoice.Currency.ToUpperInvariant(),
            InvoiceTaxCheck.Actual(invoice) == "reverse_charge", SnakeCaseEnumConverter<InvoiceStatus>.ToText(status), invoice.Id,
            BillingSql.Text(mismatch is null ? null : InvoiceTaxCheck.MismatchReason), now).ConfigureAwait(false);
        if (inserted == 0)
        {
            var other = await ExistingAsync(transaction, invoice.Id, ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return other ?? throw new InvalidOperationException("invoice.source_key_conflict");
        }

        if (mismatch is not null)
        {
            await BillingSql.AuditAsync(transaction, tenantId, null, BillingSql.System, "invoice.needs_review", "invoice", invoiceId.ToString("D"),
                new JsonObject
                {
                    ["reason"] = InvoiceTaxCheck.MismatchReason,
                    ["expected"] = mismatch.Expected,
                    ["expected_source"] = source,
                    ["actual"] = mismatch.Actual,
                    ["tax_amount"] = mismatch.TaxAmount,
                    ["expected_tax_amount"] = mismatch.ExpectedTaxAmount,
                    ["stripe_invoice_id"] = invoice.Id,
                }, now, ct).ConfigureAwait(false);
            await BillingAlerts.RaiseAsync(transaction, tenantId, "billing.alert.tax_mismatch", "invoice", invoiceId.ToString("D"), now, logger, ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        if (mismatch is null)
        {
            logger.LogInformation("invoice.awaiting_superfaktura {InvoiceId} {TenantId} {Treatment}", invoiceId, tenantId, Text(expected));
        }
        else
        {
            logger.LogWarning("invoice.tax_mismatch {InvoiceId} {TenantId} {Expected} {Actual}", invoiceId, tenantId, mismatch.Expected, mismatch.Actual);
        }

        return new InvoiceIssueOutcome(SnakeCaseEnumConverter<InvoiceStatus>.ToText(status), invoiceId, mismatch is null ? null : InvoiceTaxCheck.MismatchReason);
    }

    /// <summary>The lines of the invoice of Stripe as codes and numbers (amounts in the currency, periods in UTC).</summary>
    public static JsonArray Items(StripeInvoiceState invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        var items = new JsonArray();
        foreach (var line in invoice.Lines)
        {
            items.Add(new JsonObject
            {
                ["stripe_line_id"] = line.Id,
                ["price_id"] = line.PriceId,
                ["quantity"] = line.Quantity,
                ["amount"] = Money.FromMinor(line.Amount),
                ["discount_amount"] = Money.FromMinor(line.DiscountAmount),
                ["tax_amount"] = Money.FromMinor(line.TaxAmount),
                ["period_start"] = line.PeriodStart,
                ["period_end"] = line.PeriodEnd,
            });
        }

        return items;
    }

    private static string Text(TaxTreatment treatment) => SnakeCaseEnumConverter<TaxTreatment>.ToText(treatment);

    private static async Task<InvoiceIssueOutcome?> ExistingAsync(NpgsqlTransaction transaction, string stripeInvoiceId, CancellationToken ct)
    {
        var rows = await BillingSql.ListAsync(transaction, "SELECT id, status, needs_review_reason FROM billing.invoices WHERE source_key = $1",
            r => new InvoiceIssueOutcome(r.GetString(1), r.GetGuid(0), r.Get<string>(2)), ct, stripeInvoiceId).ConfigureAwait(false);
        return rows.FirstOrDefault();
    }

    private static async Task<(TaxBuyer Buyer, JsonObject Snapshot)> TenantAsync(NpgsqlTransaction transaction, Guid tenantId, CancellationToken ct)
    {
        var rows = await BillingSql.ListAsync(transaction,
            "SELECT name, legal_name, ico, dic, ic_dph, street, city, postal_code, country_code, locale, tax_id_status FROM iam.tenants WHERE id = $1",
            r => (Name: r.GetString(0), LegalName: r.Get<string>(1), Ico: r.Get<string>(2), Dic: r.Get<string>(3), IcDph: r.Get<string>(4), Street: r.Get<string>(5),
                City: r.Get<string>(6), PostalCode: r.Get<string>(7), Country: r.GetString(8), Locale: r.GetString(9), TaxIdStatus: r.GetString(10)),
            ct, tenantId).ConfigureAwait(false);
        var tenant = rows.Single();
        var buyer = new TaxBuyer(tenant.Country, tenant.Ico, tenant.IcDph, SnakeCaseEnumConverter<TaxIdStatus>.FromText(tenant.TaxIdStatus));
        var snapshot = new JsonObject
        {
            ["name"] = tenant.LegalName ?? tenant.Name,
            ["ico"] = tenant.Ico,
            ["dic"] = tenant.Dic,
            ["ic_dph"] = tenant.IcDph,
            ["street"] = tenant.Street,
            ["city"] = tenant.City,
            ["postal_code"] = tenant.PostalCode,
            ["country_code"] = tenant.Country.Trim().ToUpperInvariant(),
            ["locale"] = tenant.Locale,
            ["tax_id_status"] = tenant.TaxIdStatus,
        };
        return (buyer, snapshot);
    }
}
