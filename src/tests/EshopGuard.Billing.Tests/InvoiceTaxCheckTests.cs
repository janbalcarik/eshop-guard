using EshopGuard.Billing.Stripe;
using EshopGuard.Billing.Tax;
using EshopGuard.Data.Entities.Billing;

namespace EshopGuard.Billing.Tests;

/// <summary>The tax of a paid invoice of Stripe against the expected treatment (change 12, task 9.3, scenario „Nesoulad daně ve Stripe“).</summary>
public sealed class InvoiceTaxCheckTests
{
    [Theory]
    [InlineData(6900, 1587, 1)]
    [InlineData(6900, 1588, 1)]
    [InlineData(6900, 1586, 1)]
    [InlineData(8800, 2025, 2)]
    [InlineData(0, 0, 1)]
    public void DomesticVat_TheDomesticRate_WithinOneCentPerLine(long net, long tax, int lines)
    {
        Assert.Null(InvoiceTaxCheck.Check(TaxTreatment.DomesticVat, Invoice(net, tax, lines: lines), BillingOptionsTests.Tax()));
    }

    [Fact]
    public void DomesticVat_AnotherRate_IsAMismatch()
    {
        var mismatch = InvoiceTaxCheck.Check(TaxTreatment.DomesticVat, Invoice(6900, 1380), BillingOptionsTests.Tax());

        Assert.Equal(new InvoiceTaxMismatch("domestic_vat", "domestic_vat", 1380, 1587), mismatch);
    }

    [Theory]
    [InlineData(1589, 1)]
    [InlineData(1590, 2)]
    public void DomesticVat_MoreThanTheRounding_IsAMismatch(long tax, int lines)
    {
        Assert.NotNull(InvoiceTaxCheck.Check(TaxTreatment.DomesticVat, Invoice(6900, tax, lines: lines), BillingOptionsTests.Tax()));
    }

    [Theory]
    [InlineData(true, null, "reverse_charge")]
    [InlineData(false, "reverse", "reverse_charge")]
    [InlineData(false, "exempt", "exempt")]
    [InlineData(false, null, "no_tax")]
    public void DomesticVat_WithoutTheTax_IsAMismatch(bool reverse, string? exempt, string actual)
    {
        var mismatch = InvoiceTaxCheck.Check(TaxTreatment.DomesticVat, Invoice(6900, 0, reverse, exempt), BillingOptionsTests.Tax());

        Assert.Equal(new InvoiceTaxMismatch("domestic_vat", actual, 0, 1587), mismatch);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "reverse")]
    public void ReverseCharge_NoTax_AndTheReverseChargeOnTheInvoiceOrTheCustomer(bool reverse, string? exempt)
    {
        Assert.Null(InvoiceTaxCheck.Check(TaxTreatment.ReverseCharge, Invoice(6900, 0, reverse, exempt), BillingOptionsTests.Tax()));
    }

    [Theory]
    [InlineData(1587, true, null, "reverse_charge")]
    [InlineData(1587, false, null, "domestic_vat")]
    [InlineData(0, false, null, "no_tax")]
    [InlineData(0, false, "exempt", "exempt")]
    public void ReverseCharge_WithTaxOrWithoutTheReverseCharge_IsAMismatch(long tax, bool reverse, string? exempt, string actual)
    {
        var mismatch = InvoiceTaxCheck.Check(TaxTreatment.ReverseCharge, Invoice(6900, tax, reverse, exempt), BillingOptionsTests.Tax());

        Assert.Equal(new InvoiceTaxMismatch("reverse_charge", actual, tax, 0), mismatch);
    }

    [Theory]
    [InlineData(TaxTreatment.PendingVerification, "pending_verification")]
    [InlineData(TaxTreatment.Undetermined, "undetermined")]
    public void NotPayableTreatments_AreAlwaysAMismatch(TaxTreatment expected, string text)
    {
        Assert.Equal(new InvoiceTaxMismatch(text, "domestic_vat", 1587, null), InvoiceTaxCheck.Check(expected, Invoice(6900, 1587), BillingOptionsTests.Tax()));
        Assert.Equal(new InvoiceTaxMismatch(text, "reverse_charge", 0, null), InvoiceTaxCheck.Check(expected, Invoice(6900, 0, true), BillingOptionsTests.Tax()));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(6900, 1587)]
    [InlineData(1900, 437)]
    public void DomesticTax_RoundsHalfAwayFromZero(long net, long tax)
    {
        Assert.Equal(tax, InvoiceTaxCheck.DomesticTax(net, 23m));
    }

    private static StripeInvoiceState Invoice(long net, long tax, bool reverse = false, string? exempt = null, int lines = 1)
    {
        var now = new DateTimeOffset(2027, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var items = Enumerable.Range(0, lines)
            .Select(i => new StripeInvoiceLine($"il_{i}", null, net / lines, 1, "price_t2000", now, now.AddMonths(1), 0, tax / lines))
            .ToList();
        return new StripeInvoiceState("in_tax", "cus_tax", "sub_tax", "paid", "eur", net + tax, net, net + tax, net, tax, reverse, exempt, "subscription_cycle", now,
            now, "pi_tax", items, new Dictionary<string, string>());
    }
}
