using EshopGuard.Data;
using EshopGuard.Jobs.Queue;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Jobs.Processing;

/// <summary>What a handler gets for one job: the job, its DI scope (tenant set) and the ways to finish it.</summary>
public sealed class JobExecutionContext
{
    private readonly IJobQueue _queue;
    private volatile bool _runCancelRequested;
    private int _finished;

    internal JobExecutionContext(ClaimedJob job, IServiceProvider services, IJobQueue queue, string workerId)
    {
        Job = job;
        Services = services;
        _queue = queue;
        WorkerId = workerId;
    }

    /// <summary>The job.</summary>
    public ClaimedJob Job { get; }

    /// <summary>Services of the job's scope (<c>ITenantContext</c> set to the job's tenant).</summary>
    public IServiceProvider Services { get; }

    /// <summary>Worker running the job.</summary>
    public string WorkerId { get; }

    /// <summary>True after <see cref="CompleteAsync"/> or <see cref="DeferAsync"/>: the worker does not finish the job again.</summary>
    internal bool Finished => Volatile.Read(ref _finished) == 1;

    /// <summary>
    /// Writes the results and marks the job <c>succeeded</c> in one transaction (the scope's <see cref="EshopGuardDb"/>).
    /// Throws <see cref="LeaseLostException"/> when another worker owns the job now; then nothing is written.
    /// </summary>
    public async Task CompleteAsync(Func<JobTransaction, Task> writeResults, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(writeResults);
        EnsureNotFinished();
        await _queue.CompleteAsync(Job, Services.GetRequiredService<EshopGuardDb>(), writeResults, ct).ConfigureAwait(false);
        Volatile.Write(ref _finished, 1);
    }

    /// <summary>Returns the job to the queue after <paramref name="delay"/> without counting the attempt.</summary>
    public async Task DeferAsync(TimeSpan delay, string code, CancellationToken ct = default)
    {
        EnsureNotFinished();
        await _queue.DeferAsync(Job, delay, code, ct).ConfigureAwait(false);
        Volatile.Write(ref _finished, 1);
    }

    /// <summary>
    /// Whether the job's run was canceled (seen by the last heartbeat, otherwise read now). A handler checks it between
    /// partial batches and returns <see cref="JobResult.Canceled"/>. A job without a run is never canceled this way.
    /// </summary>
    public async Task<bool> IsRunCancellationRequestedAsync(CancellationToken ct = default)
    {
        if (Job.RunId is not { } runId || Job.TenantId is not { } tenantId)
        {
            return false;
        }

        if (!_runCancelRequested && await _queue.IsRunCancelRequestedAsync(tenantId, runId, ct).ConfigureAwait(false))
        {
            _runCancelRequested = true;
        }

        return _runCancelRequested;
    }

    /// <summary>Set by the heartbeat when the run's cancellation was requested.</summary>
    internal void MarkRunCancelRequested() => _runCancelRequested = true;

    private void EnsureNotFinished()
    {
        if (Finished)
        {
            throw new InvalidOperationException($"Job {Job.Id} is already completed or deferred.");
        }
    }
}
