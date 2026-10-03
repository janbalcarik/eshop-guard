using System.Text.Json.Nodes;
using EshopGuard.Application.Options;
using EshopGuard.Billing.Pricing;
using EshopGuard.Billing.Subscriptions;
using EshopGuard.Billing.Webhooks;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Scheduling;
using EshopGuard.Jobs.Shops;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EshopGuard.Billing.Jobs;

/// <summary>
/// <c>billing.evaluate_tiers</c> (task 8.3, Flow 4): for each running subscription of the tenant (or of the e-shop in the payload)
/// the tier by its counted products, the active price list of its market and currency and the volume discount by the order of
/// the e-shop, each planned by <see cref="SubscriptionChangePlanner"/>. A count only from a partial analysis plans only a
/// higher tier; a count in the custom tier raises an alert and changes nothing (fail-closed).
/// </summary>
/// <remarks>
/// The reader of counted products (EF Core) is resolved only when the job runs: the registry of handlers builds every handler at
/// the start of the worker, before its configuration is validated.
/// </remarks>
public sealed class EvaluateTiersHandler(
    EshopGuardDataSource dataSource,
    IServiceProvider services,
    SubscriptionChangePlanner planner,
    TimeProvider time,
    ILogger<EvaluateTiersHandler> logger) : IJobHandler
{
    public string Kind => BillingJobs.EvaluateTiersKind;

    public JobResourceClass ResourceClass => JobResourceClass.System;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Job.TenantId is not { } tenantId)
        {
            return new JobResult.Fail("billing.tenant_missing");
        }

        Guid? shopId = context.Job.Payload.RootElement.TryGetProperty("shop_id", out var shop) ? Guid.Parse(shop.GetString()!) : null;
        await RunAsync(tenantId, shopId, ct).ConfigureAwait(false);
        return JobResult.Done;
    }

    /// <summary>The results of planning, three per subscription (tier, price list, discount).</summary>
    public async Task<IReadOnlyList<PlanResult>> RunAsync(Guid tenantId, Guid? shopId, CancellationToken ct)
    {
        var subscriptions = await RunningAsync(tenantId, shopId, ct).ConfigureAwait(false);
        var counted = services.GetRequiredService<ICountedProductsReader>();
        var counts = new Dictionary<Guid, CountedProducts?>();
        foreach (var shop in subscriptions.Select(s => s.ShopId).Distinct())
        {
            counts[shop] = await counted.ReadAsync(shop, ct).ConfigureAwait(false);
        }

        var now = time.GetUtcNow();
        var results = new List<PlanResult>();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, null, ct).ConfigureAwait(false);
        foreach (var (id, shop, listId) in subscriptions)
        {
            if (await PriceListSql.LoadAsync(transaction, listId, ct).ConfigureAwait(false) is not { } list)
            {
                continue;
            }

            if (counts[shop] is { } products)
            {
                var tier = TierResolver.Resolve(list.Tiers, products.Count);
                if (tier.IsCustom || tier.Tier is null)
                {
                    await BillingAlerts.RaiseAsync(transaction, tenantId, "billing.alert.tier_custom", "subscription", id.ToString("D"), now, logger, ct).ConfigureAwait(false);
                }
                else
                {
                    results.Add(await planner.PlanAsync(transaction, tenantId, id, ChangeRequest.Tier(tier.Tier.Code, products.Count, products.LowerBound), ct)
                        .ConfigureAwait(false));
                }
            }

            if (await PriceListSql.ActiveIdAsync(transaction, list.List.MarketCode, list.List.Currency, now, ct).ConfigureAwait(false) is { } active && active != listId)
            {
                results.Add(await planner.PlanAsync(transaction, tenantId, id, ChangeRequest.PriceList(active), ct).ConfigureAwait(false));
            }

            results.Add(await planner.PlanAsync(transaction, tenantId, id, ChangeRequest.Discount(), ct).ConfigureAwait(false));
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        var scheduled = results.Count(r => r.Outcome == PlanResult.Scheduled);
        if (scheduled > 0)
        {
            logger.LogInformation("billing.tiers_evaluated {TenantId} {Subscriptions} {Scheduled}", tenantId, subscriptions.Count, scheduled);
        }

        return results;
    }

    private async Task<List<(Guid Id, Guid ShopId, Guid PriceListId)>> RunningAsync(Guid tenantId, Guid? shopId, CancellationToken ct)
    {
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, null, ct).ConfigureAwait(false);
        var rows = await BillingSql.ListAsync(transaction,
            """
            SELECT id, shop_id, price_list_id FROM billing.subscriptions
            WHERE status IN ('trialing', 'active', 'past_due') AND NOT cancel_at_period_end AND ($1::uuid IS NULL OR shop_id = $1)
            ORDER BY created_at, id
            """,
            r => (r.GetGuid(0), r.GetGuid(1), r.GetGuid(2)), ct, shopId).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return rows;
    }
}

