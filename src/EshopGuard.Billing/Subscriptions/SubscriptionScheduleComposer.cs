using System.Text.Json.Nodes;
using EshopGuard.Billing.Pricing;
using EshopGuard.Billing.Stripe;
using EshopGuard.Billing.Webhooks;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Tenancy;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EshopGuard.Billing.Subscriptions;

/// <summary>
/// The result of composing a schedule: <c>skipped</c>, <c>unchanged</c>, <c>released</c>, <c>composed</c>, or a code to try
/// again (<see cref="Retry"/>) or to give up (<see cref="Failed"/>).
/// </summary>
public sealed record ComposeOutcome(string Outcome, string? Code = null)
{
    public const string Skipped = "skipped";
    public const string Unchanged = "unchanged";
    public const string Released = "released";
    public const string Composed = "composed";
    public const string RetryLater = "retry";
    public const string Failed = "failed";

    public static ComposeOutcome Retry(string code) => new(RetryLater, code);

    public static ComposeOutcome Fail(string code) => new(Failed, code);
}

/// <summary>
/// <c>billing.compose_schedule</c> (task 8.2, design „Skládání“): the pending changes of a subscription become the phases of
/// its Subscription Schedule in Stripe, from the current phase (its start kept) to the last change, released at the end, so the
/// subscription keeps the last price. Phases with the same Price and coupon merge; several changes of one date make one phase.
/// <list type="bullet">
/// <item>The same fingerprint of the phases with a schedule makes no call of Stripe.</item>
/// <item>No pending change (or the subscription ends at the end of its period) releases the schedule.</item>
/// <item>A change whose date has come waits for the event of Stripe that applies it (<c>billing.schedule_change_pending</c>),
/// a subscription whose price in Stripe differs from the database waits for its event (<c>billing.subscription_stale</c>).</item>
/// <item>What cannot be charged composes nothing and raises an alert (fail-closed).</item>
/// </list>
/// No transaction is open while Stripe is called; the keys of idempotence come from the fingerprint and the version.
/// </summary>
public sealed class SubscriptionScheduleComposer(
    EshopGuardDataSource dataSource,
    IStripeGateway stripe,
    TimeProvider time,
    ILogger<SubscriptionScheduleComposer> logger)
{
    public const string ChangePending = "billing.schedule_change_pending";
    public const string SubscriptionStale = "billing.subscription_stale";

    public async Task<ComposeOutcome> ComposeAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        PlannedSubscription? subscription;
        List<PendingChange> pending;
        Dictionary<Guid, PriceListSnapshot> lists;
        long version;
        await using (var read = await TenantSql.BeginAsync(connection, tenantId, null, ct).ConfigureAwait(false))
        {
            subscription = await SubscriptionChangeSql.SubscriptionAsync(read, subscriptionId, ct).ConfigureAwait(false);
            if (subscription is not { Running: true, StripeSubscriptionId: not null })
            {
                await read.CommitAsync(ct).ConfigureAwait(false);
                return new ComposeOutcome(ComposeOutcome.Skipped);
            }

            var all = await SubscriptionChangeSql.PendingAsync(read, subscription.Id, ct).ConfigureAwait(false);
            pending = await SubscriptionChangeSql.CancelMissedAsync(read, tenantId, all, now, logger, ct).ConfigureAwait(false);
            lists = await SubscriptionChangeSql.ListsAsync(read, [subscription.PriceListId, .. pending.Select(c => c.PriceListId)], ct).ConfigureAwait(false);
            version = await SubscriptionChangeSql.VersionAsync(read, subscription.Id, ct).ConfigureAwait(false);
            await read.CommitAsync(ct).ConfigureAwait(false);
        }

        if (pending.Any(c => c.EffectiveAt <= now))
        {
            return ComposeOutcome.Retry(ChangePending);
        }

        try
        {
            if (pending.Count == 0 || subscription.CancelAtPeriodEnd)
            {
                return await ReleaseAsync(connection, tenantId, subscription, now, ct).ConfigureAwait(false);
            }

            var timeline = SubscriptionTimeline.Build(subscription.Price, subscription.RunningTrialEnd, subscription.ShopOrdinal, pending, id => lists.GetValueOrDefault(id));
            if (timeline.Problem is { } problem)
            {
                await using var alert = await TenantSql.BeginAsync(connection, tenantId, null, ct).ConfigureAwait(false);
                await BillingAlerts.RaiseAsync(alert, tenantId, "billing.alert.schedule_failed", "subscription", subscription.Id.ToString("D"), now, logger, ct)
                    .ConfigureAwait(false);
                await alert.CommitAsync(ct).ConfigureAwait(false);
                return ComposeOutcome.Fail(problem);
            }

            if (timeline.Hash == subscription.ScheduleHash && subscription.StripeScheduleId is not null)
            {
                return new ComposeOutcome(ComposeOutcome.Unchanged);
            }

            var remote = await stripe.GetSubscriptionAsync(subscription.StripeSubscriptionId, ct).ConfigureAwait(false);
            if (remote.Status is not ("trialing" or "active" or "past_due"))
            {
                return new ComposeOutcome(ComposeOutcome.Skipped);
            }

            if (remote.PriceId != subscription.StripePriceId || remote.CouponId != subscription.StripeCouponId)
            {
                return ComposeOutcome.Retry(SubscriptionStale);
            }

            var schedule = remote.ScheduleId is { } existing ? await ActiveScheduleAsync(existing, ct).ConfigureAwait(false) : null;
            schedule ??= await stripe.CreateScheduleFromSubscriptionAsync(remote.Id, $"schedule-create:{subscription.Id:N}:v{version}", ct).ConfigureAwait(false);
            var phases = Phases(timeline.Phases, CurrentStart(schedule, remote, now));
            await stripe.UpdateScheduleAsync(schedule.Id, phases, $"schedule:{subscription.Id:N}:{timeline.Hash}:{phases[0].StartDate.ToUnixTimeSeconds()}", ct)
                .ConfigureAwait(false);
            await StoreAsync(connection, tenantId, subscription.Id, schedule.Id, timeline.Hash, pending.Select(c => c.Id).ToList(), now, ct).ConfigureAwait(false);
            logger.LogInformation("billing.schedule_composed {SubscriptionId} {TenantId} {Phases}", subscription.Id, tenantId, phases.Count);
            return new ComposeOutcome(ComposeOutcome.Composed);
        }
        catch (StripeGatewayException e)
        {
            return e.Transient ? ComposeOutcome.Retry(e.Code) : ComposeOutcome.Fail(e.Code);
        }
    }

    /// <summary>
    /// The phases for Stripe: the current one from <paramref name="currentStart"/> (Stripe keeps the start of a running phase),
    /// each next one from its date to the next one, the last one without an end.
    /// </summary>
    public static IReadOnlyList<StripeSchedulePhase> Phases(IReadOnlyList<SchedulePhaseSpec> specs, DateTimeOffset currentStart)
    {
        ArgumentNullException.ThrowIfNull(specs);
        var phases = new List<StripeSchedulePhase>(specs.Count);
        for (var i = 0; i < specs.Count; i++)
        {
            var start = i == 0 ? currentStart : specs[i].Start!.Value;
            var end = i + 1 < specs.Count ? specs[i + 1].Start : null;
            phases.Add(new StripeSchedulePhase(start, end, specs[i].PriceId, specs[i].CouponId, specs[i].TrialEnd));
        }

        return phases;
    }

    /// <summary>The start of the phase running now (the last one already started), else of the first one, else of the period.</summary>
    private static DateTimeOffset CurrentStart(StripeScheduleState schedule, StripeSubscriptionState subscription, DateTimeOffset now)
    {
        var started = schedule.Phases.Where(p => p.StartDate <= now).Select(p => (DateTimeOffset?)p.StartDate).Max();
        return started ?? schedule.Phases.Select(p => (DateTimeOffset?)p.StartDate).FirstOrDefault() ?? subscription.CurrentPeriodStart ?? now;
    }

    private async Task<StripeScheduleState?> ActiveScheduleAsync(string scheduleId, CancellationToken ct)
    {
        try
        {
            var schedule = await stripe.GetScheduleAsync(scheduleId, ct).ConfigureAwait(false);
            return schedule.Status is "active" or "not_started" ? schedule : null;
        }
        catch (StripeGatewayException e) when (e.HttpStatus == 404)
        {
            return null;
        }
    }

    private async Task<ComposeOutcome> ReleaseAsync(NpgsqlConnection connection, Guid tenantId, PlannedSubscription subscription, DateTimeOffset now, CancellationToken ct)
    {
        if (subscription.StripeScheduleId is null && subscription.ScheduleHash is null)
        {
            return new ComposeOutcome(ComposeOutcome.Unchanged);
        }

        if (subscription.StripeScheduleId is { } scheduleId && await ActiveScheduleAsync(scheduleId, ct).ConfigureAwait(false) is not null)
        {
            await stripe.ReleaseScheduleAsync(scheduleId, $"schedule-release:{subscription.Id:N}:{scheduleId}", ct).ConfigureAwait(false);
        }

        await StoreAsync(connection, tenantId, subscription.Id, null, null, [], now, ct).ConfigureAwait(false);
        logger.LogInformation("billing.schedule_released {SubscriptionId} {TenantId}", subscription.Id, tenantId);
        return new ComposeOutcome(ComposeOutcome.Released);
    }

    private static async Task StoreAsync(
        NpgsqlConnection connection, Guid tenantId, Guid subscriptionId, string? scheduleId, string? hash, IReadOnlyList<Guid> changes, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, null, ct).ConfigureAwait(false);
        await BillingSql.ExecuteAsync(transaction,
            "UPDATE billing.subscriptions SET stripe_schedule_id = $2, schedule_hash = $3, updated_at = $4 WHERE id = $1",
            ct, subscriptionId, BillingSql.Text(scheduleId), BillingSql.Text(hash), now).ConfigureAwait(false);
        if (hash is not null && changes.Count > 0)
        {
            await BillingSql.ExecuteAsync(transaction,
                "UPDATE billing.subscription_changes SET stripe_schedule_phase_hash = $2, updated_at = $3 WHERE id = ANY($1) AND status IN ('scheduled', 'notified')",
                ct, changes.ToArray(), hash, now).ConfigureAwait(false);
        }

        await BillingSql.AuditAsync(transaction, tenantId, null, BillingSql.System, hash is null ? "subscription.schedule_released" : "subscription.schedule_composed",
            "subscription", subscriptionId.ToString("D"), hash is null ? null : new JsonObject { ["hash"] = hash }, now, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }
}
