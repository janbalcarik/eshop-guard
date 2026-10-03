using System.Globalization;
using System.Text.Json.Nodes;
using EshopGuard.Application.Options;
using EshopGuard.Billing.Pricing;
using EshopGuard.Billing.Stripe;
using EshopGuard.Billing.Subscriptions;
using EshopGuard.Billing.Webhooks;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Notifications;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Scheduling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Billing.Jobs;

/// <summary>
/// <c>billing.expire_order</c> (task 7.3): the order paid with the saved card waits for its subscription. Running in Stripe
/// (<c>trialing</c>/<c>active</c>) it pays the order like the webhook would; still <c>incomplete</c> at <c>checkout_expires_at</c>
/// (3-D Secure not confirmed) it is canceled in Stripe and the order expires. A newer attempt or a paid order skips the job.
/// </summary>
public sealed class ExpireOrderHandler(
    EshopGuardDataSource dataSource,
    IStripeGateway stripe,
    StripeEventProcessor processor,
    TimeProvider time,
    ILogger<ExpireOrderHandler> logger) : IJobHandler
{
    public string Kind => BillingJobs.ExpireOrderKind;

    public JobResourceClass ResourceClass => JobResourceClass.Io;

    public Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Job.TenantId is not { } tenantId)
        {
            return Task.FromResult<JobResult>(new JobResult.Fail("billing.tenant_missing"));
        }

        var root = context.Job.Payload.RootElement;
        return RunAsync(tenantId, Guid.Parse(root.GetProperty("order_id").GetString()!), root.GetProperty("attempt").GetInt32(), ct);
    }

    public async Task<JobResult> RunAsync(Guid tenantId, Guid orderId, int attempt, CancellationToken ct)
    {
        if (await ReadAsync(tenantId, orderId, ct).ConfigureAwait(false) is not { Status: "checkout_open", Subscription: { } subscriptionId } order || order.Attempt != attempt)
        {
            return JobResult.Done;
        }

        try
        {
            var subscription = await stripe.GetSubscriptionAsync(subscriptionId, ct).ConfigureAwait(false);
            if (subscription.Status is "trialing" or "active")
            {
                await processor.SettleSavedCardOrderAsync(tenantId, subscription, ct).ConfigureAwait(false);
                return JobResult.Done;
            }

            if (order.ExpiresAt is { } expires && time.GetUtcNow() < expires)
            {
                return JobResult.Done;
            }

            if (subscription.Status == "incomplete")
            {
                await stripe.CancelSubscriptionAsync(subscriptionId, $"expire-order:{orderId:N}:{attempt}", ct).ConfigureAwait(false);
            }
        }
        catch (StripeGatewayException e)
        {
            return e.Transient ? new JobResult.Retry(e.Code) : new JobResult.Fail(e.Code);
        }

        var now = time.GetUtcNow();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, null, ct).ConfigureAwait(false);
        var expired = await BillingSql.ExecuteAsync(transaction,
            """
            UPDATE billing.orders SET status = 'expired', updated_at = $4
            WHERE id = $1 AND status = 'checkout_open' AND checkout_attempt = $2 AND stripe_subscription_id = $3
            """, ct, orderId, attempt, subscriptionId, now).ConfigureAwait(false);
        if (expired == 1)
        {
            await BillingSql.AuditAsync(transaction, tenantId, null, BillingSql.System, "order.expired", "order", orderId.ToString("D"),
                new JsonObject { ["attempt"] = attempt, ["method"] = "saved_card" }, now, ct).ConfigureAwait(false);
        }

        // Never started: no longer running for its e-shop, so a later payment of the order is no second running subscription.
        await PendingSubscriptions.MarkNotStartedAsync(transaction, subscriptionId, now, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        logger.LogInformation("order.saved_card_expired {OrderId} {TenantId} {Attempt} {Expired}", orderId, tenantId, attempt, expired == 1);
        return JobResult.Done;
    }

    private async Task<(string Status, int Attempt, string? Subscription, DateTimeOffset? ExpiresAt)?> ReadAsync(Guid tenantId, Guid orderId, CancellationToken ct)
    {
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, null, ct).ConfigureAwait(false);
        var rows = await BillingSql.ListAsync(transaction,
            "SELECT status, checkout_attempt, stripe_subscription_id, checkout_expires_at FROM billing.orders WHERE id = $1",
            r => ((string Status, int Attempt, string? Subscription, DateTimeOffset? ExpiresAt)?)(r.GetString(0), r.GetInt32(1), r.Get<string>(2), r.Get<DateTimeOffset?>(3)),
            ct, orderId).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return rows.FirstOrDefault();
    }
}

