using System.Globalization;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Iam;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EshopGuard.Billing.Stripe;

/// <summary>
/// The customer of Stripe of a tenant (task 5.2): one per tenant (key <c>customer:{tenantId}</c>), with the name, the billing
/// address, the language and the e-mail of the tenant. The VAT id goes to Stripe from the data of the tenant, never from
/// Checkout (AD 9), and its verification starts as <c>pending</c>. A change of the data of the tenant updates the customer.
/// </summary>
public sealed class StripeCustomers(EshopGuardDb db, IStripeGateway stripe, IOptions<BillingOptions> options)
{
    /// <summary>The id of the customer of the tenant, created when missing; in the open transaction of the tenant or without one.</summary>
    public async Task<string> EnsureAsync(Guid tenantId, CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantId, ct).ConfigureAwait(false);
        var request = Request(tenant);
        var customerId = tenant.StripeCustomerId;
        if (customerId is null)
        {
            customerId = await stripe.CreateCustomerAsync(request, $"customer:{tenantId:N}", ct).ConfigureAwait(false);
            await db.Tenants.Where(t => t.Id == tenantId && t.StripeCustomerId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.StripeCustomerId, customerId), ct).ConfigureAwait(false);
        }
        else
        {
            await stripe.UpdateCustomerAsync(customerId, request,
                $"customer-update:{tenantId:N}:{tenant.UpdatedAt.UtcTicks.ToString(CultureInfo.InvariantCulture)}", ct).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(tenant.IcDph) && tenant.TaxIdStatus == TaxIdStatus.None)
        {
            var value = Normalize(tenant.IcDph);

            // The version of the row is in the key: a VAT id changed back to an earlier value (whose tax id was deleted) is sent again.
            var taxId = await stripe.CreateTaxIdAsync(customerId, "eu_vat", value,
                $"taxid:{tenantId:N}:{value}:{tenant.Version.ToString(CultureInfo.InvariantCulture)}", ct).ConfigureAwait(false);
            var status = taxId.VerificationStatus switch
            {
                "verified" => TaxIdStatus.Verified,
                "unverified" => TaxIdStatus.Unverified,
                _ => TaxIdStatus.Pending,
            };
            await db.Tenants.Where(t => t.Id == tenantId && t.TaxIdStatus == TaxIdStatus.None)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.TaxIdStatus, status), ct).ConfigureAwait(false);
        }

        return customerId;
    }

    /// <summary>
    /// Removes every tax id of the customer (the tenant changed or removed its VAT id; Stripe Tax would keep applying the old
    /// one). A tax id already gone is no error, so a retry is safe.
    /// </summary>
    public async Task RemoveTaxIdsAsync(string customerId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(customerId);
        var customer = await stripe.GetCustomerAsync(customerId, ct).ConfigureAwait(false);
        foreach (var taxId in customer.TaxIds)
        {
            await stripe.DeleteTaxIdAsync(customerId, taxId.Id, ct).ConfigureAwait(false);
        }
    }

    /// <summary>A VAT id as Stripe and VIES take it: without spaces, in capitals.</summary>
    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
    }

    private StripeCustomerRequest Request(Tenant tenant) => new(
        tenant.LegalName ?? tenant.Name,
        tenant.BillingEmail,
        new StripeAddress(tenant.Street, tenant.City, tenant.PostalCode, tenant.CountryCode.ToUpperInvariant()),
        tenant.Locale,
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tenant_id"] = tenant.Id.ToString("D"),
            ["mode"] = options.Value.Stripe.Mode,
        });
}
