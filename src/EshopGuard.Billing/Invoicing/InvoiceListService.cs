using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EshopGuard.Application.Options;
using EshopGuard.Application.Problems;
using EshopGuard.Billing.Contracts;
using EshopGuard.Billing.Subscriptions;
using EshopGuard.Billing.Tax;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Tenancy;
using EshopGuard.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Billing.Invoicing;

/// <summary>The PDF of an issued document under the tenant: <c>tenants/{tenantId}/invoices/{invoiceId}.pdf</c> (design, part SuperFaktúra).</summary>
public static partial class InvoicePdf
{
    public static BlobKey Key(Guid tenantId, Guid invoiceId) => BlobKey.ForTenant(tenantId, "invoices", invoiceId.ToString("D") + ".pdf");

    /// <summary>The file name by the number of the document (characters outside letters, digits, <c>.</c>, <c>_</c>, <c>-</c> become <c>_</c>).</summary>
    public static string FileName(string? number, Guid invoiceId)
    {
        var safe = Unsafe().Replace(number ?? string.Empty, "_").Trim('_', '.');
        return (safe.Length == 0 ? invoiceId.ToString("D") : safe) + ".pdf";
    }

    [GeneratedRegex("[^A-Za-z0-9._-]", RegexOptions.CultureInvariant)]
    private static partial Regex Unsafe();
}

/// <summary>The files of the ZIP of invoices and how many documents of the filter have no PDF (header <c>X-EshopGuard-Skipped</c>).</summary>
public sealed record InvoiceZipPlan(IReadOnlyList<InvoiceZipFile> Files, int Skipped, string FileName);

public sealed record InvoiceZipFile(BlobKey Key, string Name);

