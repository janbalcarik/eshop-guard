using System.Globalization;
using System.Text.Json;
using EshopGuard.Data.Connections;
using EshopGuard.Jobs.Queue;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Jobs.Workers;

/// <summary><see cref="IWorkerStore"/> over PostgreSQL as <c>eshopguard_worker</c>.</summary>
public sealed class PgWorkerStore(EshopGuardDataSource dataSource) : IWorkerStore
{
    private const string Register = """
        INSERT INTO ops.workers (id, version, slots, started_at, heartbeat_at, draining)
        VALUES (@id, @version, @slots, clock_timestamp(), clock_timestamp(), false)
        ON CONFLICT (id) DO UPDATE SET version = excluded.version, slots = excluded.slots, started_at = excluded.started_at,
            heartbeat_at = excluded.heartbeat_at, draining = false, updated_at = clock_timestamp()
        """;

    private const string Heartbeat = """
        UPDATE ops.workers SET heartbeat_at = clock_timestamp(), draining = @draining, updated_at = clock_timestamp()
        WHERE id = @id
        """;

    private const string Unregister = "DELETE FROM ops.workers WHERE id = @id";

    private const string DeleteStale = "DELETE FROM ops.workers WHERE heartbeat_at < clock_timestamp() - @older";

    private const string EnsureDomain = """
        INSERT INTO ops.domains (domain, consecutive_errors) VALUES (@domain, 0) ON CONFLICT (domain) DO NOTHING
        """;

    private const string AcquireDomain = """
        WITH d AS (
          SELECT domain FROM ops.domains
          WHERE domain = @domain
            AND (lease_until IS NULL OR lease_until < clock_timestamp() OR lease_job_id = @job)
            AND (blocked_until IS NULL OR blocked_until <= clock_timestamp())
          FOR UPDATE SKIP LOCKED)
        UPDATE ops.domains x SET lease_job_id = @job, lease_until = clock_timestamp() + @lease, updated_at = clock_timestamp()
        FROM d WHERE x.domain = d.domain
        RETURNING x.robots_txt, x.robots_fetched_at, x.crawl_delay_ms, x.sitemaps, x.last_request_at, x.rate,
                  x.consecutive_errors, x.blocked_until
        """;

    private const string RenewDomain = """
        UPDATE ops.domains SET lease_until = clock_timestamp() + @lease, updated_at = clock_timestamp()
        WHERE domain = @domain AND lease_job_id = @job
        """;

    private const string ReleaseDomain = """
        UPDATE ops.domains SET lease_job_id = NULL, lease_until = NULL, robots_txt = @robots, robots_fetched_at = @robots_at,
               crawl_delay_ms = @delay, sitemaps = @sitemaps, last_request_at = @last, rate = @rate,
               consecutive_errors = @errors, blocked_until = @blocked, updated_at = clock_timestamp()
        WHERE domain = @domain AND lease_job_id = @job
        """;

    /// <summary>Tokens available now: stored tokens plus the refill since the last change, at most the capacity.</summary>
    private const string Available =
        "least(b.capacity, b.tokens + extract(epoch FROM clock_timestamp() - b.updated_at) * b.refill_per_sec)";

    /// <summary>Share of the capacity P2–P4 must leave for P0–P1.</summary>
    private const string Reserve =
        "CASE WHEN @priority <= 1 THEN 0 ELSE b.capacity * coalesce((b.reserved ->> 'p0_p1_share')::double precision, 0) END";

    private const string Reserve1 = $"""
        UPDATE ops.rate_limit_buckets b SET tokens = {Available} - @n, updated_at = clock_timestamp()
        WHERE b.key = @key AND {Available} - @n >= {Reserve}
        RETURNING b.tokens
        """;

    private const string BucketState = $"""
        SELECT b.capacity, {Available}, b.refill_per_sec, {Reserve}
        FROM ops.rate_limit_buckets b WHERE b.key = @key
        """;

    private const string ReturnTokens = """
        UPDATE ops.rate_limit_buckets SET tokens = least(capacity, tokens + @n)
        WHERE key = @key
        """;

