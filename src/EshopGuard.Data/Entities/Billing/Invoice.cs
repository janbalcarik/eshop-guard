using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Billing;

/// <summary>Invoice, credit note or pro forma (issued through SuperFaktúra). Table <c>billing.invoices</c>.</summary>
public sealed class Invoice : TenantEntity
{
    public Guid ShopId { get; set; }

    public Guid? PaymentId { get; set; }

    public InvoiceKind Kind { get; set; }

    public string? Number { get; set; }

    public string? SuperfakturaId { get; set; }

    public DateTimeOffset? IssuedAt { get; set; }

    public DateOnly? TaxableSupplyDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public required JsonDocument Buyer { get; set; }

    public required JsonDocument Items { get; set; }

    public decimal AmountNet { get; set; }

    public decimal VatAmount { get; set; }

    public decimal AmountGross { get; set; }

    public required string Currency { get; set; }

    public bool ReverseCharge { get; set; }

    public bool EinvoiceRequired { get; set; }

    public EinvoiceStatus EinvoiceStatus { get; set; }

    public string? EinvoiceMessageId { get; set; }

    public string? PdfBlobKey { get; set; }

    public InvoiceStatus Status { get; set; }

    public Guid? CreditNoteFor { get; set; }

    /// <summary>The id of the paid invoice or of the refund of Stripe (unique: one invoice per payment, AD 8).</summary>
    public string? SourceKey { get; set; }

    public InvoiceSourceKind? SourceKind { get; set; }

    public DateOnly? PeriodFrom { get; set; }

    public DateOnly? PeriodTo { get; set; }

    /// <summary>Code (and HTTP status) why the invoice waits for a person, e.g. <c>tax_mismatch</c>; never a text of SuperFaktúra.</summary>
    public string? NeedsReviewReason { get; set; }

    public InvoiceEmailStatus EmailStatus { get; set; }
}
