using System.Globalization;
using EshopGuard.Application.Options;
using EshopGuard.Application.Problems;
using EshopGuard.Billing.Contracts;
using EshopGuard.Billing.Stripe;
using EshopGuard.Data;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EshopGuard.Billing.Subscriptions;

/// <summary>
/// The change of the card of the account (task 7.4): the customer portal of Stripe with the flow <c>payment_method_update</c>
/// (its configuration allows only the card, see <c>Billing:Stripe:PortalConfigurationId</c>) or, as the fallback, a SetupIntent
/// for the Payment Element in the application. The answers carry only the URL or the client secret, never an id of Stripe; the
/// new card reaches every running subscription through the webhook (<see cref="AccountCardService"/>).
/// </summary>
public sealed class CardSessionService(
    EshopGuardDb db,
    IStripeGateway stripe,
    StripeCustomers customers,
    IOptions<BillingOptions> options,
    IOptions<FrontendOptions> frontend,
    TimeProvider time)
{
    public async Task<CardPortalDto> PortalAsync(CancellationToken ct)
    {
        var tenantId = db.TenantContext.RequireTenantId();
        if (!options.Value.StripeEnabled)
        {
            throw new BillingUnavailableException();
        }

        var customerId = await db.Tenants.AsNoTracking().Where(t => t.Id == tenantId).Select(t => t.StripeCustomerId).FirstOrDefaultAsync(ct).ConfigureAwait(false)
            ?? throw new DomainException(BillingCodes.SavedCardMissing, 409);
        var origin = string.IsNullOrEmpty(options.Value.PublicAppUrl) ? frontend.Value.BaseUrl : options.Value.PublicAppUrl;
        var returnUrl = origin + options.Value.PortalReturnPath.Replace("{tenantId}", tenantId.ToString("D"), StringComparison.Ordinal);
        var url = await stripe.CreatePortalSessionAsync(customerId, returnUrl, $"portal:{tenantId:N}:{Minute()}", ct).ConfigureAwait(false);
        return new CardPortalDto(url);
    }

    public async Task<CardSetupIntentDto> SetupIntentAsync(CancellationToken ct)
    {
        var tenantId = db.TenantContext.RequireTenantId();
        if (!options.Value.StripeEnabled)
        {
            throw new BillingUnavailableException();
        }

        var customerId = await customers.EnsureAsync(tenantId, ct).ConfigureAwait(false);
        var secret = await stripe.CreateSetupIntentAsync(customerId, $"setup-intent:{tenantId:N}:{Minute()}", ct).ConfigureAwait(false);
        return new CardSetupIntentDto(secret);
    }

    /// <summary>A repeated click within the same minute gets the same session of Stripe (the key of idempotence).</summary>
    private string Minute() => time.GetUtcNow().UtcDateTime.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
}
