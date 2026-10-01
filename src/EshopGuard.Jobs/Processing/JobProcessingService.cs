using System.Collections.Concurrent;
using System.Reflection;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Jobs.Processing;

/// <summary>
/// The worker's job processing: registers the worker in <c>ops.workers</c>, runs one claiming loop per resource class with
/// slots, runs each job in its own DI scope with a heartbeat and maps the handler's result to the queue. On shutdown it stops
/// claiming, marks itself draining, lets running jobs finish within <see cref="WorkerOptions.DrainTimeout"/>, returns the rest
/// to the queue without counting the attempt and removes its row.
/// </summary>
public sealed class JobProcessingService : IHostedService, IAsyncDisposable
{
    /// <summary>The longest wait in a paused class before it looks again.</summary>
    private static readonly TimeSpan PausedWait = TimeSpan.FromSeconds(1);

    private readonly IJobQueue _queue;
    private readonly IWorkerStore _store;
    private readonly JobHandlerRegistry _registry;
    private readonly JobNotificationHub _hub;
    private readonly IServiceScopeFactory _scopes;
    private readonly WorkerOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<JobProcessingService> _logger;

    private readonly CancellationTokenSource _claimCts = new();
    private readonly CancellationTokenSource _jobsCts = new();
    private readonly CancellationTokenSource _registryCts = new();
    private readonly ConcurrentDictionary<RunningJob, byte> _running = new();
    private readonly List<Task> _loops = [];
    private WorkerRegistration? _registration;
    private Task _registryHeartbeat = Task.CompletedTask;
    private volatile bool _draining;
    private volatile bool _crashed;
    private volatile bool _heartbeatsSuspended;
    private int _stopped;
    private int _disposed;

