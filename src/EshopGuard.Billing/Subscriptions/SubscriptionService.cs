using System.Text.Json.Nodes;
using EshopGuard.Application.Problems;
using EshopGuard.Billing.Contracts;
using EshopGuard.Billing.Orders;
using EshopGuard.Billing.Pricing;
using EshopGuard.Billing.Stripe;
using EshopGuard.Billing.Tax;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EshopGuard.Billing.Subscriptions;

/// <summary>
/// The monitoring of an e-shop by the customer (requirement „Předplatné sledování po e-shopech“, task 7.5):
/// <list type="bullet">
/// <item>canceling sets <c>cancel_at_period_end</c>: the monitoring runs to the end of the paid period (or of the trial, nothing is
/// charged), the e-shop and its data stay;</item>
/// <item>resuming takes the cancellation back, only before that end;</item>
/// <item>starting again after the end makes a new subscription without a trial and without the analysis, at the current price list,
/// tier and volume discount, with the card of the account; first <c>confirm_required</c> with the amount and the date, the
/// subscription only after the confirmation of that amount.</item>
/// </list>
/// Stripe is called first, the row follows; the webhook brings the same state again (it never depends on this order).
/// </summary>
public sealed class SubscriptionService(
    EshopGuardDb db,
    IStripeGateway stripe,
    StripeCustomers customers,
    PriceListReader prices,
    IOptions<BillingOptions> options,
    TimeProvider time,
    ILogger<SubscriptionService> logger)
{
    public const string ConfirmRequired = "confirm_required";

    public async Task<SubscriptionStateDto> CancelAsync(Guid userId, Guid shopId, CancellationToken ct)
    {
        var tenantId = RequireEnabled();
        var subscription = await RunningAsync(shopId, ct).ConfigureAwait(false);
        if (subscription is not { Status: SubscriptionStatus.Trialing or SubscriptionStatus.Active or SubscriptionStatus.PastDue, StripeSubscriptionId: { } stripeId })
        {
            throw new DomainException(BillingCodes.SubscriptionNotCancelable, 409);
        }

        if (subscription.CancelAtPeriodEnd)
        {
            return Dto(subscription);
        }

        var state = await stripe.SetCancelAtPeriodEndAsync(stripeId, true, Key("subscription-cancel", subscription), ct).ConfigureAwait(false);
        return await StoreAsync(tenantId, userId, subscription, state, "subscription.cancel_requested", ct).ConfigureAwait(false);
    }

    public async Task<SubscriptionStateDto> ResumeAsync(Guid userId, Guid shopId, CancellationToken ct)
    {
        var tenantId = RequireEnabled();
        var subscription = await RunningAsync(shopId, ct).ConfigureAwait(false);
        if (subscription is not { Status: SubscriptionStatus.Trialing or SubscriptionStatus.Active or SubscriptionStatus.PastDue, StripeSubscriptionId: { } stripeId })
        {
            throw new DomainException(BillingCodes.SubscriptionNotResumable, 409);
        }

        if (!subscription.CancelAtPeriodEnd)
        {
            return Dto(subscription);
        }

        if (End(subscription) is { } end && end <= time.GetUtcNow())
        {
            throw new DomainException(BillingCodes.SubscriptionNotResumable, 409, new Dictionary<string, object?> { ["endedAt"] = end });
        }

        var state = await stripe.SetCancelAtPeriodEndAsync(stripeId, false, Key("subscription-resume", subscription), ct).ConfigureAwait(false);
        return await StoreAsync(tenantId, userId, subscription, state, "subscription.resumed", ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Monitoring started again after it ended (scenario „Nové zapnutí po skončení sledování“). Without <paramref name="confirm"/>, or
    /// when <paramref name="amount"/> is not the current one, the answer is <c>confirm_required</c> with the amount and the date and
    /// nothing is created. A start waiting for 3-D Secure answers its client secret again.
    /// </summary>
    public async Task<StartSubscriptionDto> StartAgainAsync(Guid userId, Guid shopId, bool confirm, decimal? amount, CancellationToken ct)
    {
        var tenantId = RequireEnabled();
        var now = time.GetUtcNow();
        var (shop, history, card) = await db.ExecuteInTenantTransactionAsync(async () => (
            await db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.Id == shopId, ct).ConfigureAwait(false),
            await db.Subscriptions.AsNoTracking().Where(s => s.ShopId == shopId).OrderByDescending(s => s.CreatedAt).ToListAsync(ct).ConfigureAwait(false),
            await db.PaymentMethods.AsNoTracking().FirstOrDefaultAsync(p => p.IsDefault && p.DetachedAt == null, ct).ConfigureAwait(false)), ct).ConfigureAwait(false);
        if (shop is null)
        {
            throw new DomainException(ProblemCodes.ShopNotFound, 404);
        }

        if (history.Count == 0)
        {
            throw new DomainException(BillingCodes.SubscriptionNotFound, 404);
        }

        if (history.FirstOrDefault(s => s.Status is SubscriptionStatus.Trialing or SubscriptionStatus.Active or SubscriptionStatus.PastDue or SubscriptionStatus.Incomplete) is { } running)
        {
            return await RunningStartAsync(running, ct).ConfigureAwait(false);
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

        var currency = tenant.Currency ?? await prices.MarketCurrencyAsync(tenant.MarketCode, ct).ConfigureAwait(false) ?? string.Empty;
        var list = await prices.ActiveAsync(tenant.MarketCode, currency, ct).ConfigureAwait(false)
            ?? throw new DomainException(BillingCodes.PriceListMissing, 409);
        var tierCode = shop.TierCode ?? history[0].TierCode;
        if (list.Tier(tierCode) is not { } tier || TierResolver.IsCustom(tier) || tier.MonitoringMonthly is not { } monthly)
        {
            throw new DomainException(BillingCodes.TierUnavailable, 409, new Dictionary<string, object?> { ["tier"] = tierCode });
        }

        if (tier.StripePriceMonthly is not { } priceId)
        {
            throw new BillingUnavailableException();
        }

        var ordinal = 1 + await db.ExecuteInTenantTransactionAsync(() => db.Subscriptions.AsNoTracking()
            .Where(s => s.ShopId != shopId && (s.Status == SubscriptionStatus.Trialing || s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PastDue))
            .Select(s => s.ShopId).Distinct().CountAsync(ct), ct).ConfigureAwait(false);
        var discount = VolumeDiscountResolver.Resolve(list.Discounts, ordinal);
        var due = PriceQuoteService.DiscountedMonthly(monthly, discount?.Percent) ?? monthly;
        if (!confirm || amount != due)
        {
            return new StartSubscriptionDto(ConfirmRequired, tier.Code, due, list.List.Currency, discount?.Percent, now, null);
        }

        var customerId = await customers.EnsureAsync(tenantId, ct).ConfigureAwait(false);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tenant_id"] = tenantId.ToString("D"),
            ["shop_id"] = shopId.ToString("D"),
        };

        // One key per attempt: a double click gets the same subscription; after the webhook stored a failed one, the next is new.
        var subscription = await stripe.CreateSubscriptionAsync(
            new StripeSubscriptionRequest(customerId, priceId, null, discount?.StripeCouponId, null, card.StripePaymentMethodId, metadata),
            $"start-again:{shopId:N}:{history.Count}:{priceId}:{discount?.StripeCouponId ?? "none"}", ct).ConfigureAwait(false);
        await db.ExecuteInTenantTransactionAsync(() => BillingSql.AuditAsync((NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction(), tenantId, userId,
            BillingSql.User, "subscription.start_again_requested", "shop", shopId.ToString("D"),
            new JsonObject { ["tier"] = tier.Code, ["amount"] = due, ["discount_percent"] = discount?.Percent, ["status"] = subscription.Status }, now, ct), ct).ConfigureAwait(false);
        logger.LogInformation("subscription.start_again {ShopId} {TenantId} {Tier} {Status}", shopId, tenantId, tier.Code, subscription.Status);
        return new StartSubscriptionDto(StartStatus(subscription), tier.Code, due, list.List.Currency, discount?.Percent, now,
            subscription.Status == "incomplete" ? subscription.PaymentClientSecret : null);
    }

    /// <summary>
    /// The e-shop has a running subscription: a start again waiting for 3-D Secure answers its secret, anything else is running
    /// already (409).
    /// </summary>
    private async Task<StartSubscriptionDto> RunningStartAsync(Subscription running, CancellationToken ct)
    {
        if (running is { Status: SubscriptionStatus.Incomplete, OrderId: null, StripeSubscriptionId: { } stripeId })
        {
            var state = await stripe.GetSubscriptionAsync(stripeId, ct).ConfigureAwait(false);
            if (state is { Status: "incomplete", PaymentClientSecret: { } secret })
            {
                return new StartSubscriptionDto(SavedCardStatus.RequiresAction, running.TierCode,
                    PriceQuoteService.DiscountedMonthly(running.UnitPrice, running.DiscountPercent) ?? running.UnitPrice,
                    await CurrencyAsync(running, ct).ConfigureAwait(false), running.DiscountPercent, running.CreatedAt, secret);
            }
        }

        throw new DomainException(BillingCodes.SubscriptionAlreadyRunning, 409, new Dictionary<string, object?> { ["status"] = running.Status.ToString() });
    }

    private async Task<string> CurrencyAsync(Subscription subscription, CancellationToken ct) =>
        (await prices.GetAsync(subscription.PriceListId, ct).ConfigureAwait(false))?.List.Currency ?? string.Empty;

    private static string StartStatus(StripeSubscriptionState subscription) => subscription switch
    {
        { Status: "trialing" or "active" } => SavedCardStatus.Processing,
        { Status: "incomplete", PaymentClientSecret: not null } => SavedCardStatus.RequiresAction,
        _ => SavedCardStatus.Failed,
    };

    private Guid RequireEnabled()
    {
        var tenantId = db.TenantContext.RequireTenantId();
        return options.Value.StripeEnabled ? tenantId : throw new BillingUnavailableException();
    }

    /// <summary>The running subscription of the e-shop (404 when the e-shop never had one).</summary>
    private async Task<Subscription?> RunningAsync(Guid shopId, CancellationToken ct)
    {
        var (shopExists, history) = await db.ExecuteInTenantTransactionAsync(async () => (
            await db.Shops.AsNoTracking().AnyAsync(s => s.Id == shopId, ct).ConfigureAwait(false),
            await db.Subscriptions.AsNoTracking().Where(s => s.ShopId == shopId).OrderByDescending(s => s.CreatedAt).ToListAsync(ct).ConfigureAwait(false)), ct)
            .ConfigureAwait(false);
        if (!shopExists)
        {
            throw new DomainException(ProblemCodes.ShopNotFound, 404);
        }

        if (history.Count == 0)
        {
            throw new DomainException(BillingCodes.SubscriptionNotFound, 404);
        }

        return history.FirstOrDefault(s => SubscriptionSync.Running.Contains(Text(s.Status)));
    }

    /// <summary>The state of Stripe in the row now (the webhook writes the same again) and the audit.</summary>
    private async Task<SubscriptionStateDto> StoreAsync(Guid tenantId, Guid userId, Subscription subscription, StripeSubscriptionState state, string action, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            await db.Subscriptions.Where(s => s.Id == subscription.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CancelAtPeriodEnd, state.CancelAtPeriodEnd).SetProperty(x => x.UpdatedAt, now), ct).ConfigureAwait(false);
            await BillingSql.AuditAsync((NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction(), tenantId, userId, BillingSql.User, action, "subscription",
                subscription.Id.ToString("D"), new JsonObject { ["shop_id"] = subscription.ShopId.ToString("D"), ["end"] = End(subscription) }, now, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
        logger.LogInformation("{Action} {SubscriptionId} {TenantId}", action, subscription.Id, tenantId);
        return new SubscriptionStateDto(subscription.ShopId, SubscriptionSync.Status(state.Status), state.CancelAtPeriodEnd, state.TrialEnd, state.CurrentPeriodEnd);
    }

    /// <summary>The end of the monitoring after a cancellation: the end of the trial, else of the paid period.</summary>
    public static DateTimeOffset? End(Subscription subscription) =>
        subscription.Status == SubscriptionStatus.Trialing ? subscription.TrialEnd ?? subscription.CurrentPeriodEnd : subscription.CurrentPeriodEnd;

    private static SubscriptionStateDto Dto(Subscription subscription) =>
        new(subscription.ShopId, Text(subscription.Status), subscription.CancelAtPeriodEnd, subscription.TrialEnd, subscription.CurrentPeriodEnd);

    /// <summary>The key changes with every stored change of the row, so canceling, resuming and canceling again are three calls.</summary>
    private static string Key(string prefix, Subscription subscription) =>
        $"{prefix}:{subscription.Id:N}:{subscription.UpdatedAt.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    private static string Text(SubscriptionStatus status) => status switch
    {
        SubscriptionStatus.Trialing => "trialing",
        SubscriptionStatus.Active => "active",
        SubscriptionStatus.PastDue => "past_due",
        SubscriptionStatus.Canceled => "canceled",
        SubscriptionStatus.Incomplete => "incomplete",
        _ => "paused",
    };
}
