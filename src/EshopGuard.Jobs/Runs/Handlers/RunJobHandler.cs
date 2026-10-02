using EshopGuard.Core.Fix;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Options;
using EshopGuard.Core.Rules;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Notifications;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Queue;
using EshopGuard.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EshopGuard.Jobs.Runs.Handlers;

/// <summary>What every handler of a run needs.</summary>
public sealed class RunHandlerContext(
    EshopGuardDataSource dataSource,
    IJobQueue queue,
    IBlobStore blobs,
    UsageRecorder usage,
    IOptions<RunsOptions> runs,
    IOptions<EshopGuardOptions> guard,
    TimeProvider time,
    ILoggerFactory loggers,
    NotificationDispatcher notifications)
{
    public EshopGuardDataSource DataSource { get; } = dataSource;

    public IJobQueue Queue { get; } = queue;

    public IBlobStore Blobs { get; } = blobs;

    public UsageRecorder Usage { get; } = usage;

    public RunsOptions Runs => runs.Value;

    public EshopGuardOptions Guard => guard.Value;

    public TimeProvider Time { get; } = time;

    public ILoggerFactory Loggers { get; } = loggers;

    public NotificationDispatcher Notifications { get; } = notifications;
}

/// <summary>One job of a run being handled.</summary>
public sealed class RunJob(JobExecutionContext context, RunRow run, RunAmbientScope scope)
{
    public JobExecutionContext Context { get; } = context;

    /// <summary>The run as it was when the job started (the completion reads it again under a lock).</summary>
    public RunRow Run { get; } = run;

    public RunAmbientScope Scope { get; } = scope;

    public ClaimedJob Job => Context.Job;
}

/// <summary>
/// Base of the handlers of the steps of a run. Before the step: the run must exist, be in a state of the step and not be
/// canceled (a canceled run ends <c>canceled</c> here). During the step: the tenant, e-shop and run of the job are the
/// context of the stores (<see cref="RunAmbient"/>). After it: the usage gathered is written. Errors: invalid rule sets end
/// the run <c>failed</c>; a fatal error of Jev or OpenAI (key, credit) pauses the class of jobs without using an attempt
/// (change 4) with the event <c>run.paused_internal</c>; other exceptions go to the queue, which repeats the batch; on the
/// last attempt of the job they end the run <c>failed</c> with <c>internal_error</c>, so no run is left unfinished
/// (<see cref="StepErrorPolicy"/>).
/// </summary>
public abstract class RunJobHandler(RunHandlerContext context) : IJobHandler
{
    private readonly ILogger _logger = context.Loggers.CreateLogger("EshopGuard.Jobs.Runs");

    protected RunHandlerContext Ctx { get; } = context;

    protected ILogger Logger => _logger;

    public abstract string Kind { get; }

    public abstract JobResourceClass ResourceClass { get; }

    /// <summary>States in which the step runs; in any other the job ends without doing anything (repeated or late job).</summary>
    protected abstract IReadOnlyCollection<RunStatus> Statuses { get; }

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        var job = context.Job;
        if (job.RunId is not { } runId || job.TenantId is not { } tenantId || job.ShopId is not { } shopId)
        {
            return new JobResult.Fail("run.job_without_run");
        }

        var run = await InTenantAsync(tenantId, (c, t) => RunStore.LoadAsync(c, t, runId, forUpdate: false, ct), ct).ConfigureAwait(false);
        if (run is null)
        {
            return new JobResult.Fail(RunCodes.RunNotFound);
        }

        if (run.IsFinal)
        {
            return run.Status == RunStatus.Canceled ? new JobResult.Canceled() : JobResult.Done;
        }

        if (run.CancelRequested)
        {
            await CancelRunAsync(context, run, ct).ConfigureAwait(false);
            return JobResult.Done;
        }

        if (!Statuses.Contains(run.Status))
        {
            _logger.LogInformation("run.step_skipped {RunId} {TenantId} {JobId} {Kind} {Status}", runId, tenantId, job.Id, Kind, run.Status);
            return JobResult.Done;
        }

        var scope = new RunAmbientScope(tenantId, shopId, runId, job.Id);
        using var ambient = RunAmbient.Enter(scope);
        using var logScope = _logger.BeginScope(new Dictionary<string, object> { ["tenant_id"] = tenantId, ["run_id"] = runId, ["job_id"] = job.Id });
        try
        {
            return await RunAsync(new RunJob(context, run, scope), ct).ConfigureAwait(false);
        }
        catch (RuleValidationException)
        {
            _logger.LogError("run.rules_invalid {RunId} {TenantId} {JobId}", runId, tenantId, job.Id);
            await FailRunAsync(context, run.Id, RunCodes.RulesInvalid, ct).ConfigureAwait(false);
            return JobResult.Done;
        }
        catch (JevApiException ex) when (ex.IsFatal)
        {
            return await PauseAsync(context, run, "jev.fatal_" + ex.StatusCode, ct).ConfigureAwait(false);
        }
        catch (RewriteApiException ex) when (ex.IsFatal)
        {
            return await PauseAsync(context, run, "llm.fatal_" + ex.StatusCode, ct).ConfigureAwait(false);
        }
        catch (TransientStepException ex) when (!StepErrorPolicy.IsLastAttempt(job))
        {
            _logger.LogWarning("run.step_retry {RunId} {TenantId} {JobId} {Kind} {Attempt} {Code}", runId, tenantId, job.Id, Kind, job.Attempt, ex.Code);
            return new JobResult.Retry(ex.Code, ex.RetryAfter);
        }
        catch (Exception ex) when (StepErrorPolicy.IsLastAttempt(job) && ex is not (OperationCanceledException or LeaseLostException))
        {
            // The queue would fail the job and the run would wait for it forever.
            _logger.LogError("run.step_exhausted {RunId} {TenantId} {JobId} {Kind} {Attempt} {ErrorKind} {ExceptionType}",
                runId, tenantId, job.Id, Kind, job.Attempt, StepErrorPolicy.Of(ex), ex.GetType().Name);
            await FailRunAsync(context, run.Id, RunCodes.InternalError, ct).ConfigureAwait(false);
            return JobResult.Done;
        }
        finally
        {
            try
            {
                await Ctx.Usage.FlushAsync(scope, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException)
            {
                _logger.LogError("run.usage_write_failed {RunId} {TenantId} {JobId} {ExceptionType}", runId, tenantId, job.Id, ex.GetType().Name);
            }
        }
    }

