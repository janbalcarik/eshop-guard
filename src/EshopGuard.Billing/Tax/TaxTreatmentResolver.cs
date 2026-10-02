using System.Globalization;
using System.Text.Json.Nodes;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Configurations.Conventions;

namespace EshopGuard.Billing.Tax;

/// <summary>The buyer for the tax treatment: the country of the billing address, the company id, the VAT id and its verification.</summary>
public sealed record TaxBuyer(string CountryCode, string? Ico, string? IcDph, TaxIdStatus TaxIdStatus)
{
    public static TaxBuyer Of(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return new TaxBuyer(tenant.CountryCode, tenant.Ico, tenant.IcDph, tenant.TaxIdStatus);
    }
}

/// <summary>
/// The tax treatment of a buyer (design „Daňový režim“, task 9.1). The supplier is a Slovak company paying VAT:
/// <list type="bullet">
/// <item>seat in the country of the supplier (with or without a VAT id): <see cref="TaxTreatment.DomesticVat"/> (the rate comes from Stripe Tax);</item>
/// <item>another state of the EU VAT area with a VAT id verified in VIES: <see cref="TaxTreatment.ReverseCharge"/>;</item>
/// <item>the same with the verification running: <see cref="TaxTreatment.PendingVerification"/>;</item>
/// <item>unverified, without a VAT id, or outside the EU: <see cref="TaxTreatment.Undetermined"/> until the accountant decides (K rozhodnutí 3).</item>
/// </list>
/// The countries are data (<c>Billing:Tax</c>), not code.
/// </summary>
public static class TaxTreatmentResolver
{
    public static TaxTreatment Resolve(TaxBuyer buyer, BillingOptions.TaxSettings tax)
    {
        ArgumentNullException.ThrowIfNull(buyer);
        ArgumentNullException.ThrowIfNull(tax);
        var country = buyer.CountryCode.Trim().ToUpperInvariant();
        if (string.Equals(country, tax.SupplierCountry, StringComparison.OrdinalIgnoreCase))
        {
            return TaxTreatment.DomesticVat;
        }

        if (!tax.EuVatCountries.Contains(country, StringComparer.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(buyer.IcDph))
        {
            return TaxTreatment.Undetermined;
        }

        return buyer.TaxIdStatus switch
        {
            TaxIdStatus.Verified => TaxTreatment.ReverseCharge,
            TaxIdStatus.Pending => TaxTreatment.PendingVerification,
            _ => TaxTreatment.Undetermined,
        };
    }

    /// <summary>The code that refuses a payment in this treatment, or null when it may be paid.</summary>
    public static string? RefusalCode(TaxTreatment treatment) => treatment switch
    {
        TaxTreatment.PendingVerification => BillingCodes.TaxIdPending,
        TaxTreatment.Undetermined => BillingCodes.TaxTreatmentUndetermined,
        _ => null,
    };

    /// <summary>
    /// The preview of VAT for a quote (<c>vat_preview</c>, K rozhodnutí 12): the treatment, and for the domestic VAT the rate and
    /// the amounts with VAT (rounded half away from zero to cents). Codes and numbers only; the frontend writes the sentence.
    /// </summary>
    public static JsonObject Preview(TaxTreatment treatment, decimal? analysisNet, decimal? monitoringNet, BillingOptions.TaxSettings tax)
    {
        ArgumentNullException.ThrowIfNull(tax);
        var preview = new JsonObject { ["treatment"] = SnakeCaseEnumConverter<TaxTreatment>.ToText(treatment) };
        if (treatment == TaxTreatment.DomesticVat)
        {
            preview["rate"] = tax.DomesticVatRate;
            if (analysisNet is { } analysis)
            {
                preview["analysisVat"] = Vat(analysis, tax.DomesticVatRate);
                preview["analysisGross"] = analysis + Vat(analysis, tax.DomesticVatRate);
            }

            if (monitoringNet is { } monitoring)
            {
                preview["monitoringVat"] = Vat(monitoring, tax.DomesticVatRate);
                preview["monitoringGross"] = monitoring + Vat(monitoring, tax.DomesticVatRate);
            }
        }

        return preview;
    }

    public static decimal Vat(decimal net, decimal rate) => Math.Round(net * rate / 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>The rate as text for logs and snapshots (<c>23</c>, <c>0</c>).</summary>
    public static string Text(decimal rate) => rate.ToString("0.##", CultureInfo.InvariantCulture);
}