    /// <inheritdoc />
    public async Task RegisterAsync(WorkerRegistration worker, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(worker);
        var slots = JsonSerializer.Serialize(worker.Slots.ToDictionary(s => s.Key.ToDb(), s => s.Value));
        await ExecuteAsync(Register, ct,
            ("id", worker.Id, NpgsqlDbType.Text),
            ("version", worker.Version, NpgsqlDbType.Text),
            ("slots", slots, NpgsqlDbType.Jsonb)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> HeartbeatAsync(string workerId, bool draining, CancellationToken ct = default) =>
        await ExecuteAsync(Heartbeat, ct, ("id", workerId, NpgsqlDbType.Text), ("draining", draining, NpgsqlDbType.Boolean)).ConfigureAwait(false) == 1;

    /// <inheritdoc />
    public Task UnregisterAsync(string workerId, CancellationToken ct = default) =>
        ExecuteAsync(Unregister, ct, ("id", workerId, NpgsqlDbType.Text));

    /// <inheritdoc />
    public Task<int> DeleteStaleWorkersAsync(TimeSpan olderThan, CancellationToken ct = default) =>
        ExecuteAsync(DeleteStale, ct, ("older", olderThan, NpgsqlDbType.Interval));

    /// <inheritdoc />
    public async Task<DomainLease?> TryAcquireDomainAsync(string domain, long jobId, TimeSpan lease, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (var ensure = PgJobQueue.Command(connection, null, EnsureDomain, ("domain", domain, NpgsqlDbType.Text)))
        {
            await ensure.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await using var acquire = PgJobQueue.Command(connection, null, AcquireDomain,
            ("domain", domain, NpgsqlDbType.Text),
            ("job", jobId, NpgsqlDbType.Bigint),
            ("lease", lease, NpgsqlDbType.Interval));
        await using var reader = await acquire.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }

        var state = new DomainPolitenessState(
            RobotsTxt: reader.IsDBNull(0) ? null : reader.GetString(0),
            RobotsFetchedAt: reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1),
            CrawlDelayMs: reader.IsDBNull(2) ? null : reader.GetInt32(2),
            Sitemaps: reader.GetFieldValue<string[]>(3),
            LastRequestAt: reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
            Rate: reader.IsDBNull(5) ? null : reader.GetDouble(5),
            ConsecutiveErrors: reader.GetInt32(6),
            BlockedUntil: reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7));
        return new DomainLease(domain, jobId, state);
    }

    /// <inheritdoc />
    public async Task<bool> RenewDomainAsync(string domain, long jobId, TimeSpan lease, CancellationToken ct = default) =>
        await ExecuteAsync(RenewDomain, ct,
            ("domain", domain, NpgsqlDbType.Text),
            ("job", jobId, NpgsqlDbType.Bigint),
            ("lease", lease, NpgsqlDbType.Interval)).ConfigureAwait(false) == 1;

    /// <inheritdoc />
    public Task ReleaseDomainAsync(string domain, long jobId, DomainPolitenessState state, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        return ExecuteAsync(ReleaseDomain, ct,
            ("domain", domain, NpgsqlDbType.Text),
            ("job", jobId, NpgsqlDbType.Bigint),
            ("robots", state.RobotsTxt, NpgsqlDbType.Text),
            ("robots_at", state.RobotsFetchedAt?.ToUniversalTime(), NpgsqlDbType.TimestampTz),
            ("delay", state.CrawlDelayMs, NpgsqlDbType.Integer),
            ("sitemaps", (state.Sitemaps ?? []).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text),
            ("last", state.LastRequestAt?.ToUniversalTime(), NpgsqlDbType.TimestampTz),
            ("rate", state.Rate, NpgsqlDbType.Double),
            ("errors", state.ConsecutiveErrors, NpgsqlDbType.Integer),
            ("blocked", state.BlockedUntil?.ToUniversalTime(), NpgsqlDbType.TimestampTz));
    }

    /// <inheritdoc />
    public async Task<RateLimitReservation> TryReserveAsync(string bucketKey, int tokens, JobPriority priority, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tokens);
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        (string, object?, NpgsqlDbType)[] parameters =
        [
            ("key", bucketKey, NpgsqlDbType.Text),
            ("n", (double)tokens, NpgsqlDbType.Double),
            ("priority", (int)priority, NpgsqlDbType.Integer),
        ];
        await using (var take = PgJobQueue.Command(connection, null, Reserve1, parameters))
        {
            if (await take.ExecuteScalarAsync(ct).ConfigureAwait(false) is double remaining)
            {
                return new RateLimitReservation.Granted(remaining);
            }
        }

        await using var read = PgJobQueue.Command(connection, null, BucketState, parameters);
        await using var reader = await read.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            throw new RateLimitBucketMissingException(bucketKey);
        }

        var capacity = reader.GetDouble(0);
        var available = reader.GetDouble(1);
        var refillPerSecond = reader.GetDouble(2);
        var reserve = reader.GetDouble(3);
        if (tokens > capacity - reserve)
        {
            throw new ArgumentOutOfRangeException(nameof(tokens), tokens, string.Create(CultureInfo.InvariantCulture,
                $"{JobErrorCodes.RateLimitBatchTooLarge}: bucket {bucketKey} lets priority {priority} draw at most {capacity - reserve:0.##} tokens"));
        }

        var missing = tokens + reserve - available;
        var retryAfter = refillPerSecond <= 0 ? TimeSpan.MaxValue : TimeSpan.FromSeconds(Math.Max(missing, 0) / refillPerSecond);
        return new RateLimitReservation.Denied(retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.FromMilliseconds(1));
    }

    /// <inheritdoc />
    public async Task ReturnTokensAsync(string bucketKey, int tokens, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tokens);
        if (await ExecuteAsync(ReturnTokens, ct, ("key", bucketKey, NpgsqlDbType.Text), ("n", (double)tokens, NpgsqlDbType.Double)).ConfigureAwait(false) == 0)
        {
            throw new RateLimitBucketMissingException(bucketKey);
        }
    }

    private async Task<int> ExecuteAsync(string sql, CancellationToken ct, params (string Name, object? Value, NpgsqlDbType Type)[] parameters)
    {
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var command = PgJobQueue.Command(connection, null, sql, parameters);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
