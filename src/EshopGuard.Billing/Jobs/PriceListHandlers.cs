using EshopGuard.Application.Problems;
using EshopGuard.Billing.Pricing;
using EshopGuard.Billing.Stripe;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Scheduling;
using Microsoft.Extensions.Logging;

namespace EshopGuard.Billing.Jobs;

/// <summary><c>billing.price_list_admin</c>: one command of the admin API on a global price list (a refusal fails the job with its code).</summary>
public sealed class PriceListAdminHandler(PriceListAdminService service, ILogger<PriceListAdminHandler> logger) : IJobHandler
{
    public string Kind => BillingJobs.PriceListAdminKind;

    public JobResourceClass ResourceClass => JobResourceClass.System;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            await service.RunAsync(context.Job.Payload, ct).ConfigureAwait(false);
            return JobResult.Done;
        }
        catch (DomainException e)
        {
            logger.LogInformation("price_list.admin_refused {JobId} {Code}", context.Job.Id, e.Code);
            return new JobResult.Fail(e.Code);
        }
    }
}

/// <summary><c>billing.sync_price_list</c>: the catalog of a price list in Stripe, then <c>published</c> and the activation enqueued.</summary>
public sealed class SyncPriceListHandler(StripeCatalogSync sync) : IJobHandler
{
    public string Kind => BillingJobs.SyncPriceListKind;

    public JobResourceClass ResourceClass => JobResourceClass.Io;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var id = BillingJobs.GuidOf(context.Job.Payload, "price_list_id");
        var result = await sync.SyncAsync(id, ct).ConfigureAwait(false);
        return Outcome(result);
    }

    internal static JobResult Outcome(CatalogSyncResult result) => result.ErrorCode switch
    {
        null => JobResult.Done,
        _ when result.Transient => new JobResult.Retry(result.ErrorCode),
        _ => new JobResult.Fail(result.ErrorCode),
    };
}

/// <summary><c>billing.activate_price_list</c> (at <c>valid_from</c>): lookup keys, the market, the previous list retired, the transfer enqueued.</summary>
/// <remarks>Reads through the data source (not the DbContext), so resolving the handlers at the start needs no connection.</remarks>
public sealed class ActivatePriceListHandler(StripeCatalogSync sync, EshopGuardDataSource dataSource, TimeProvider time) : IJobHandler
{
    public string Kind => BillingJobs.ActivatePriceListKind;

    public JobResourceClass ResourceClass => JobResourceClass.Io;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var id = BillingJobs.GuidOf(context.Job.Payload, "price_list_id");
        DateTimeOffset? validFrom;
        await using (var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false))
        await using (var transaction = await BillingSql.BeginGlobalAsync(connection, null, ct).ConfigureAwait(false))
        {
            validFrom = (await BillingSql.ListAsync(transaction, "SELECT valid_from FROM billing.price_lists WHERE id = $1",
                r => (DateTimeOffset?)r.GetFieldValue<DateTimeOffset>(0), ct, id).ConfigureAwait(false)).FirstOrDefault();
        }

        if (validFrom is { } from && from > time.GetUtcNow())
        {
            return new JobResult.Defer(from - time.GetUtcNow(), "not_yet_valid");
        }

        return SyncPriceListHandler.Outcome(await sync.ActivateAsync(id, ct).ConfigureAwait(false));
    }
}

