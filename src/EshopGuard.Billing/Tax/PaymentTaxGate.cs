using EshopGuard.Application.Problems;
using EshopGuard.Billing.Stripe;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Entities.Iam;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EshopGuard.Billing.Tax;

/// <summary>The tenant before a payment and its payable tax treatment.</summary>
public sealed record PaymentTax(Tenant Tenant, TaxTreatment Treatment);

/// <summary>
/// The tax treatment before Checkout, a payment with the saved card or monitoring started again (requirement „Daňový režim
/// odběratele“, task 9.2). A VAT id of another state of the EU not yet sent to Stripe (status <c>none</c>) goes to the customer of
/// Stripe first (<see cref="StripeCustomers"/>), so its verification in VIES starts; the payment then waits for it with 409
/// <c>billing.tax_id_pending</c> until <c>customer.tax_id.updated</c> brings <c>verified</c>. An undetermined treatment is 422.
/// </summary>
public sealed class PaymentTaxGate(EshopGuardDb db, StripeCustomers customers, IOptions<BillingOptions> options)
{
    public async Task<PaymentTax> RequireAsync(Guid tenantId, CancellationToken ct)
    {
        var tenant = await TenantAsync(tenantId, ct).ConfigureAwait(false);
        var treatment = TaxTreatmentResolver.Resolve(TaxBuyer.Of(tenant), options.Value.Tax);
        if (treatment == TaxTreatment.PendingVerification && tenant.TaxIdStatus == TaxIdStatus.None)
        {
            await customers.EnsureAsync(tenantId, ct).ConfigureAwait(false);
            tenant = await TenantAsync(tenantId, ct).ConfigureAwait(false);
            treatment = TaxTreatmentResolver.Resolve(TaxBuyer.Of(tenant), options.Value.Tax);
        }

        if (TaxTreatmentResolver.RefusalCode(treatment) is { } refusal)
        {
            throw new DomainException(refusal, treatment == TaxTreatment.PendingVerification ? 409 : 422);
        }

        return new PaymentTax(tenant, treatment);
    }

    private Task<Tenant> TenantAsync(Guid tenantId, CancellationToken ct) => db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantId, ct);
}
