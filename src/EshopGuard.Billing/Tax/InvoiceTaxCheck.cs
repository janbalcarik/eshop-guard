using EshopGuard.Billing.Stripe;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Billing;

namespace EshopGuard.Billing.Tax;

/// <summary>A paid invoice of Stripe that does not match the tax treatment of the buyer: codes and amounts in minor units.</summary>
public sealed record InvoiceTaxMismatch(string Expected, string Actual, long TaxAmount, long? ExpectedTaxAmount);

/// <summary>
/// The tax of a paid invoice of Stripe against the expected treatment (design „Kontrola po zaplacení“, task 9.3). Before an invoice
/// is issued, the tax Stripe charged must match:
/// <list type="bullet">
/// <item><see cref="TaxTreatment.DomesticVat"/>: no reverse charge, the customer not exempt, and the tax of the amount without tax at
/// the domestic rate (<c>Billing:Tax:DomesticVatRate</c>), within one cent per line (Stripe Tax rounds each line);</item>
/// <item><see cref="TaxTreatment.ReverseCharge"/>: no tax, and the reverse charge on the invoice or the customer;</item>
/// <item><see cref="TaxTreatment.PendingVerification"/>, <see cref="TaxTreatment.Undetermined"/>: never payable, always a mismatch.</item>
/// </list>
/// A changed domestic rate stops the invoices until the setting follows it (fail-closed).
/// </summary>
public static class InvoiceTaxCheck
{
    public const string MismatchReason = "tax_mismatch";

    public static InvoiceTaxMismatch? Check(TaxTreatment expected, StripeInvoiceState invoice, BillingOptions.TaxSettings tax)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(tax);
        var actual = Actual(invoice);
        var expectedText = SnakeCaseEnumConverter<TaxTreatment>.ToText(expected);
        switch (expected)
        {
            case TaxTreatment.DomesticVat:
                var due = DomesticTax(invoice.TotalExcludingTax, tax.DomesticVatRate);
                var tolerance = Math.Max(1, invoice.Lines.Count);
                return actual is "domestic_vat" or "no_tax" && Math.Abs(invoice.TaxAmount - due) <= tolerance
                    ? null
                    : new InvoiceTaxMismatch(expectedText, actual, invoice.TaxAmount, due);
            case TaxTreatment.ReverseCharge:
                return actual == "reverse_charge" && invoice.TaxAmount == 0 ? null : new InvoiceTaxMismatch(expectedText, actual, invoice.TaxAmount, 0);
            default:
                return new InvoiceTaxMismatch(expectedText, actual, invoice.TaxAmount, null);
        }
    }

    /// <summary>
    /// What the invoice of Stripe says: <c>reverse_charge</c> (a tax line with that reason, or the customer marked <c>reverse</c>),
    /// <c>exempt</c>, <c>domestic_vat</c> (some tax), else <c>no_tax</c>.
    /// </summary>
    public static string Actual(StripeInvoiceState invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        return invoice switch
        {
            { ReverseCharge: true } or { CustomerTaxExempt: "reverse" } => "reverse_charge",
            { CustomerTaxExempt: "exempt" } => "exempt",
            { TaxAmount: > 0 } => "domestic_vat",
            _ => "no_tax",
        };
    }

    /// <summary>The domestic tax of an amount without tax in minor units, rounded half away from zero.</summary>
    public static long DomesticTax(long amountExcludingTax, decimal rate) =>
        (long)Math.Round(amountExcludingTax * rate / 100m, 0, MidpointRounding.AwayFromZero);
}
