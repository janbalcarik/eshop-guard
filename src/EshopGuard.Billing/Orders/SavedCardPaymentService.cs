using System.Text.Json.Nodes;
using EshopGuard.Application.Problems;
using EshopGuard.Billing.Contracts;
using EshopGuard.Billing.Jobs;
using EshopGuard.Billing.Pricing;
using EshopGuard.Billing.Stripe;
using EshopGuard.Billing.Subscriptions;
using EshopGuard.Billing.Tax;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Queue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EshopGuard.Billing.Orders;

/// <summary>The answer of a payment with the card of the account (and of monitoring started again).</summary>
public static class SavedCardStatus
{
    /// <summary>Stripe runs the subscription; the webhook confirms the payment.</summary>
    public const string Processing = "processing";

    /// <summary>The bank asks for 3-D Secure: the client secret goes to the Payment Element.</summary>
    public const string RequiresAction = "requires_action";

    /// <summary>The first payment was refused without a step of the customer; the order stays open for Checkout.</summary>
    public const string Failed = "failed";
}

/// <summary>
/// The payment of an order with the card of the account (requirement „Jedna platební karta na účet“, task 7.3): no Checkout, the
/// API creates the subscription of the e-shop with the one-off analysis (<c>add_invoice_items</c>), the trial and
/// <c>payment_behavior = default_incomplete</c>. Running at once, the webhook pays the order (<c>processing</c>); waiting for the
/// bank, the answer carries the <c>client_secret</c> of 3-D Secure (<c>requires_action</c>). The job <c>billing.expire_order</c>
/// settles it a minute later and at <c>checkout_expires_at</c> cancels a subscription still <c>incomplete</c> and expires the order.
/// An open Checkout session of the order is expired first, so the order is never paid twice.
/// </summary>
public sealed class SavedCardPaymentService(
    EshopGuardDb db,
    IStripeGateway stripe,
    StripeCustomers customers,
    IJobQueue queue,
    IOptions<BillingOptions> options,
    TimeProvider time,
    ILogger<SavedCardPaymentService> logger)
{
    public async Task<SavedCardPaymentDto> PayAsync(Guid userId, Guid orderId, CancellationToken ct)
    {
        var tenantId = db.TenantContext.RequireTenantId();
        if (!options.Value.StripeEnabled)
        {
            throw new BillingUnavailableException();
        }

        var (order, card) = await db.ExecuteInTenantTransactionAsync(async () => (
            await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct).ConfigureAwait(false),
            await db.PaymentMethods.AsNoTracking().FirstOrDefaultAsync(p => p.IsDefault && p.DetachedAt == null, ct).ConfigureAwait(false)), ct).ConfigureAwait(false);
        if (order is null)
        {
            throw new DomainException(BillingCodes.OrderNotFound, 404);
        }

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

        if (card is null || tenant.StripeCustomerId is null)
        {
            throw new DomainException(BillingCodes.SavedCardMissing, 409);
        }

        var now = time.GetUtcNow();
        if (order is { Status: OrderStatus.CheckoutOpen, StripeSubscriptionId: { } pendingId })
        {
            var pending = await stripe.GetSubscriptionAsync(pendingId, ct).ConfigureAwait(false);
            if (pending.Status is "trialing" or "active")
            {
                return new SavedCardPaymentDto(order.Id, SavedCardStatus.Processing, null, order.CheckoutExpiresAt ?? now);
            }

            if (pending is { Status: "incomplete", PaymentClientSecret: { } secret } && order.CheckoutExpiresAt is { } expires && expires > now.AddMinutes(1))
            {
                return new SavedCardPaymentDto(order.Id, SavedCardStatus.RequiresAction, secret, expires);
            }

            if (pending.Status == "incomplete")
            {
                await stripe.CancelSubscriptionAsync(pendingId, $"saved-card-cancel:{order.Id:N}:{order.CheckoutAttempt}", ct).ConfigureAwait(false);
            }
        }

        if (order is { Status: OrderStatus.CheckoutOpen, StripeCheckoutSessionId: { } sessionId })
        {
            await CloseCheckoutAsync(order, sessionId, now, ct).ConfigureAwait(false);
        }

        var customerId = await customers.EnsureAsync(tenantId, ct).ConfigureAwait(false);
        var attempt = order.CheckoutAttempt + 1;
        var trialEnd = BillingPeriods.TrialEnd(now, options.Value.TrialMode);
        var expiresAt = now.AddMinutes(options.Value.CheckoutExpiresMinutes);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tenant_id"] = tenantId.ToString("D"),
            ["shop_id"] = order.ShopId.ToString("D"),
            ["order_id"] = order.Id.ToString("D"),
        };
        var subscription = await stripe.CreateSubscriptionAsync(
            new StripeSubscriptionRequest(customerId, order.StripePriceMonitoring, order.StripePriceAnalysis, order.StripeCouponId, trialEnd, card.StripePaymentMethodId, metadata),
            $"saved-card:{order.Id:N}:{attempt}", ct).ConfigureAwait(false);

        var started = await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var transaction = (NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction();
            if (order.StripeSubscriptionId is { } previous)
            {
                await PendingSubscriptions.MarkNotStartedAsync(transaction, previous, now, ct).ConfigureAwait(false);
            }

            var updated = await db.Orders
                .Where(o => o.Id == order.Id && o.CheckoutAttempt == order.CheckoutAttempt && (o.Status == OrderStatus.Created || o.Status == OrderStatus.CheckoutOpen))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, OrderStatus.CheckoutOpen)
                    .SetProperty(o => o.StripeSubscriptionId, subscription.Id)
                    .SetProperty(o => o.StripeCheckoutSessionId, (string?)null)
                    .SetProperty(o => o.CheckoutExpiresAt, expiresAt)
                    .SetProperty(o => o.CheckoutAttempt, attempt)
                    .SetProperty(o => o.TrialEndPlanned, trialEnd)
                    .SetProperty(o => o.UpdatedAt, now), ct).ConfigureAwait(false);
            if (updated == 0)
            {
                return false;
            }

            await BillingSql.AuditAsync(transaction, tenantId, userId, BillingSql.User, "order.saved_card_payment_started", "order", order.Id.ToString("D"),
                new JsonObject { ["attempt"] = attempt, ["status"] = subscription.Status, ["last4"] = card.Last4 }, now, ct).ConfigureAwait(false);
            await queue.EnqueueAsync(BillingJobs.ExpireOrder(tenantId, order.ShopId, order.Id, attempt, "settle", now.AddMinutes(1)), transaction, ct).ConfigureAwait(false);
            await queue.EnqueueAsync(BillingJobs.ExpireOrder(tenantId, order.ShopId, order.Id, attempt, "expire", expiresAt), transaction, ct).ConfigureAwait(false);
            return true;
        }, ct).ConfigureAwait(false);

        if (!started)
        {
            // Another tab paid or started the order meanwhile: the new subscription must not run beside it.
            if (subscription.Status == "incomplete")
            {
                await stripe.CancelSubscriptionAsync(subscription.Id, $"saved-card-cancel:{order.Id:N}:{attempt}", ct).ConfigureAwait(false);
            }

            logger.LogWarning("order.saved_card_conflict {OrderId} {TenantId} {Attempt} {Status}", order.Id, tenantId, attempt, subscription.Status);
            throw new DomainException(BillingCodes.OrderNotOpen, 409);
        }

        logger.LogInformation("order.saved_card {OrderId} {TenantId} {Attempt} {Status}", order.Id, tenantId, attempt, subscription.Status);
        return subscription switch
        {
            { Status: "trialing" or "active" } => new SavedCardPaymentDto(order.Id, SavedCardStatus.Processing, null, expiresAt),
            { Status: "incomplete", PaymentClientSecret: { } secret } => new SavedCardPaymentDto(order.Id, SavedCardStatus.RequiresAction, secret, expiresAt),
            _ => new SavedCardPaymentDto(order.Id, SavedCardStatus.Failed, null, expiresAt),
        };
    }

    /// <summary>
    /// The open Checkout of the order ends before the card of the account pays: unlinked first (its expiry then leaves the order
    /// open), then expired in Stripe. A session paid meanwhile makes the order not open (409).
    /// </summary>
    private async Task CloseCheckoutAsync(Order order, string sessionId, DateTimeOffset now, CancellationToken ct)
    {
        var session = await stripe.GetCheckoutSessionAsync(sessionId, ct).ConfigureAwait(false);
        if (session.Status == "complete")
        {
            throw new DomainException(BillingCodes.OrderNotOpen, 409, new Dictionary<string, object?> { ["status"] = "checkout_complete" });
        }

        await db.ExecuteInTenantTransactionAsync(() => db.Orders
            .Where(o => o.Id == order.Id && o.StripeCheckoutSessionId == sessionId && (o.Status == OrderStatus.Created || o.Status == OrderStatus.CheckoutOpen))
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.StripeCheckoutSessionId, (string?)null).SetProperty(o => o.UpdatedAt, now), ct), ct).ConfigureAwait(false);
        if (session.Status == "open")
        {
            await stripe.ExpireCheckoutSessionAsync(sessionId, $"checkout-expire:{sessionId}", ct).ConfigureAwait(false);
        }
    }
}