/// <summary>
/// Enqueues <c>billing.evaluate_tiers</c> every hour for each tenant with a customer of Stripe once its local time is past
/// <see cref="LocalTime"/>; the key of the local day makes it one job a day.
/// </summary>
public sealed class EvaluateTiersTask(IJobQueue queue, IOptions<LocalizationOptions> localization) : IScheduledTask
{
    public static readonly TimeOnly LocalTime = new(3, 30);

    public string Name => BillingJobs.EvaluateTiersKind;

    public TimeSpan Interval => TimeSpan.FromHours(1);

    public async Task RunAsync(ScheduledTaskContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(localization.Value.TimeZone);
        var local = TimeZoneInfo.ConvertTime(context.Now, zone);
        if (TimeOnly.FromDateTime(local.DateTime) < LocalTime)
        {
            return;
        }

        var day = SubscriptionChangeSql.Day(DateOnly.FromDateTime(local.DateTime));
        var tenants = await BillingSql.ListAsync(context.Transaction, "SELECT id FROM iam.tenants WHERE stripe_customer_id IS NOT NULL", r => r.GetGuid(0), ct)
            .ConfigureAwait(false);
        foreach (var tenantId in tenants)
        {
            await queue.EnqueueAsync(BillingJobs.EvaluateTiers(tenantId, day), context.Transaction, ct).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// <c>billing.compose_schedule</c> (task 8.2): the Subscription Schedule of one subscription by
/// <see cref="SubscriptionScheduleComposer"/>. A change due now waits for its event of Stripe and a subscription read before its
/// last change waits for the webhook (retried after <see cref="WaitForStripe"/>); an error of Stripe is retried when transient.
/// </summary>
public sealed class ComposeScheduleHandler(SubscriptionScheduleComposer composer) : IJobHandler
{
    public static readonly TimeSpan WaitForStripe = TimeSpan.FromMinutes(15);

    public string Kind => BillingJobs.ComposeScheduleKind;

    public JobResourceClass ResourceClass => JobResourceClass.Io;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Job.TenantId is not { } tenantId)
        {
            return new JobResult.Fail("billing.tenant_missing");
        }

        var outcome = await composer.ComposeAsync(tenantId, BillingJobs.GuidOf(context.Job.Payload, "subscription_id"), ct).ConfigureAwait(false);
        return Map(outcome);
    }

    public static JobResult Map(ComposeOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return outcome.Outcome switch
        {
            ComposeOutcome.RetryLater => new JobResult.Retry(outcome.Code!,
                outcome.Code is SubscriptionScheduleComposer.ChangePending or SubscriptionScheduleComposer.SubscriptionStale ? WaitForStripe : null),
            ComposeOutcome.Failed => new JobResult.Fail(outcome.Code!),
            _ => JobResult.Done,
        };
    }
}

/// <summary>
/// <c>billing.schedule_price_list_transfer</c> (task 8.5): after the activation of a price list, the change <c>price_list</c> of
/// every running subscription on another list of its market and currency, tenant by tenant in batches of
/// <see cref="BatchSize"/> (each in its own transaction). Running again keeps the planned changes.
/// </summary>
public sealed class SchedulePriceListTransferHandler(
    EshopGuardDataSource dataSource,
    SubscriptionChangePlanner planner,
    ILogger<SchedulePriceListTransferHandler> logger) : IJobHandler
{
    public const int BatchSize = 200;

    public string Kind => BillingJobs.SchedulePriceListTransferKind;

    public JobResourceClass ResourceClass => JobResourceClass.System;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        await RunAsync(BillingJobs.GuidOf(context.Job.Payload, "price_list_id"), ct).ConfigureAwait(false);
        return JobResult.Done;
    }