/// <summary>
/// <c>billing.archive_unused_prices</c> (weekly, task 3.8): the prices of retired price lists that no running subscription, open
/// order or scheduled change uses are archived in Stripe (<c>active = false</c>) and the tier gets <c>archived_at</c>.
/// </summary>
public sealed class ArchiveUnusedPricesHandler(EshopGuardDataSource dataSource, IStripeGateway stripe, TimeProvider time, ILogger<ArchiveUnusedPricesHandler> logger)
    : IJobHandler
{
    public string Kind => BillingJobs.ArchiveUnusedPricesKind;

    public JobResourceClass ResourceClass => JobResourceClass.Io;

    public Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct) => RunAsync(ct);

    public async Task<JobResult> RunAsync(CancellationToken ct)
    {
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        List<(Guid Tier, string[] Prices)> candidates;
        List<Guid> tenants;
        await using (var read = await connection.BeginTransactionAsync(ct).ConfigureAwait(false))
        {
            candidates = await BillingSql.ListAsync(read,
                """
                SELECT t.id, array_remove(ARRAY[t.stripe_price_analysis, t.stripe_price_monthly, t.stripe_price_yearly], NULL)
                FROM billing.price_tiers t JOIN billing.price_lists l ON l.id = t.price_list_id
                WHERE l.status = 'retired' AND t.archived_at IS NULL AND t.stripe_price_monthly IS NOT NULL
                """, r => (r.GetGuid(0), r.GetFieldValue<string[]>(1)), ct).ConfigureAwait(false);
            tenants = await BillingSql.ListAsync(read, "SELECT id FROM iam.tenants WHERE stripe_customer_id IS NOT NULL", r => r.GetGuid(0), ct).ConfigureAwait(false);
            await read.CommitAsync(ct).ConfigureAwait(false);
        }

        if (candidates.Count == 0)
        {
            return JobResult.Done;
        }

        var all = candidates.SelectMany(c => c.Prices).ToArray();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tenant in tenants)
        {
            await using var transaction = await TenantSql.BeginAsync(connection, tenant, null, ct).ConfigureAwait(false);
            used.UnionWith(await BillingSql.ListAsync(transaction,
                """
                SELECT stripe_price_id FROM billing.subscriptions WHERE status IN ('trialing', 'active', 'past_due', 'incomplete') AND stripe_price_id = ANY($1)
                UNION SELECT stripe_price_analysis FROM billing.orders WHERE status IN ('created', 'checkout_open') AND stripe_price_analysis = ANY($1)
                UNION SELECT stripe_price_monitoring FROM billing.orders WHERE status IN ('created', 'checkout_open') AND stripe_price_monitoring = ANY($1)
                UNION SELECT "to"->>'stripe_price_id' FROM billing.subscription_changes WHERE status IN ('scheduled', 'notified') AND "to"->>'stripe_price_id' = ANY($1)
                """, r => r.GetString(0), ct, (object)all).ConfigureAwait(false));
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }

        var archived = 0;
        foreach (var (tier, prices) in candidates.Where(c => !c.Prices.Any(used.Contains)))
        {
            try
            {
                foreach (var price in prices)
                {
                    await stripe.SetPriceActiveAsync(price, false, $"archive:{price}", ct).ConfigureAwait(false);
                }
            }
            catch (StripeGatewayException e) when (e.Transient)
            {
                return new JobResult.Retry(e.Code);
            }

            await using var write = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            await BillingSql.ExecuteAsync(write, "UPDATE billing.price_tiers SET archived_at = $2, updated_at = $2 WHERE id = $1", ct, tier, time.GetUtcNow()).ConfigureAwait(false);
            await write.CommitAsync(ct).ConfigureAwait(false);
            archived++;
        }

        logger.LogInformation("billing.prices_archived {Tiers} {Kept}", archived, candidates.Count - archived);
        return JobResult.Done;
    }
}

/// <summary>Enqueues <c>billing.archive_unused_prices</c> once a week (Monday, UTC).</summary>
public sealed class ArchiveUnusedPricesTask(IJobQueue queue) : IScheduledTask
{
    public string Name => BillingJobs.ArchiveUnusedPricesKind;

    public TimeSpan Interval => TimeSpan.FromHours(1);

    public Task RunAsync(ScheduledTaskContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Now.UtcDateTime.DayOfWeek == DayOfWeek.Monday
            ? queue.EnqueueAsync(BillingJobs.ArchiveUnusedPrices(context.Now), context.Transaction, ct)
            : Task.CompletedTask;
    }
}
