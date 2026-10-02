using System.Text.Json.Nodes;
using EshopGuard.Billing.Stripe;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Tenancy;
using Microsoft.Extensions.Logging;

namespace EshopGuard.Billing.Subscriptions;

/// <summary>
/// One card per account (requirement „Jedna platební karta na účet“, AD 10, task 7.2): the default card of the customer of Stripe
/// is the card of every running subscription of the tenant; the previous card is detached. <c>billing.payment_methods</c> keeps at
/// most one row with <c>is_default</c> and without <c>detached_at</c>. Every call of Stripe has its key, so a repeated event
/// changes nothing twice.
/// </summary>
public sealed class AccountCardService(EshopGuardDataSource dataSource, IStripeGateway stripe, TimeProvider time, ILogger<AccountCardService> logger)
{
    /// <summary>
    /// Applies the default card of the customer; when the customer has none yet, <paramref name="fallbackPaymentMethodId"/> (the card
    /// of a new subscription) becomes the default. Returns the card, or null when the customer has no card.
    /// </summary>
    public async Task<StripePaymentMethodState?> ApplyDefaultAsync(Guid tenantId, string customerId, string? fallbackPaymentMethodId, CancellationToken ct)
    {
        var customer = await stripe.GetCustomerAsync(customerId, ct).ConfigureAwait(false);
        var defaultId = customer.DefaultPaymentMethodId;
        if (defaultId is null && fallbackPaymentMethodId is not null)
        {
            await stripe.SetCustomerDefaultPaymentMethodAsync(customerId, fallbackPaymentMethodId, $"default-card:{customerId}:{fallbackPaymentMethodId}", ct).ConfigureAwait(false);
            defaultId = fallbackPaymentMethodId;
        }

        if (defaultId is null || await stripe.GetPaymentMethodAsync(defaultId, ct).ConfigureAwait(false) is not { } card)
        {
            return null;
        }

        var now = time.GetUtcNow();
        List<string> detach;
        List<(string Subscription, string? Card)> subscriptions;
        await using (var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false))
        await using (var transaction = await TenantSql.BeginAsync(connection, tenantId, null, ct).ConfigureAwait(false))
        {
            detach = await BillingSql.ListAsync(transaction,
                "SELECT stripe_payment_method_id FROM billing.payment_methods WHERE detached_at IS NULL AND stripe_payment_method_id <> $1",
                r => r.GetString(0), ct, card.Id).ConfigureAwait(false);
            await BillingSql.ExecuteAsync(transaction,
                "UPDATE billing.payment_methods SET is_default = false, detached_at = $2, updated_at = $2 WHERE detached_at IS NULL AND stripe_payment_method_id <> $1",
                ct, card.Id, now).ConfigureAwait(false);
            await BillingSql.ExecuteAsync(transaction,
                """
                INSERT INTO billing.payment_methods (tenant_id, stripe_payment_method_id, brand, last4, exp_month, exp_year, is_default, created_at, updated_at)
                VALUES ($1, $2, $3, $4, $5, $6, true, $7, $7)
                ON CONFLICT (stripe_payment_method_id) DO UPDATE SET is_default = true, detached_at = NULL, brand = excluded.brand, last4 = excluded.last4,
                    exp_month = excluded.exp_month, exp_year = excluded.exp_year, updated_at = excluded.updated_at
                """, ct, tenantId, card.Id, card.Brand, card.Last4, card.ExpMonth, card.ExpYear, now).ConfigureAwait(false);
            subscriptions = await BillingSql.ListAsync(transaction,
                "SELECT stripe_subscription_id, NULL::text FROM billing.subscriptions WHERE stripe_subscription_id IS NOT NULL AND status IN ('trialing', 'active', 'past_due', 'incomplete')",
                r => (r.GetString(0), r.Get<string>(1)), ct).ConfigureAwait(false);
            if (detach.Count > 0)
            {
                await BillingSql.AuditAsync(transaction, tenantId, null, BillingSql.System, "billing.card_changed", "payment_method", card.Id,
                    new JsonObject { ["brand"] = card.Brand, ["last4"] = card.Last4 }, now, ct).ConfigureAwait(false);
            }

            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }

        foreach (var (subscription, _) in subscriptions)
        {
            await stripe.SetSubscriptionPaymentMethodAsync(subscription, card.Id, $"card:{subscription}:{card.Id}", ct).ConfigureAwait(false);
        }

        foreach (var old in detach)
        {
            try
            {
                await stripe.DetachPaymentMethodAsync(old, $"detach:{old}", ct).ConfigureAwait(false);
            }
            catch (StripeGatewayException e) when (!e.Transient)
            {
                // Detached already (or deleted with the customer): the row stays detached.
                logger.LogInformation("card.detach_skipped {TenantId} {Code}", tenantId, e.Code);
            }
        }

        return card;
    }
}