    /// <summary>The number of subscriptions planned (a new or a kept change).</summary>
    public async Task<int> RunAsync(Guid priceListId, CancellationToken ct)
    {
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        (string Market, string Currency, string Status)? list;
        await using (var read = await BillingSql.BeginGlobalAsync(connection, null, ct).ConfigureAwait(false))
        {
            list = (await BillingSql.ListAsync(read, "SELECT market_code, currency, status FROM billing.price_lists WHERE id = $1",
                r => ((string, string, string)?)(r.GetString(0), r.GetString(1).Trim(), r.GetString(2)), ct, priceListId).ConfigureAwait(false)).FirstOrDefault();
            await read.CommitAsync(ct).ConfigureAwait(false);
        }

        if (list is not { Status: "published" } target)
        {
            logger.LogInformation("price_list.transfer_skipped {PriceListId} {Status}", priceListId, list?.Status);
            return 0;
        }

        var planned = 0;
        Guid? after = null;
        while (true)
        {
            List<Guid> tenants;
            await using (var read = await BillingSql.BeginGlobalAsync(connection, null, ct).ConfigureAwait(false))
            {
                tenants = await BillingSql.ListAsync(read,
                    "SELECT id FROM iam.tenants WHERE stripe_customer_id IS NOT NULL AND ($1::uuid IS NULL OR id > $1) ORDER BY id LIMIT $2",
                    r => r.GetGuid(0), ct, after, BatchSize).ConfigureAwait(false);
                await read.CommitAsync(ct).ConfigureAwait(false);
            }

            foreach (var tenantId in tenants)
            {
                planned += await TransferAsync(connection, tenantId, priceListId, target.Market, target.Currency, ct).ConfigureAwait(false);
            }

            if (tenants.Count < BatchSize)
            {
                break;
            }

            after = tenants[^1];
        }

        logger.LogInformation("price_list.transfer_planned {PriceListId} {Subscriptions}", priceListId, planned);
        return planned;
    }

    private async Task<int> TransferAsync(NpgsqlConnection connection, Guid tenantId, Guid priceListId, string market, string currency, CancellationToken ct)
    {
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, null, ct).ConfigureAwait(false);
        var subscriptions = await BillingSql.ListAsync(transaction,
            """
            SELECT s.id FROM billing.subscriptions s JOIN billing.price_lists l ON l.id = s.price_list_id
            WHERE s.status IN ('trialing', 'active', 'past_due') AND NOT s.cancel_at_period_end
              AND l.market_code = $1 AND l.currency = $2 AND s.price_list_id <> $3
            ORDER BY s.created_at, s.id
            """, r => r.GetGuid(0), ct, market, currency, priceListId).ConfigureAwait(false);
        var planned = 0;
        foreach (var id in subscriptions)
        {
            var result = await planner.PlanAsync(transaction, tenantId, id, ChangeRequest.PriceList(priceListId), ct).ConfigureAwait(false);
            planned += result.Outcome is PlanResult.Scheduled or PlanResult.Kept ? 1 : 0;
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return planned;
    }
}

/// <summary>A full analysis counted the products of an e-shop: its tier is checked by <c>billing.evaluate_tiers</c> (task 8.3).</summary>
public sealed class BillingProductCountObserver(IJobQueue queue) : IProductCountObserver
{
    public async Task ChangedAsync(NpgsqlTransaction transaction, Guid tenantId, Guid shopId, Guid sourceId, CancellationToken ct) =>
        await queue.EnqueueAsync(BillingJobs.EvaluateTiersForShop(tenantId, shopId, sourceId), transaction, ct).ConfigureAwait(false);
}
