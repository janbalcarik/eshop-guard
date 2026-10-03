using System.Text;
using System.Text.Json;
using EshopGuard.Billing.Invoicing;
using EshopGuard.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Api.Tests.Billing;

/// <summary>Documents of <c>billing.invoices</c> as the issuing stores them (group 10 not yet), with their PDF in the store of the factory.</summary>
internal static class InvoiceSeed
{
    /// <summary>08:00 UTC of the day (the same day in Bratislava).</summary>
    public static DateTimeOffset At(int year, int month, int day) => new(year, month, day, 8, 0, 0, TimeSpan.Zero);

    /// <summary>A small PDF of the document, different for every number.</summary>
    public static byte[] PdfOf(string number) => Encoding.ASCII.GetBytes($"%PDF-1.7\n% {number}\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n%%EOF\n");

    /// <summary>The lines of a Stripe invoice: price and period of each.</summary>
    public static string Lines(params (string Price, DateTimeOffset Start, DateTimeOffset End)[] lines) =>
        JsonSerializer.Serialize(lines.Select(l => new Dictionary<string, object> { ["price_id"] = l.Price, ["period_start"] = l.Start, ["period_end"] = l.End }));

    /// <summary>One month of the monitoring from <paramref name="start"/>.</summary>
    public static string Monthly(DateTimeOffset start) => Lines(("price_monthly", start, start.AddMonths(1)));

    /// <summary>
    /// A document of the tenant (23 % VAT); an issued one has the key of its PDF and, with <paramref name="file"/>, the file in the
    /// store. Any other state has no key and no file.
    /// </summary>
    public static async Task<Guid> DocumentAsync(
        ApiFactory factory, Guid tenantId, Guid shopId, string? number, DateTimeOffset at, decimal net, string kind = "invoice", string status = "issued",
        Guid? paymentId = null, Guid? creditNoteFor = null, string items = "[]", bool file = true)
    {
        var id = Guid.CreateVersion7();
        var issued = status == "issued";
        var key = InvoicePdf.Key(tenantId, id);
        var vat = Math.Round(net * 0.23m, 2, MidpointRounding.AwayFromZero);
        await ApiTestBase.AdminAsync(
            """
            INSERT INTO billing.invoices (id, tenant_id, shop_id, payment_id, kind, buyer, items, amount_net, vat_amount, amount_gross, currency, status, number,
                issued_at, pdf_blob_key, credit_note_for, source_key, source_kind, einvoice_status, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, '{}'::jsonb, $6::jsonb, $7, $8, $9, 'EUR', $10, $11, $12, $13, $14, $15, 'stripe_invoice', 'not_required', $16, $16)
            """,
            id, tenantId, shopId, paymentId, kind, items, net, vat, net + vat, status, number, issued ? at : null, issued ? key.Value : null, creditNoteFor,
            "in_" + id.ToString("N"), at);
        if (issued && file)
        {
            await using var content = new MemoryStream(PdfOf(number ?? id.ToString("N")));
            await factory.Services.GetRequiredService<IBlobStore>().PutAsync(key, content, "application/pdf", TestContext.Current.CancellationToken);
        }

        return id;
    }
}