    /// <summary>The step itself.</summary>
    protected abstract Task<JobResult> RunAsync(RunJob job, CancellationToken ct);

    /// <summary>
    /// Writes the result of the step and completes the job in one transaction. The run row is locked first (the barrier of
    /// the steps); a run that ended meanwhile gets nothing, a run whose cancellation was requested ends <c>canceled</c>.
    /// </summary>
    protected Task CompleteAsync(RunJob job, Func<JobTransaction, RunRow, Task> write, CancellationToken ct) =>
        job.Context.CompleteAsync(async tx =>
        {
            var run = await RunStore.LoadAsync(tx.Connection, tx.Transaction, job.Run.Id, forUpdate: true, ct).ConfigureAwait(false);
            if (run is null || run.IsFinal)
            {
                return;
            }

            if (run.CancelRequested)
            {
                await RunTransitions.CancelAsync(tx.Connection, tx.Transaction, Ctx.Queue, run, ct).ConfigureAwait(false);
                return;
            }

            await write(tx, run).ConfigureAwait(false);
        }, ct);

    /// <summary>Ends the run <c>failed</c> with the code and completes the job.</summary>
    protected async Task<JobResult> FailRunAsync(RunJob job, string code, CancellationToken ct)
    {
        _logger.LogWarning("run.failed {RunId} {TenantId} {JobId} {Code}", job.Run.Id, job.Run.TenantId, job.Job.Id, code);
        await FailRunAsync(job.Context, job.Run.Id, code, ct).ConfigureAwait(false);
        return JobResult.Done;
    }

    /// <summary>Work in a transaction of the tenant outside the completion (reads, events that must be seen at once).</summary>
    protected async Task<T> InTenantAsync<T>(Guid tenantId, Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> work, CancellationToken ct)
    {
        await using var connection = await Ctx.DataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, ct: ct).ConfigureAwait(false);
        var result = await work(connection, transaction).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc cref="InTenantAsync{T}"/>
    protected Task InTenantAsync(Guid tenantId, Func<NpgsqlConnection, NpgsqlTransaction, Task> work, CancellationToken ct) =>
        InTenantAsync(tenantId, async (c, t) =>
        {
            await work(c, t).ConfigureAwait(false);
            return true;
        }, ct);

    /// <summary>Moves the run between two states in its own transaction (the start of a step the user should see).</summary>
    protected Task<bool> TransitionNowAsync(RunRow run, RunStatus from, RunStatus to, CancellationToken ct) =>
        InTenantAsync(run.TenantId, (c, t) => RunStateMachine.TryTransitionAsync(c, t, run.TenantId, run.Id, from, to, ct), ct);

    private Task FailRunAsync(JobExecutionContext context, Guid runId, string code, CancellationToken ct) =>
        context.CompleteAsync(async tx =>
        {
            if (await RunStore.LoadAsync(tx.Connection, tx.Transaction, runId, forUpdate: true, ct).ConfigureAwait(false) is { IsFinal: false } run)
            {
                await RunTransitions.FailAsync(tx.Connection, tx.Transaction, Ctx.Queue, run, code, ct).ConfigureAwait(false);
            }
        }, ct);

    private Task CancelRunAsync(JobExecutionContext context, RunRow run, CancellationToken ct) =>
        context.CompleteAsync(async tx =>
        {
            if (await RunStore.LoadAsync(tx.Connection, tx.Transaction, run.Id, forUpdate: true, ct).ConfigureAwait(false) is { IsFinal: false } locked)
            {
                await RunTransitions.CancelAsync(tx.Connection, tx.Transaction, Ctx.Queue, locked, ct).ConfigureAwait(false);
            }
        }, ct);

    private async Task<JobResult> PauseAsync(JobExecutionContext context, RunRow run, string code, CancellationToken ct)
    {
        _logger.LogError("run.paused_internal {RunId} {TenantId} {JobId} {Kind} {Code}", run.Id, run.TenantId, context.Job.Id, Kind, code);
        await InTenantAsync(run.TenantId, (c, t) => RunEventWriter.WriteAsync(c, t, run.TenantId, run.Id, "warning", RunCodes.EventPausedInternal, new System.Text.Json.Nodes.JsonObject(), ct), ct).ConfigureAwait(false);
        return new JobResult.PauseClass(code);
    }
}
