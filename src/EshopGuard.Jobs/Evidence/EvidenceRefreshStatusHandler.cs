using System.Text.Json.Nodes;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Notifications;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EshopGuard.Jobs.Evidence;

/// <summary>What one refresh of the evidence of a tenant did.</summary>
public sealed record EvidenceRefreshResult(int Checked, int Reminded, int Expired, int FindingsReopened);

/// <summary>
/// The daily refresh of the evidence of a tenant (change 11, AD 10), in the transaction of the tenant: the states are
/// computed again; a piece of evidence that starts to expire sends <c>evidence_expiring</c> once (<c>reminder_sent_at</c>);
/// one that expired returns its findings <c>kept_with_evidence → open</c> (audit by the system), supersedes the decisions
/// remembered with it and sends <c>evidence_expired</c>. Notifications carry ids and counts only.
/// </summary>
public sealed class EvidenceStatusRefresher(NotificationDispatcher notifications, IOptions<EvidenceOptions> options)
{
    public async Task<EvidenceRefreshResult> RefreshAsync(EshopGuardDb db, NpgsqlTransaction transaction, Guid tenantId, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var settings = options.Value;
        var today = EvidenceStatusCalculator.Today(now, settings.TimeZone);
        var items = await db.EvidenceItems.Where(e => e.DeletedAt == null && e.ValidUntil != null
                && (e.Status == EvidenceStatus.Valid || e.Status == EvidenceStatus.Expiring || e.Status == EvidenceStatus.Expired))
            .OrderBy(e => e.Id).ToListAsync(ct).ConfigureAwait(false);
        int reminded = 0, expired = 0, reopened = 0;
        foreach (var item in items)
        {
            var (status, days) = EvidenceStatusCalculator.Calculate(item.Status, EvidenceStatusCalculator.Date(item.ValidUntil), today, settings.ExpiringDays);
            var route = new JsonObject { ["evidenceId"] = item.Id.ToString("D") };
            if (status == EvidenceStatus.Expiring && item.ReminderSentAt is null)
            {
                await notifications.NotifyAsync(transaction, new NotificationRequest(tenantId, null, NotificationKinds.EvidenceExpiring,
                    new JsonObject { ["evidence_id"] = item.Id.ToString("D"), ["days"] = days }, NotificationRoutes.Evidence, route), ct).ConfigureAwait(false);
                item.ReminderSentAt = now;
                reminded++;
            }

            if (status == EvidenceStatus.Expired && item.Status != EvidenceStatus.Expired)
            {
                var findings = await ReopenAsync(db, tenantId, item.Id, "evidence_expired", now, ct).ConfigureAwait(false);
                await notifications.NotifyAsync(transaction, new NotificationRequest(tenantId, null, NotificationKinds.EvidenceExpired,
                    new JsonObject { ["evidence_id"] = item.Id.ToString("D"), ["findings"] = findings }, NotificationRoutes.Evidence, route), ct).ConfigureAwait(false);
                expired++;
                reopened += findings;
            }

            if (status != item.Status)
            {
                item.Status = status;
                item.UpdatedAt = now;
            }
            else if (item.ReminderSentAt == now)
            {
                item.UpdatedAt = now;
            }
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return new EvidenceRefreshResult(items.Count, reminded, expired, reopened);
    }

    /// <summary>
    /// The findings kept with a piece of evidence (and no other valid one) go back to <c>open</c> (audit <c>finding.status_changed</c> by the system) and
    /// the decisions remembered with it are superseded; returns how many findings were reopened.
    /// </summary>
    public static async Task<int> ReopenAsync(EshopGuardDb db, Guid tenantId, Guid evidenceId, string reasonCode, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var linked = db.EvidenceLinks.Where(l => l.EvidenceId == evidenceId && l.FindingId != null).Select(l => l.FindingId!.Value);

        // A finding another valid piece of evidence holds stays kept.
        var heldElsewhere = db.EvidenceLinks.Where(l => l.EvidenceId != evidenceId && l.FindingId != null
            && db.EvidenceItems.Any(e => e.Id == l.EvidenceId && e.DeletedAt == null && (e.Status == EvidenceStatus.Valid || e.Status == EvidenceStatus.Expiring)))
            .Select(l => l.FindingId!.Value);
        var findings = await db.Findings.Where(f => linked.Contains(f.Id) && !heldElsewhere.Contains(f.Id) && f.Status == FindingStatus.KeptWithEvidence)
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var finding in findings)
        {
            finding.Status = FindingStatus.Open;
            finding.UpdatedAt = now;
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO ops.audit_log (at, tenant_id, actor_user_id, actor_kind, action, entity_type, entity_id, data, created_at)
                VALUES ({now}, {tenantId}, NULL, 'system', 'finding.status_changed', 'finding', {finding.Id.ToString("D")},
                    jsonb_build_object('from', 'kept_with_evidence', 'to', 'open', 'reasonCode', {reasonCode}::text), {now})
                """, ct).ConfigureAwait(false);
        }

        await db.DecisionMemory.Where(m => m.EvidenceId == evidenceId && m.SupersededAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.SupersededAt, now).SetProperty(m => m.UpdatedAt, now), ct).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return findings.Count;
    }
}

/// <summary>The job <c>evidence.refresh_status</c> of one tenant (daily, class <c>system</c>).</summary>
public sealed class EvidenceRefreshStatusHandler(EvidenceStatusRefresher refresher, TimeProvider time, ILogger<EvidenceRefreshStatusHandler> logger) : IJobHandler
{
    public const string JobKind = "evidence.refresh_status";

    public string Kind => JobKind;

    public JobResourceClass ResourceClass => JobResourceClass.System;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var tenantId = context.Job.TenantId ?? throw new InvalidOperationException("evidence.refresh_status without a tenant");
        EvidenceRefreshResult? result = null;
        await context.CompleteAsync(async tx => result = await refresher.RefreshAsync(tx.Db, tx.Transaction, tenantId, time.GetUtcNow(), ct).ConfigureAwait(false), ct)
            .ConfigureAwait(false);
        logger.LogInformation("evidence.refreshed {TenantId} {Checked} {Reminded} {Expired} {Reopened}",
            tenantId, result!.Checked, result.Reminded, result.Expired, result.FindingsReopened);
        return JobResult.Done;
    }
}

/// <summary>
/// Every hour of the scheduler: the job <c>evidence.refresh_status</c> of every active tenant once a day (dedupe key with the
/// tenant and the UTC date). <c>iam.tenants</c> has no RLS; the evidence is read only by the job of its tenant.
/// </summary>
public sealed class EvidenceRefreshTask(IJobQueue queue) : IScheduledTask
{
    public string Name => EvidenceRefreshStatusHandler.JobKind;

    public TimeSpan Interval => TimeSpan.FromHours(1);

    public async Task RunAsync(ScheduledTaskContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var tenants = new List<Guid>();
        await using (var select = new NpgsqlCommand("SELECT id FROM iam.tenants WHERE status = 'active' ORDER BY id", context.Transaction.Connection, context.Transaction))
        await using (var reader = await select.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                tenants.Add(reader.GetGuid(0));
            }
        }

        foreach (var tenantId in tenants)
        {
            await queue.EnqueueAsync(new JobRequest(EvidenceRefreshStatusHandler.JobKind, JobResourceClass.System, JobPriority.P4, JobRequest.EmptyPayload(),
                TenantId: tenantId, DedupeKey: DailySystemJobTask.DedupeKey($"{EvidenceRefreshStatusHandler.JobKind}:{tenantId:N}", context.Now)), context.Transaction, ct)
                .ConfigureAwait(false);
        }
    }
}

/// <summary>Registration of the jobs of the evidence (worker).</summary>
public static class EvidenceJobsServiceCollectionExtensions
{
    public static IServiceCollection AddEvidenceJobs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddEvidenceOptions();
        services.AddNotifications();
        services.AddSingleton<EvidenceStatusRefresher>();
        services.AddJobHandler<EvidenceRefreshStatusHandler>();
        services.Add(ServiceDescriptor.Singleton<IScheduledTask, EvidenceRefreshTask>());
        return services;
    }

    /// <summary><c>Evidence</c> with the time zone of <c>Localization:TimeZone</c> (API and worker).</summary>
    public static IServiceCollection AddEvidenceOptions(this IServiceCollection services)
    {
        services.AddOptions<EvidenceOptions>().BindConfiguration(EvidenceOptions.SectionName)
            .Configure<Microsoft.Extensions.Configuration.IConfiguration>((o, c) => o.TimeZone = c["Localization:TimeZone"] ?? o.TimeZone);
        return services;
    }
}