    /// <summary>Creates the service (the registry checks the handlers here, so a duplicate kind fails the start).</summary>
    public JobProcessingService(
        IJobQueue queue,
        IWorkerStore store,
        JobHandlerRegistry registry,
        JobNotificationHub hub,
        IServiceScopeFactory scopes,
        IOptions<WorkerOptions> options,
        TimeProvider time,
        ILogger<JobProcessingService> logger)
    {
        _queue = queue;
        _store = store;
        _registry = registry;
        _hub = hub;
        _scopes = scopes;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    /// <summary>Id of this worker.</summary>
    public string WorkerId => _options.EffectiveId;

    /// <summary>Number of jobs running now.</summary>
    public int RunningCount => _running.Count;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var slots = JobNames.ResourceClasses.ToDictionary(c => c, c => _options.Slots[c]);
        _registration = new WorkerRegistration(WorkerId, Version(), slots);
        await _store.RegisterAsync(_registration, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("worker.started {WorkerId}", WorkerId);

        foreach (var (resourceClass, count) in slots.Where(s => s.Value > 0))
        {
            _loops.Add(Task.Run(() => ClaimLoopAsync(resourceClass, count, _claimCts.Token), CancellationToken.None));
        }

        _registryHeartbeat = Task.Run(() => RegistryHeartbeatAsync(_registryCts.Token), CancellationToken.None);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 1 || _crashed)
        {
            return;
        }

        var deadline = _time.GetUtcNow() + _options.DrainTimeout;

        // 1. No new jobs.
        _draining = true;
        await _claimCts.CancelAsync().ConfigureAwait(false);
        await WhenAllQuietly(_loops).ConfigureAwait(false);
        await TryAsync(() => _store.HeartbeatAsync(WorkerId, draining: true, CancellationToken.None), "worker.heartbeat_failed").ConfigureAwait(false);

        // 2. Running jobs may finish until the deadline.
        await WaitForRunningAsync(deadline - _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        // 3. The rest is canceled and goes back to the queue without counting the attempt.
        if (!_running.IsEmpty)
        {
            _logger.LogWarning("worker.shutdown_returning_jobs {WorkerId} {Count}", WorkerId, _running.Count);
            await _jobsCts.CancelAsync().ConfigureAwait(false);
            await WaitForRunningAsync(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            foreach (var running in _running.Keys)
            {
                if (running.TryBeginFinish())
                {
                    await ReturnOnShutdownAsync(running.Job).ConfigureAwait(false);
                }
            }
        }

        await _registryCts.CancelAsync().ConfigureAwait(false);
        await WhenAllQuietly([_registryHeartbeat]).ConfigureAwait(false);
        await TryAsync(() => _store.UnregisterAsync(WorkerId, CancellationToken.None), "worker.unregister_failed").ConfigureAwait(false);
        _logger.LogInformation("worker.stopped {WorkerId}", WorkerId);
    }

    /// <summary>
    /// Test hook: behaves like a killed process. Claiming, heartbeats of jobs and of the worker stop, handlers are canceled
    /// and nothing more is written: leases stay until they expire, the row in <c>ops.workers</c> stays.
    /// </summary>
    internal async Task SimulateCrashAsync()
    {
        _crashed = true;
        _heartbeatsSuspended = true;
        await _claimCts.CancelAsync().ConfigureAwait(false);
        await _registryCts.CancelAsync().ConfigureAwait(false);
        await _jobsCts.CancelAsync().ConfigureAwait(false);
        await WhenAllQuietly([.. _loops, _registryHeartbeat, .. _running.Keys.Select(r => r.Task)]).ConfigureAwait(false);
    }

    /// <summary>Test hook: running jobs stop sending heartbeats (a stuck worker), everything else goes on.</summary>
    internal void SuspendJobHeartbeats() => _heartbeatsSuspended = true;

    private async Task ClaimLoopAsync(JobResourceClass resourceClass, int slots, CancellationToken ct)
    {
        var signal = _hub.Signal(resourceClass);
        var minPoll = TimeSpan.FromMilliseconds(_options.MinPollMilliseconds);
        var maxPoll = TimeSpan.FromSeconds(_options.MaxPollSeconds);
        var poll = minPoll;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if ((await _queue.GetPausedClassesAsync(ct).ConfigureAwait(false)).Contains(resourceClass))
                {
                    await signal.WaitAsync(PausedWait < maxPoll ? PausedWait : maxPoll, ct).ConfigureAwait(false);
                    continue;
                }

                var free = slots - _running.Keys.Count(r => r.Job.ResourceClass == resourceClass);
                if (free <= 0)
                {
                    // A finished job sets the signal.
                    await signal.WaitAsync(maxPoll, ct).ConfigureAwait(false);
                    continue;
                }

                var jobs = await _queue.ClaimAsync(resourceClass, free, WorkerId, ct).ConfigureAwait(false);
                foreach (var job in jobs)
                {
                    Start(job, signal);
                }

                if (jobs.Count == free)
                {
                    poll = minPoll;
                    continue;
                }

                poll = jobs.Count > 0 ? minPoll : Min(poll * 2, maxPoll);
                if (await signal.WaitAsync(poll, ct).ConfigureAwait(false))
                {
                    poll = minPoll;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("jobs.claim_failed {ResourceClass} {ExceptionType}", resourceClass.ToDb(), ex.GetType().Name);
                try
                {
                    await Task.Delay(maxPoll, _time, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private void Start(ClaimedJob job, AsyncSignal signal)
    {
        var running = new RunningJob(job, CancellationTokenSource.CreateLinkedTokenSource(_jobsCts.Token));
        _running[running] = 0;
        running.Task = Task.Run(async () =>
        {
            try
            {
                await ExecuteAsync(running).ConfigureAwait(false);
            }
            finally
            {
                _running.TryRemove(running, out _);
                running.Cancellation.Dispose();
                signal.Set();
            }
        });
    }

    private async Task ExecuteAsync(RunningJob running)
    {
        var job = running.Job;
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            if (job.TenantId is { } tenantId)
            {
                scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
            }

            var context = new JobExecutionContext(job, scope.ServiceProvider, _queue, WorkerId);
            var handler = _registry.Resolve(job.Kind, scope.ServiceProvider);
            if (handler is not null && handler.ResourceClass != job.ResourceClass)
            {
                _logger.LogWarning("job.resource_class_mismatch {JobId} {Kind} {ResourceClass}", job.Id, job.Kind, job.ResourceClass.ToDb());
            }

            JobResult? result = null;
            Exception? error = null;
            using (var heartbeatCts = new CancellationTokenSource())
            {
                var heartbeat = HeartbeatAsync(running, context, heartbeatCts.Token);
                try
                {
                    if (handler is not null)
                    {
                        result = await handler.ExecuteAsync(context, running.Cancellation.Token).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    await heartbeatCts.CancelAsync().ConfigureAwait(false);
                    await WhenAllQuietly([heartbeat]).ConfigureAwait(false);
                }
            }

            await FinishAsync(running, context, handler, result, error, scope.ServiceProvider).ConfigureAwait(false);
        }
        catch (LeaseLostException)
        {
            _logger.LogWarning("{Code} {JobId} {Attempt}", JobErrorCodes.LeaseLost, job.Id, job.Attempt);
        }
        catch (Exception ex)
        {
            // The job stays running until its lease expires; the reaper returns it.
            _logger.LogError("job.finish_failed {JobId} {Kind} {ExceptionType} {StackTrace}", job.Id, job.Kind, ex.GetType().FullName, ex.StackTrace);
        }
    }

    private async Task FinishAsync(RunningJob running, JobExecutionContext context, IJobHandler? handler, JobResult? result, Exception? error, IServiceProvider services)
    {
        var job = running.Job;
        if (_crashed || !running.TryBeginFinish())
        {
            return;
        }

        if (running.LeaseLost || error is LeaseLostException)
        {
            _logger.LogWarning("{Code} {JobId} {Attempt}", JobErrorCodes.LeaseLost, job.Id, job.Attempt);
            return;
        }

        if (handler is null)
        {
            await _queue.FailAsync(job, new JobError(JobErrorCodes.UnknownKind, null, job.Kind), permanent: true).ConfigureAwait(false);
            _logger.LogError("{Code} {JobId} {Kind}", JobErrorCodes.UnknownKind, job.Id, job.Kind);
            return;
        }

        if (error is OperationCanceledException && running.Cancellation.IsCancellationRequested && _draining)
        {
            await ReturnOnShutdownAsync(job).ConfigureAwait(false);
            return;
        }

        if (error is not null)
        {
            var state = await _queue.FailAsync(job, new JobError(JobErrorCodes.Unhandled, error.GetType().FullName), permanent: false).ConfigureAwait(false);
            // Type and stack only: the message of an exception may quote a page text.
            _logger.LogError("{Code} {JobId} {Kind} {Attempt} {State} {ExceptionType} {StackTrace}", JobErrorCodes.Unhandled, job.Id, job.Kind, job.Attempt, state, error.GetType().FullName, error.StackTrace);
            return;
        }

        if (context.Finished)
        {
            if (result is not JobResult.Succeeded)
            {
                _logger.LogWarning("job.result_ignored {JobId} {Kind} {Result}", job.Id, job.Kind, result?.GetType().Name);
            }

            return;
        }

        switch (result)
        {
            case JobResult.Succeeded or null:
                await _queue.CompleteAsync(job, services.GetRequiredService<EshopGuardDb>(), null).ConfigureAwait(false);
                break;
            case JobResult.Retry retry:
                var retried = await _queue.FailAsync(job, new JobError(retry.Code), permanent: false, retry.After).ConfigureAwait(false);
                LogFailure(job, retry.Code, retried);
                break;
            case JobResult.Fail fail:
                await _queue.FailAsync(job, new JobError(fail.Code), permanent: true).ConfigureAwait(false);
                LogFailure(job, fail.Code, JobState.Failed);
                break;
            case JobResult.Defer defer:
                await _queue.DeferAsync(job, defer.Delay, defer.Code).ConfigureAwait(false);
                break;
            case JobResult.PauseClass pause:
                await _queue.PauseResourceClassAsync(job.ResourceClass, pause.Code).ConfigureAwait(false);
                await _queue.DeferAsync(job, TimeSpan.Zero, pause.Code).ConfigureAwait(false);
                break;
            case JobResult.Canceled:
                await _queue.MarkCanceledAsync(job).ConfigureAwait(false);
                _logger.LogInformation("job.canceled {JobId} {Kind}", job.Id, job.Kind);
                break;
        }
    }

    private void LogFailure(ClaimedJob job, string code, JobState state)
    {
        if (state == JobState.Failed)
        {
            _logger.LogError("job.failed {JobId} {Kind} {Attempt} {Code}", job.Id, job.Kind, job.Attempt, code);
        }
        else
        {
            _logger.LogWarning("job.retry {JobId} {Kind} {Attempt} {Code}", job.Id, job.Kind, job.Attempt, code);
        }
    }

    private async Task HeartbeatAsync(RunningJob running, JobExecutionContext context, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.HeartbeatSeconds), _time);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                if (_heartbeatsSuspended)
                {
                    continue;
                }

                try
                {
                    var beat = await _queue.HeartbeatAsync(running.Job, ct).ConfigureAwait(false);
                    if (!beat.LeaseHeld)
                    {
                        running.LeaseLost = true;
                        _logger.LogWarning("{Code} {JobId} {Attempt}", JobErrorCodes.LeaseLost, running.Job.Id, running.Job.Attempt);
                        await running.Cancellation.CancelAsync().ConfigureAwait(false);
                        return;
                    }

                    if (beat.CancelRequested)
                    {
                        context.MarkRunCancelRequested();
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One missed heartbeat is harmless (a third of the lease); the lease decides.
                    _logger.LogWarning("job.heartbeat_failed {JobId} {ExceptionType}", running.Job.Id, ex.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task RegistryHeartbeatAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.RegistryHeartbeatSeconds), _time);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    if (!await _store.HeartbeatAsync(WorkerId, _draining, ct).ConfigureAwait(false) && _registration is { } registration)
                    {
                        // The row was deleted as stale (e.g. after a long pause of the process).
                        await _store.RegisterAsync(registration, ct).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning("worker.heartbeat_failed {WorkerId} {ExceptionType}", WorkerId, ex.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task ReturnOnShutdownAsync(ClaimedJob job)
    {
        try
        {
            await _queue.DeferAsync(job, TimeSpan.Zero, JobErrorCodes.WorkerShutdown).ConfigureAwait(false);
            _logger.LogWarning("{Code} {JobId} {Kind}", JobErrorCodes.WorkerShutdown, job.Id, job.Kind);
        }
        catch (LeaseLostException)
        {
            _logger.LogWarning("{Code} {JobId} {Attempt}", JobErrorCodes.LeaseLost, job.Id, job.Attempt);
        }
        catch (Exception ex)
        {
            // The lease expires and the reaper returns the job.
            _logger.LogError("worker.shutdown_return_failed {JobId} {ExceptionType}", job.Id, ex.GetType().Name);
        }
    }

    private async Task WaitForRunningAsync(TimeSpan timeout, CancellationToken ct)
    {
        var tasks = _running.Keys.Select(r => r.Task).ToArray();
        if (tasks.Length == 0 || timeout <= TimeSpan.Zero)
        {
            return;
        }

        try
        {
            await Task.WhenAll(tasks).WaitAsync(timeout, _time, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task TryAsync(Func<Task> action, string code)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("{Code} {WorkerId} {ExceptionType}", code, WorkerId, ex.GetType().Name);
        }
    }

    private static async Task WhenAllQuietly(IEnumerable<Task> tasks)
    {
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch
        {
            // Failures of loops and heartbeats are logged where they happen.
        }
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private static string? Version() =>
        typeof(JobProcessingService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Registered as a singleton and as a hosted service, so the container disposes it twice.
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _claimCts.CancelAsync().ConfigureAwait(false);
        await _registryCts.CancelAsync().ConfigureAwait(false);
        _claimCts.Dispose();
        _jobsCts.Dispose();
        _registryCts.Dispose();
    }

    private sealed class RunningJob(ClaimedJob job, CancellationTokenSource cancellation)
    {
        private int _finishing;
        private volatile bool _leaseLost;

        public ClaimedJob Job { get; } = job;

        public CancellationTokenSource Cancellation { get; } = cancellation;

        public Task Task { get; set; } = Task.CompletedTask;

        /// <summary>The heartbeat found another owner; nothing of this attempt may be written.</summary>
        public bool LeaseLost
        {
            get => _leaseLost;
            set => _leaseLost = value;
        }

        /// <summary>Only one of the job's own finish and the shutdown's return writes the outcome.</summary>
        public bool TryBeginFinish() => Interlocked.Exchange(ref _finishing, 1) == 0;
    }
}