/// <summary>
/// <c>billing.trial_reminder</c> (task 7.6, scenario „Připomenutí před první platbou“): for each subscription of the tenant in
/// the trial whose local date of <c>trial_end</c> minus <see cref="DaysBefore"/> days has come, one notification
/// <c>trial_ending</c> (in the app and by e-mail to owners and admins) with the monthly amount after the discount and the date of
/// the first payment. <c>trial_reminder_sent_at</c> makes it once; a canceled subscription gets none.
/// </summary>
public sealed class TrialReminderHandler(
    EshopGuardDataSource dataSource,
    NotificationDispatcher notifications,
    IOptions<LocalizationOptions> localization,
    TimeProvider time,
    ILogger<TrialReminderHandler> logger) : IJobHandler
{
    public const int DaysBefore = 7;

    public string Kind => BillingJobs.TrialReminderKind;

    public JobResourceClass ResourceClass => JobResourceClass.System;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Job.TenantId is not { } tenantId)
        {
            return new JobResult.Fail("billing.tenant_missing");
        }

        await RunAsync(tenantId, ct).ConfigureAwait(false);
        return JobResult.Done;
    }

    /// <summary>The number of reminders sent.</summary>
    public async Task<int> RunAsync(Guid tenantId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(localization.Value.TimeZone);
        var today = LocalDay(now, zone);
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, null, ct).ConfigureAwait(false);
        var due = await BillingSql.ListAsync(transaction,
            """
            SELECT s.id, s.shop_id, s.trial_end, s.unit_price, s.discount_percent, l.currency
            FROM billing.subscriptions s JOIN billing.price_lists l ON l.id = s.price_list_id
            WHERE s.status = 'trialing' AND NOT s.cancel_at_period_end AND s.trial_reminder_sent_at IS NULL AND s.trial_end > $1 AND s.trial_end <= $2
            ORDER BY s.trial_end
            FOR UPDATE OF s
            """,
            r => (Id: r.GetGuid(0), Shop: r.GetGuid(1), TrialEnd: r.GetFieldValue<DateTimeOffset>(2), Unit: r.GetDecimal(3), Discount: r.Get<decimal?>(4), Currency: r.GetString(5).Trim()),
            ct, now, now.AddDays(DaysBefore + 1)).ConfigureAwait(false);
        var sent = 0;
        foreach (var subscription in due)
        {
            var date = LocalDay(subscription.TrialEnd, zone);
            if (date.AddDays(-DaysBefore) > today)
            {
                continue;
            }

            var amount = PriceQuoteService.DiscountedMonthly(subscription.Unit, subscription.Discount) ?? subscription.Unit;
            await notifications.NotifyAsync(transaction, new NotificationRequest(tenantId, subscription.Shop, NotificationKinds.TrialEnding,
                new JsonObject
                {
                    ["amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture),
                    ["currency"] = subscription.Currency,
                    ["date"] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["subscription_id"] = subscription.Id.ToString("D"),
                },
                NotificationRoutes.Billing, new JsonObject()), ct).ConfigureAwait(false);
            await BillingSql.ExecuteAsync(transaction, "UPDATE billing.subscriptions SET trial_reminder_sent_at = $2 WHERE id = $1", ct, subscription.Id, now).ConfigureAwait(false);
            sent++;
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        if (sent > 0)
        {
            logger.LogInformation("billing.trial_reminders {TenantId} {Sent}", tenantId, sent);
        }

        return sent;
    }

    private static DateOnly LocalDay(DateTimeOffset at, TimeZoneInfo zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);
}

/// <summary>
/// Enqueues <c>billing.trial_reminder</c> every hour for each tenant with a customer of Stripe; the key of the day makes it one job
/// a day (the job reads the subscriptions in the transaction of its tenant, RLS allows no scan across tenants).
/// </summary>
public sealed class TrialReminderTask(IJobQueue queue) : IScheduledTask
{
    public string Name => BillingJobs.TrialReminderKind;

    public TimeSpan Interval => TimeSpan.FromHours(1);

    public async Task RunAsync(ScheduledTaskContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var tenants = await BillingSql.ListAsync(context.Transaction, "SELECT id FROM iam.tenants WHERE stripe_customer_id IS NOT NULL", r => r.GetGuid(0), ct)
            .ConfigureAwait(false);
        foreach (var tenantId in tenants)
        {
            await queue.EnqueueAsync(BillingJobs.TrialReminder(tenantId, context.Now), context.Transaction, ct).ConfigureAwait(false);
        }
    }
}
