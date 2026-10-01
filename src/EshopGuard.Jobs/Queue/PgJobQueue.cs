using System.Data.Common;
using System.Text;
using System.Text.Json;
using EshopGuard.Data;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Jobs.Queue;

/// <summary><see cref="IJobQueue"/> over <c>ops.jobs</c> through the host's connection pool (API: enqueue and cancel only).</summary>
public sealed class PgJobQueue(
    EshopGuardDataSource dataSource,
    IServiceScopeFactory scopes,
    IOptions<WorkerOptions> workerOptions,
    IOptions<JobsOptions> jobsOptions,
    TimeProvider time,
    ILogger<PgJobQueue> logger) : IJobQueue
{
    /// <summary>How many 23505 conflicts on the concurrency index one claim tolerates before it gives up until the next round.</summary>
    private const int MaxKeyConflicts = 3;

    private readonly Lock _pausedGate = new();
    private IReadOnlySet<JobResourceClass>? _paused;
    private DateTimeOffset _pausedReadAt;

    private WorkerOptions Worker => workerOptions.Value;

    private JobsOptions Jobs => jobsOptions.Value;

    /// <inheritdoc />
    public Task<EnqueueResult> EnqueueAsync(JobRequest request, DbTransaction transaction, CancellationToken ct = default) =>
        EnqueueCoreAsync(request, RequireOpen(transaction), checkRunCanceled: false, ct);

    /// <summary>Enqueue in <paramref name="transaction"/>; with <paramref name="checkRunCanceled"/> nothing is created for a canceled run.</summary>
    internal async Task<EnqueueResult> EnqueueCoreAsync(JobRequest request, NpgsqlTransaction transaction, bool checkRunCanceled, CancellationToken ct)
    {
        var payload = Validate(request);
        var connection = transaction.Connection!;
        if (checkRunCanceled && request.RunId is { } runId)
        {
            await using var check = Command(connection, transaction, JobQueueSql.RunCancelRequestedForShare, ("run", runId, NpgsqlDbType.Uuid));
            var canceled = await check.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (canceled is null or DBNull or true)
            {
                // A vanished run counts as canceled: nothing continues work nobody can see.
                return new EnqueueResult(0, false, JobState.Canceled);
            }
        }

        var resourceClass = request.ResourceClass.ToDb();
        await using (var insert = Command(connection, transaction, JobQueueSql.Enqueue,
            ("tenant", request.TenantId, NpgsqlDbType.Uuid),
            ("shop", request.ShopId, NpgsqlDbType.Uuid),
            ("run", request.RunId, NpgsqlDbType.Uuid),
            ("kind", request.Kind, NpgsqlDbType.Text),
            ("class", resourceClass, NpgsqlDbType.Text),
            ("priority", (short)request.Priority, NpgsqlDbType.Smallint),
            ("payload", payload, NpgsqlDbType.Jsonb),
            ("dedupe", request.DedupeKey, NpgsqlDbType.Text),
            ("concurrency", request.ConcurrencyKey, NpgsqlDbType.Text),
            ("max_attempts", request.MaxAttempts ?? Jobs.DefaultMaxAttempts, NpgsqlDbType.Integer),
            ("not_before", request.NotBefore?.ToUniversalTime(), NpgsqlDbType.TimestampTz)))
        {
            if (await insert.ExecuteScalarAsync(ct).ConfigureAwait(false) is long id)
            {
                await using var notify = Command(connection, transaction, JobQueueSql.Notify, ("class", resourceClass, NpgsqlDbType.Text));
                await notify.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                return new EnqueueResult(id, true, JobState.Queued);
            }
        }

        await using var existing = Command(connection, transaction, JobQueueSql.SelectByDedupeKey, ("dedupe", request.DedupeKey, NpgsqlDbType.Text));
        await using var reader = await existing.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            // The conflicting job was deleted between the two statements (cleanup); the caller may enqueue again.
            throw new JobQueueException(JobErrorCodes.InvalidRequest, "the job with this dedupe key disappeared during enqueue; retry");
        }

        return new EnqueueResult(reader.GetInt64(0), false, JobNames.ParseState(reader.GetString(1)));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClaimedJob>> ClaimAsync(JobResourceClass resourceClass, int max, string workerId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (max <= 0)
        {
            return [];
        }

        var claimed = new List<ClaimedJob>(max);
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);

        // Keyed jobs that no unkeyed job precedes, then unkeyed jobs in one statement, then the remaining keyed jobs:
        // the order (priority, id) holds across both kinds, and neither kind starves the other.
        await ClaimKeyedAsync(connection, JobQueueSql.ClaimKeyedAhead, resourceClass, max, workerId, claimed, ct).ConfigureAwait(false);
        if (claimed.Count < max)
        {
            claimed.AddRange(await RunClaimAsync(connection, JobQueueSql.ClaimUnkeyed, resourceClass, max - claimed.Count, workerId, ct).ConfigureAwait(false));
        }

        if (claimed.Count < max)
        {
            await ClaimKeyedAsync(connection, JobQueueSql.ClaimKeyed, resourceClass, max, workerId, claimed, ct).ConfigureAwait(false);
        }

        return claimed;
    }

    private async Task ClaimKeyedAsync(NpgsqlConnection connection, string sql, JobResourceClass resourceClass, int max, string workerId, List<ClaimedJob> claimed, CancellationToken ct)
    {
        var conflicts = 0;
        while (claimed.Count < max)
        {
            try
            {
                var jobs = await RunClaimAsync(connection, sql, resourceClass, 1, workerId, ct).ConfigureAwait(false);
                if (jobs.Count == 0)
                {
                    return;
                }

                claimed.AddRange(jobs);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation && ex.ConstraintName == JobQueueSql.ConcurrencyIndex)
            {
                // Another worker took a job with the same key at the same moment; the statement changed nothing.
                logger.LogDebug("job.claim_key_conflict {ResourceClass}", resourceClass.ToDb());
                if (++conflicts >= MaxKeyConflicts)
                {
                    return;
                }
            }
        }
    }

    private async Task<List<ClaimedJob>> RunClaimAsync(NpgsqlConnection connection, string sql, JobResourceClass resourceClass, int n, string workerId, CancellationToken ct)
    {
        await using var command = Command(connection, null, sql,
            ("class", resourceClass.ToDb(), NpgsqlDbType.Text),
            ("n", n, NpgsqlDbType.Integer),
            ("tenant_cap", Worker.TenantCaps[resourceClass], NpgsqlDbType.Integer),
            ("worker", workerId, NpgsqlDbType.Text),
            ("lease", Worker.Lease, NpgsqlDbType.Interval));
        var jobs = new List<ClaimedJob>(n);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            jobs.Add(new ClaimedJob(
                Id: reader.GetInt64(0),
                Kind: reader.GetString(1),
                ResourceClass: resourceClass,
                Priority: (JobPriority)reader.GetInt16(2),
                TenantId: reader.IsDBNull(3) ? null : reader.GetGuid(3),
                ShopId: reader.IsDBNull(4) ? null : reader.GetGuid(4),
                RunId: reader.IsDBNull(5) ? null : reader.GetGuid(5),
                Payload: JsonDocument.Parse(reader.GetString(6)),
                Attempt: reader.GetInt32(7),
                MaxAttempts: reader.GetInt32(8),
                ConcurrencyKey: reader.IsDBNull(9) ? null : reader.GetString(9),
                LeaseOwner: workerId));
        }

        return jobs;
    }

    /// <inheritdoc />
    public async Task<HeartbeatResult> HeartbeatAsync(ClaimedJob job, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        Guid? runId = null;
        Guid? tenantId = null;
        await using (var command = Fenced(connection, null, JobQueueSql.Heartbeat, job, ("lease", Worker.Lease, NpgsqlDbType.Interval)))
        await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return new HeartbeatResult(LeaseHeld: false, CancelRequested: false);
            }

            runId = reader.IsDBNull(0) ? null : reader.GetGuid(0);
            tenantId = reader.IsDBNull(1) ? null : reader.GetGuid(1);
        }

        var canceled = runId is { } run && tenantId is { } tenant
            && await IsRunCancelRequestedAsync(connection, tenant, run, ct).ConfigureAwait(false);
        return new HeartbeatResult(LeaseHeld: true, CancelRequested: canceled);
    }

    /// <inheritdoc />
    public async Task<bool> IsRunCancelRequestedAsync(Guid tenantId, Guid runId, CancellationToken ct = default)
    {
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        return await IsRunCancelRequestedAsync(connection, tenantId, runId, ct).ConfigureAwait(false);
    }

    private static async Task<bool> IsRunCancelRequestedAsync(NpgsqlConnection connection, Guid tenantId, Guid runId, CancellationToken ct)
    {
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, ct: ct).ConfigureAwait(false);
        await using var command = Command(connection, transaction, JobQueueSql.RunCancelRequested, ("run", runId, NpgsqlDbType.Uuid));
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return value is not false;
    }

    /// <inheritdoc />
    public async Task CompleteAsync(ClaimedJob job, Func<JobTransaction, Task>? writeResults, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        await using var scope = scopes.CreateAsyncScope();
        if (job.TenantId is { } tenantId)
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
        }

        await CompleteAsync(job, scope.ServiceProvider.GetRequiredService<EshopGuardDb>(), writeResults, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CompleteAsync(ClaimedJob job, EshopGuardDb db, Func<JobTransaction, Task>? writeResults, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(db);
        if (db.TenantContext.TenantId != job.TenantId)
        {
            throw new InvalidOperationException($"The context's tenant is not the tenant of job {job.Id}.");
        }

        if (db.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException($"Job {job.Id} completes in its own transaction; the context already has one open.");
        }

        await using var efTransaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var transaction = (NpgsqlTransaction)efTransaction.GetDbTransaction();
        var connection = transaction.Connection!;

        // Lock the job row first: until COMMIT neither the reaper nor another worker can take it, and a worker that lost the
        // lease finds nothing here and writes none of its results.
        await using (var lockRow = Fenced(connection, transaction, JobQueueSql.LockForCompletion, job))
        {
            if (await lockRow.ExecuteScalarAsync(ct).ConfigureAwait(false) is null)
            {
                await efTransaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                throw new LeaseLostException(job.Id, job.Attempt);
            }
        }

        if (writeResults is not null)
        {
            await writeResults(new JobTransaction(db, transaction, this, job)).ConfigureAwait(false);
        }

        await using (var done = Command(connection, transaction, JobQueueSql.MarkSucceeded, ("id", job.Id, NpgsqlDbType.Bigint)))
        {
            await done.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        if (job.ConcurrencyKey is not null)
        {
            // The key is free again: wake the workers waiting for the next job with it.
            await NotifyAsync(connection, transaction, job.ResourceClass, ct).ConfigureAwait(false);
        }

        await efTransaction.CommitAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<JobState> FailAsync(ClaimedJob job, JobError error, bool permanent, TimeSpan? retryAfter = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(error);
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var command = Fenced(connection, null, JobQueueSql.Fail, job,
            ("permanent", permanent, NpgsqlDbType.Boolean),
            ("after", retryAfter, NpgsqlDbType.Interval),
            ("base", Jobs.Retry.BaseSeconds, NpgsqlDbType.Double),
            ("max", Jobs.Retry.MaxSeconds, NpgsqlDbType.Double),
            ("error", error.Format(), NpgsqlDbType.Text));
        if (await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is not string state)
        {
            throw new LeaseLostException(job.Id, job.Attempt);
        }

        if (job.ConcurrencyKey is not null)
        {
            await NotifyAsync(connection, null, job.ResourceClass, ct).ConfigureAwait(false);
        }

        return JobNames.ParseState(state);
    }

    /// <inheritdoc />
    public async Task DeferAsync(ClaimedJob job, TimeSpan delay, string reasonCode, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var command = Fenced(connection, null, JobQueueSql.Defer, job,
            ("delay", delay < TimeSpan.Zero ? TimeSpan.Zero : delay, NpgsqlDbType.Interval),
            ("reason", new JobError(reasonCode).Format(), NpgsqlDbType.Text));
        if (await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 0)
        {
            throw new LeaseLostException(job.Id, job.Attempt);
        }

        await NotifyAsync(connection, null, job.ResourceClass, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task MarkCanceledAsync(ClaimedJob job, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var command = Fenced(connection, null, JobQueueSql.MarkCanceled, job);
        if (await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 0)
        {
            throw new LeaseLostException(job.Id, job.Attempt);
        }

        if (job.ConcurrencyKey is not null)
        {
            await NotifyAsync(connection, null, job.ResourceClass, ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<bool> CancelAsync(long jobId, CancellationToken ct = default)
    {
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var command = Command(connection, null, JobQueueSql.CancelQueued, ("id", jobId, NpgsqlDbType.Bigint));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 1;
    }

    /// <inheritdoc />
    public async Task<int> CancelRunJobsAsync(Guid runId, DbTransaction transaction, CancellationToken ct = default)
    {
        var tx = RequireOpen(transaction);
        await using var command = Command(tx.Connection!, tx, JobQueueSql.CancelRunQueued, ("run", runId, NpgsqlDbType.Uuid));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> RequeueExpiredLeasesAsync(int max, CancellationToken ct = default)
    {
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var command = Command(connection, null, JobQueueSql.RequeueExpired,
            ("max_jobs", max, NpgsqlDbType.Integer),
            ("base", Jobs.Retry.BaseSeconds, NpgsqlDbType.Double),
            ("max", Jobs.Retry.MaxSeconds, NpgsqlDbType.Double));
        var count = 0;
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            count++;
            logger.LogWarning("{Code} {JobId} {State}", JobErrorCodes.LeaseExpired, reader.GetInt64(0), reader.GetString(1));
        }

        return count;
    }

    /// <inheritdoc />
    public async Task PauseResourceClassAsync(JobResourceClass resourceClass, string reasonCode, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        await UpdatePausedAsync(JobQueueSql.Pause, ct, ("class", resourceClass.ToDb(), NpgsqlDbType.Text), ("reason", reasonCode, NpgsqlDbType.Text)).ConfigureAwait(false);
        logger.LogError("jobs.class_paused {ResourceClass} {Reason}", resourceClass.ToDb(), reasonCode);
    }

    /// <inheritdoc />
    public async Task ResumeResourceClassAsync(JobResourceClass resourceClass, CancellationToken ct = default)
    {
        await UpdatePausedAsync(JobQueueSql.Resume, ct, ("class", resourceClass.ToDb(), NpgsqlDbType.Text)).ConfigureAwait(false);
        logger.LogWarning("jobs.class_resumed {ResourceClass}", resourceClass.ToDb());
    }

    private async Task UpdatePausedAsync(string sql, CancellationToken ct, params (string Name, object? Value, NpgsqlDbType Type)[] parameters)
    {
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var command = Command(connection, null, sql, parameters);
        if (await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 0)
        {
            throw new JobQueueException(JobErrorCodes.PausedClassesMissing, "ops.system_settings has no row jobs.paused_classes");
        }

        lock (_pausedGate)
        {
            _paused = null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<JobResourceClass>> GetPausedClassesAsync(CancellationToken ct = default)
    {
        var now = time.GetUtcNow();
        lock (_pausedGate)
        {
            if (_paused is { } cached && now - _pausedReadAt < TimeSpan.FromSeconds(Jobs.PausedClassesCacheSeconds))
            {
                return cached;
            }
        }

        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var command = Command(connection, null, JobQueueSql.PausedClasses);
        if (await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is not string json)
        {
            throw new JobQueueException(JobErrorCodes.PausedClassesMissing, "ops.system_settings has no row jobs.paused_classes");
        }

        using var document = JsonDocument.Parse(json);
        var paused = new HashSet<JobResourceClass>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (JobNames.ParseResourceClass(property.Name) is { } resourceClass)
            {
                paused.Add(resourceClass);
            }
        }

        lock (_pausedGate)
        {
            _paused = paused;
            _pausedReadAt = now;
        }

        return paused;
    }

    /// <inheritdoc />
    public async Task<int> DeleteFinishedAsync(TimeSpan succeededOlderThan, TimeSpan failedOlderThan, int batchSize, CancellationToken ct = default)
    {
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var command = Command(connection, null, JobQueueSql.DeleteFinished,
            ("succeeded", succeededOlderThan, NpgsqlDbType.Interval),
            ("failed", failedOlderThan, NpgsqlDbType.Interval),
            ("n", batchSize, NpgsqlDbType.Integer));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task NotifyAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, JobResourceClass resourceClass, CancellationToken ct)
    {
        await using var notify = Command(connection, transaction, JobQueueSql.Notify, ("class", resourceClass.ToDb(), NpgsqlDbType.Text));
        await notify.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static NpgsqlTransaction RequireOpen(DbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (transaction is not NpgsqlTransaction npgsql)
        {
            throw new ArgumentException("The transaction must be a PostgreSQL transaction of the EshopGuard database.", nameof(transaction));
        }

        return npgsql.Connection is null
            ? throw new JobQueueException(JobErrorCodes.EnqueueRequiresTransaction, "the transaction is already committed or rolled back")
            : npgsql;
    }

    /// <summary>Checks the request; returns the payload as JSON text.</summary>
    private static string Validate(JobRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Kind))
        {
            throw new JobQueueException(JobErrorCodes.InvalidRequest, "Kind is empty");
        }

        if (!Enum.IsDefined(request.Priority))
        {
            throw new JobQueueException(JobErrorCodes.InvalidRequest, "Priority must be 0–4");
        }

        if (!Enum.IsDefined(request.ResourceClass))
        {
            throw new JobQueueException(JobErrorCodes.InvalidRequest, "unknown ResourceClass");
        }

        if (request.MaxAttempts is < 1)
        {
            throw new JobQueueException(JobErrorCodes.InvalidRequest, "MaxAttempts must be at least 1");
        }

        if (request.TenantId is null && (request.ShopId is not null || request.RunId is not null))
        {
            throw new JobQueueException(JobErrorCodes.InvalidRequest, "a job of a shop or run needs its TenantId");
        }

        ArgumentNullException.ThrowIfNull(request.Payload);
        var payload = request.Payload.RootElement.GetRawText();
        if (Encoding.UTF8.GetByteCount(payload) > JobRequest.MaxPayloadBytes)
        {
            throw new JobQueueException(JobErrorCodes.PayloadTooLarge, $"payload over {JobRequest.MaxPayloadBytes} bytes; store large data and pass its key");
        }

        return payload;
    }

    private static NpgsqlCommand Fenced(NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql, ClaimedJob job, params (string Name, object? Value, NpgsqlDbType Type)[] parameters) =>
        Command(connection, transaction, sql,
        [
            ("id", job.Id, NpgsqlDbType.Bigint),
            ("worker", job.LeaseOwner, NpgsqlDbType.Text),
            ("attempt", job.Attempt, NpgsqlDbType.Integer),
            .. parameters,
        ]);

    internal static NpgsqlCommand Command(NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql, params (string Name, object? Value, NpgsqlDbType Type)[] parameters)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value, type) in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter(name, type) { Value = value ?? DBNull.Value });
        }

        return command;
    }
}
