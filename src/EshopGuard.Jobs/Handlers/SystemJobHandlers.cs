using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Maintenance;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Queue;
using Microsoft.Extensions.Logging;

namespace EshopGuard.Jobs.Handlers;

/// <summary>Kinds of the maintenance jobs.</summary>
public static class SystemJobKinds
{
    /// <summary>Creates the monthly partitions ahead.</summary>
    public const string EnsurePartitions = "system.ensure_partitions";

    /// <summary>Deletes finished jobs.</summary>
    public const string CleanupJobs = "system.cleanup_jobs";
}

/// <summary><c>system.ensure_partitions</c>: partitions for this month and three more (<see cref="PartitionMaintainer"/>).</summary>
public sealed class EnsurePartitionsHandler(PartitionMaintainer maintainer) : IJobHandler
{
    /// <summary>Months ahead.</summary>
    public const int MonthsAhead = 3;

    /// <inheritdoc />
    public string Kind => SystemJobKinds.EnsurePartitions;

    /// <inheritdoc />
    public JobResourceClass ResourceClass => JobResourceClass.System;

    /// <inheritdoc />
    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        await maintainer.EnsureMonthlyPartitionsAsync(MonthsAhead, ct).ConfigureAwait(false);
        return JobResult.Done;
    }
}

/// <summary>
/// <c>system.cleanup_jobs</c>: deletes succeeded and canceled jobs older than 7 days and failed ones older than 30 days
/// (K rozhodnutí 3), in batches of 10 000 until nothing is left.
/// </summary>
public sealed class CleanupJobsHandler(IJobQueue queue, ILogger<CleanupJobsHandler> logger) : IJobHandler
{
    /// <summary>Age of deleted succeeded and canceled jobs.</summary>
    public static readonly TimeSpan SucceededAge = TimeSpan.FromDays(7);

    /// <summary>Age of deleted failed jobs.</summary>
    public static readonly TimeSpan FailedAge = TimeSpan.FromDays(30);

    /// <summary>Jobs per delete statement.</summary>
    public const int BatchSize = 10_000;

    /// <inheritdoc />
    public string Kind => SystemJobKinds.CleanupJobs;

    /// <inheritdoc />
    public JobResourceClass ResourceClass => JobResourceClass.System;

    /// <inheritdoc />
    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        var total = 0;
        int deleted;
        do
        {
            deleted = await queue.DeleteFinishedAsync(SucceededAge, FailedAge, BatchSize, ct).ConfigureAwait(false);
            total += deleted;
        }
        while (deleted > 0);

        logger.LogInformation("jobs.cleaned {Deleted}", total);
        return JobResult.Done;
    }
}
