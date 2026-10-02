using System.Text.Json.Nodes;
using EshopGuard.Application.Options;
using EshopGuard.Application.Problems;
using EshopGuard.Billing.Contracts;
using EshopGuard.Billing.Pricing;
using EshopGuard.Billing.Stripe;
using EshopGuard.Billing.Tax;
using EshopGuard.Billing.Texts;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EshopGuard.Billing.Orders;

/// <summary>
/// Stripe Checkout of an order (requirement „Platba úvodní analýzy a sledování přes Stripe Checkout“, task 5.3): the mode
/// <c>subscription</c> with the one-off analysis and the monitoring with a trial to <c>Billing:TrialMode</c>, the metadata of the
/// order, the customer of the tenant, automatic tax, the language of the tenant, the sentence about the first monthly payment
/// and canceling, the coupon of the volume discount and <c>expires_at</c>. The prices are those of the snapshot of the order,
/// never a lookup key. An open session that has not expired is used again; a new one has the key
/// <c>checkout:{orderId}:{attempt}</c>. Before the session the tax treatment must allow the payment (409/422).
/// </summary>
public sealed class CheckoutService(
    EshopGuardDb db,
    IStripeGateway stripe,
    StripeCustomers customers,
    IOptions<BillingOptions> options,
    IOptions<FrontendOptions> frontend,
    IOptions<LocalizationOptions> localization,
    TimeProvider time,
    ILogger<CheckoutService> logger)
{
    public async Task<CheckoutDto> CreateSessionAsync(Guid userId, Guid orderId, CancellationToken ct)
    {
        var tenantId = db.TenantContext.RequireTenantId();
        if (!options.Value.StripeEnabled)
        {
            throw new BillingUnavailableException();
        }

        var order = await db.ExecuteInTenantTransactionAsync(() => db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct), ct).ConfigureAwait(false)
            ?? throw new DomainException(BillingCodes.OrderNotFound, 404);
        if (order.Status is not (OrderStatus.Created or OrderStatus.CheckoutOpen))
        {
            throw new DomainException(BillingCodes.OrderNotOpen, 409, new Dictionary<string, object?> { ["status"] = order.Status.ToString() });
        }

        if (order.StripePriceAnalysis is null || order.StripePriceMonitoring is null)
        {
            throw new BillingUnavailableException();
        }

        var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantId, ct).ConfigureAwait(false);
        var treatment = TaxTreatmentResolver.Resolve(TaxBuyer.Of(tenant), options.Value.Tax);
        if (TaxTreatmentResolver.RefusalCode(treatment) is { } refusal)
        {
            throw new DomainException(refusal, treatment == TaxTreatment.PendingVerification ? 409 : 422);
        }

        var now = time.GetUtcNow();
        if (order is { Status: OrderStatus.CheckoutOpen, StripeCheckoutSessionId: { } sessionId, CheckoutExpiresAt: { } expires } && expires > now.AddMinutes(1))
        {
            var existing = await stripe.GetCheckoutSessionAsync(sessionId, ct).ConfigureAwait(false);
            if (existing is { Status: "open", Url: { } url })
            {
                return new CheckoutDto(order.Id, url, existing.ExpiresAt);
            }
        }

        var customerId = await customers.EnsureAsync(tenantId, ct).ConfigureAwait(false);
        var attempt = order.CheckoutAttempt + 1;
        var trialEnd = BillingPeriods.TrialEnd(now, options.Value.TrialMode);
        var expiresAt = now.AddMinutes(options.Value.CheckoutExpiresMinutes);
        var monthly = PriceQuoteService.DiscountedMonthly(order.MonitoringMonthly, order.MonitoringDiscountPercent) ?? 0m;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(localization.Value.TimeZone);
        var message = BillingTexts.Format(tenant.Locale, "checkout.submit_message",
            ("monthly", BillingTexts.Money(monthly, order.Currency, tenant.Locale)), ("date", BillingTexts.Date(trialEnd, zone)));
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tenant_id"] = tenantId.ToString("D"),
            ["shop_id"] = order.ShopId.ToString("D"),
            ["order_id"] = order.Id.ToString("D"),
        };
        var session = await stripe.CreateCheckoutSessionAsync(
            new StripeCheckoutRequest(customerId, order.StripePriceAnalysis, order.StripePriceMonitoring, order.StripeCouponId, trialEnd, Locale(tenant.Locale), message,
                Url(options.Value.CheckoutSuccessPath, tenantId, order), Url(options.Value.CheckoutCancelPath, tenantId, order), expiresAt, metadata),
            $"checkout:{order.Id:N}:{attempt}", ct).ConfigureAwait(false);

        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            await db.Orders.Where(o => o.Id == order.Id && (o.Status == OrderStatus.Created || o.Status == OrderStatus.CheckoutOpen))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, OrderStatus.CheckoutOpen)
                    .SetProperty(o => o.StripeCheckoutSessionId, session.Id)
                    .SetProperty(o => o.CheckoutExpiresAt, session.ExpiresAt)
                    .SetProperty(o => o.CheckoutAttempt, attempt)
                    .SetProperty(o => o.TrialEndPlanned, trialEnd)
                    .SetProperty(o => o.UpdatedAt, now), ct).ConfigureAwait(false);
            await BillingSql.AuditAsync((NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction(), tenantId, userId, BillingSql.User, "order.checkout_started", "order",
                order.Id.ToString("D"), new JsonObject { ["attempt"] = attempt }, now, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
        logger.LogInformation("order.checkout {OrderId} {TenantId} {Attempt}", order.Id, tenantId, attempt);
        return new CheckoutDto(order.Id, session.Url ?? throw new BillingUnavailableException(), session.ExpiresAt);
    }

    /// <summary>The language of Checkout from the locale of the tenant (<c>sk</c>, <c>cs</c>; Stripe knows both).</summary>
    public static string Locale(string locale) => (locale ?? "sk").Split('-')[0].ToLowerInvariant();

    private string Url(string path, Guid tenantId, Order order)
    {
        var origin = string.IsNullOrEmpty(options.Value.PublicAppUrl) ? frontend.Value.BaseUrl : options.Value.PublicAppUrl;
        return origin + path
            .Replace("{tenantId}", tenantId.ToString("D"), StringComparison.Ordinal)
            .Replace("{shopId}", order.ShopId.ToString("D"), StringComparison.Ordinal)
            .Replace("{orderId}", order.Id.ToString("D"), StringComparison.Ordinal);
    }
}