/// <summary>
/// The invoices of the tenant (change 12, group 11; design Billing, „Faktúry“): documents of <c>billing.invoices</c> and the next
/// payment of every running subscription as „Naplánovaná“, both by e-shop and by the local year. The scheduled payments are those
/// of <see cref="BillingOverviewService"/>, so the list and the overview on the same page agree. A PDF is given only for an issued
/// document whose file has the key of the document (<see cref="InvoicePdf"/>); every other document of the filter is listed
/// without a PDF and counted as skipped in the ZIP (fail-closed, nothing is hidden).
/// </summary>
public sealed class InvoiceListService(
    EshopGuardDb db,
    BillingOverviewService overview,
    IBlobStore blobs,
    IOptions<BillingOptions> options,
    IOptions<LocalizationOptions> localization,
    ILogger<InvoiceListService> logger)
{
    /// <summary>The most documents in one ZIP (<c>422 billing.zip_too_large</c> above; a narrower filter helps).</summary>
    public const int ZipMaxDocuments = 500;

    public const string Scheduled = "scheduled";
    public const string Paid = "paid";
    public const string Refunded = "refunded";
    public const string Issued = "issued";
    public const string Analysis = "analysis";
    public const string Monitoring = "monitoring";

    public async Task<InvoiceListDto> ListAsync(Guid? shopId, int? year, CancellationToken ct)
    {
        var zone = Zone();
        var documents = await DocumentsAsync(shopId, zone, ct).ConfigureAwait(false);
        var rows = documents.Select(d => (d.Row, d.At)).Concat(await ScheduledAsync(shopId, zone, ct).ConfigureAwait(false)).ToList();
        var years = rows.Select(r => r.Row.Date.Year).Distinct().OrderDescending().ToList();
        var items = rows
            .Where(r => year is null || r.Row.Date.Year == year)
            .OrderByDescending(r => r.At)
            .ThenByDescending(r => r.Row.Kind == Scheduled)
            .ThenBy(r => r.Row.ShopDomain, StringComparer.Ordinal)
            .ThenByDescending(r => r.Row.Number, StringComparer.Ordinal)
            .Select(r => r.Row)
            .ToList();
        return new InvoiceListDto(items, years);
    }

    /// <summary>The stored PDF of an issued document; <c>404 billing.invoice_not_found</c> for an unknown or foreign one, <c>409 billing.invoice_pdf_missing</c> before it is issued.</summary>
    public async Task<(BlobKey Key, string FileName)> PdfAsync(Guid invoiceId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var invoice = await db.Invoices.AsNoTracking()
                .Where(i => i.Id == invoiceId)
                .Select(i => new { i.TenantId, i.Status, i.PdfBlobKey, i.Number })
                .FirstOrDefaultAsync(ct).ConfigureAwait(false)
                ?? throw new DomainException(BillingCodes.InvoiceNotFound, 404);
            var key = InvoicePdf.Key(invoice.TenantId, invoiceId);
            return invoice.Status == InvoiceStatus.Issued && invoice.PdfBlobKey == key.Value
                ? (key, InvoicePdf.FileName(invoice.Number, invoiceId))
                : throw new DomainException(BillingCodes.InvoicePdfMissing, 409);
        }, ct).ConfigureAwait(false);

    /// <summary>
    /// The PDFs of the issued documents of the filter whose file exists, named by the number (a repeated name gets <c>-2</c>, <c>-3</c>…);
    /// the other documents of the filter are skipped. More than <see cref="ZipMaxDocuments"/> documents is <c>422 billing.zip_too_large</c>.
    /// </summary>
    public async Task<InvoiceZipPlan> ZipAsync(Guid? shopId, int? year, CancellationToken ct)
    {
        var tenantId = db.TenantContext.RequireTenantId();
        var documents = (await DocumentsAsync(shopId, Zone(), ct).ConfigureAwait(false))
            .Where(d => year is null || d.Row.Date.Year == year)
            .ToList();
        var withPdf = documents.Where(d => d.Row.HasPdf).OrderBy(d => d.At).ThenBy(d => d.Row.Number, StringComparer.Ordinal).ToList();
        if (withPdf.Count > ZipMaxDocuments)
        {
            throw new DomainException(BillingCodes.ZipTooLarge, 422, new Dictionary<string, object?> { ["max"] = ZipMaxDocuments, ["count"] = withPdf.Count });
        }

        var files = new List<InvoiceZipFile>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in withPdf)
        {
            var id = document.Row.Id!.Value;
            var key = InvoicePdf.Key(tenantId, id);
            if (!await blobs.ExistsAsync(key, ct).ConfigureAwait(false))
            {
                logger.LogWarning("invoice.pdf_file_missing {InvoiceId} {TenantId}", id, tenantId);
                continue;
            }

            files.Add(new InvoiceZipFile(key, Unique(names, InvoicePdf.FileName(document.Row.Number, id))));
        }

        var domain = shopId is null ? null : documents.FirstOrDefault()?.Row.ShopDomain ?? await DomainAsync(shopId.Value, ct).ConfigureAwait(false);
        var name = string.Join('-', new[] { "invoices", domain, year?.ToString(CultureInfo.InvariantCulture) }.Where(p => p is not null)) + ".zip";
        return new InvoiceZipPlan(files, documents.Count - files.Count, name);
    }

    private async Task<List<Document>> DocumentsAsync(Guid? shopId, TimeZoneInfo zone, CancellationToken ct)
    {
        var tenantId = db.TenantContext.RequireTenantId();
        var data = await db.ExecuteInTenantTransactionAsync(async () =>
        {
            if (shopId is { } id && !await db.Shops.AsNoTracking().AnyAsync(s => s.Id == id, ct).ConfigureAwait(false))
            {
                throw new DomainException(ProblemCodes.ShopNotFound, 404);
            }

            var invoices = await db.Invoices.AsNoTracking()
                .Where(i => shopId == null || i.ShopId == shopId)
                .Select(i => new InvoiceData(i.Id, i.ShopId, i.PaymentId, i.Kind, i.Number, i.IssuedAt, i.CreatedAt, i.Items, i.AmountNet, i.AmountGross, i.Currency,
                    i.PdfBlobKey, i.Status, i.CreditNoteFor, i.PeriodFrom, i.PeriodTo))
                .ToListAsync(ct).ConfigureAwait(false);
            var paymentIds = invoices.Where(i => i.PaymentId != null).Select(i => i.PaymentId!.Value).Distinct().ToList();
            var shopIds = invoices.Select(i => i.ShopId).Distinct().ToList();
            return new DocumentData(
                invoices,
                await db.Payments.AsNoTracking().Where(p => paymentIds.Contains(p.Id) && p.OrderId != null).Select(p => p.Id).ToListAsync(ct).ConfigureAwait(false),
                await db.Orders.AsNoTracking().Where(o => o.StripePriceAnalysis != null).Select(o => o.StripePriceAnalysis!).Distinct().ToListAsync(ct).ConfigureAwait(false),
                await db.Shops.AsNoTracking().Where(s => shopIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Domain, ct).ConfigureAwait(false));
        }, ct).ConfigureAwait(false);

        var ofOrders = data.PaymentsOfOrders.ToHashSet();
        var analysisPrices = data.AnalysisPrices.ToHashSet(StringComparer.Ordinal);
        var byId = data.Invoices.ToDictionary(i => i.Id);
        return data.Invoices.Select(invoice =>
        {
            var at = invoice.IssuedAt ?? invoice.CreatedAt;
            var source = invoice.CreditNoteFor is { } original && byId.TryGetValue(original, out var credited) ? credited : invoice;
            var (from, to) = invoice.PeriodFrom is not null || invoice.PeriodTo is not null ? (invoice.PeriodFrom, invoice.PeriodTo) : Period(invoice.Items, zone);
            var hasPdf = invoice.Status == InvoiceStatus.Issued && invoice.PdfBlobKey == InvoicePdf.Key(tenantId, invoice.Id).Value;
            return new Document(new InvoiceRowDto(
                invoice.Id,
                Kind(invoice.Kind),
                invoice.Kind switch { InvoiceKind.CreditNote => Refunded, InvoiceKind.Proforma => Issued, _ => Paid },
                invoice.ShopId,
                data.Domains.GetValueOrDefault(invoice.ShopId, string.Empty),
                IsAnalysis(source, ofOrders, analysisPrices) ? Analysis : Monitoring,
                SubscriptionChangeSql.LocalDay(at, zone),
                from,
                to,
                invoice.Number,
                invoice.AmountNet,
                invoice.AmountGross,
                invoice.Currency,
                hasPdf), at);
        }).ToList();
    }

    /// <summary>The next payment of every running subscription of the overview (none for an ending or ended one), by e-shop.</summary>
    private async Task<List<(InvoiceRowDto Row, DateTimeOffset At)>> ScheduledAsync(Guid? shopId, TimeZoneInfo zone, CancellationToken ct)
    {
        var tenantId = db.TenantContext.RequireTenantId();
        var page = await overview.GetAsync(ct).ConfigureAwait(false);
        var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantId, ct).ConfigureAwait(false);
        var currency = tenant.Currency ?? page.Currency;
        if (currency is null)
        {
            return [];
        }

        var tax = options.Value.Tax;
        var treatment = TaxTreatmentResolver.Resolve(TaxBuyer.Of(tenant), tax);
        var rows = new List<(InvoiceRowDto, DateTimeOffset)>();
        foreach (var shop in page.Shops.Where(s => s.NextPaymentAt is not null && (shopId is null || s.ShopId == shopId)))
        {
            var at = shop.NextPaymentAt!.Value;
            var day = SubscriptionChangeSql.LocalDay(at, zone);
            decimal? gross = shop.NextPaymentNet is { } net && treatment is TaxTreatment.DomesticVat or TaxTreatment.ReverseCharge
                ? TaxTreatmentResolver.Charge(treatment, net, tax).Gross
                : null;
            rows.Add((new InvoiceRowDto(null, Scheduled, Scheduled, shop.ShopId, shop.Domain, Monitoring, day, day, day.AddMonths(1).AddDays(-1), null,
                shop.NextPaymentNet, gross, currency, false), at));
        }

        return rows;
    }

    private async Task<string?> DomainAsync(Guid shopId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(
            () => db.Shops.AsNoTracking().Where(s => s.Id == shopId).Select(s => s.Domain).FirstOrDefaultAsync(ct), ct).ConfigureAwait(false);

    /// <summary>The analysis: the payment of an order, or a line with the Stripe price of an analysis (a payment not yet linked).</summary>
    private static bool IsAnalysis(InvoiceData invoice, HashSet<Guid> paymentsOfOrders, HashSet<string> analysisPrices) =>
        (invoice.PaymentId is { } payment && paymentsOfOrders.Contains(payment))
        || Lines(invoice.Items).Any(l => l.TryGetProperty("price_id", out var price) && price.ValueKind == JsonValueKind.String && analysisPrices.Contains(price.GetString()!));

    /// <summary>
    /// The period of the lines of Stripe with a length (one-off lines start and end at once): the first local day to the day before
    /// the last end. Used until the document has its own period (SuperFaktúra, group 10).
    /// </summary>
    internal static (DateOnly? From, DateOnly? To) Period(JsonDocument items, TimeZoneInfo zone)
    {
        DateTimeOffset? start = null;
        DateTimeOffset? end = null;
        foreach (var line in Lines(items))
        {
            if (Instant(line, "period_start") is { } s && Instant(line, "period_end") is { } e && e > s)
            {
                start = start is null || s < start ? s : start;
                end = end is null || e > end ? e : end;
            }
        }

        return start is { } from && end is { } to
            ? (SubscriptionChangeSql.LocalDay(from, zone), SubscriptionChangeSql.LocalDay(to, zone).AddDays(-1))
            : (null, null);
    }

    private static IEnumerable<JsonElement> Lines(JsonDocument items) =>
        items.RootElement.ValueKind == JsonValueKind.Array ? items.RootElement.EnumerateArray() : [];

    private static DateTimeOffset? Instant(JsonElement line, string name) =>
        line.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out var at) ? at : null;

    private static string Unique(HashSet<string> names, string name)
    {
        var stem = Path.GetFileNameWithoutExtension(name);
        var candidate = name;
        for (var n = 2; !names.Add(candidate); n++)
        {
            candidate = $"{stem}-{n}.pdf";
        }

        return candidate;
    }

    private static string Kind(InvoiceKind kind) => kind switch
    {
        InvoiceKind.CreditNote => "credit_note",
        InvoiceKind.Proforma => "proforma",
        _ => "invoice",
    };

    private TimeZoneInfo Zone() => TimeZoneInfo.FindSystemTimeZoneById(localization.Value.TimeZone);

    private sealed record InvoiceData(
        Guid Id, Guid ShopId, Guid? PaymentId, InvoiceKind Kind, string? Number, DateTimeOffset? IssuedAt, DateTimeOffset CreatedAt, JsonDocument Items,
        decimal AmountNet, decimal AmountGross, string Currency, string? PdfBlobKey, InvoiceStatus Status, Guid? CreditNoteFor, DateOnly? PeriodFrom, DateOnly? PeriodTo);

    private sealed record DocumentData(IReadOnlyList<InvoiceData> Invoices, IReadOnlyList<Guid> PaymentsOfOrders, IReadOnlyList<string> AnalysisPrices, Dictionary<Guid, string> Domains);

    private sealed record Document(InvoiceRowDto Row, DateTimeOffset At);
}
