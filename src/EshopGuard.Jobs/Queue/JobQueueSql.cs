namespace EshopGuard.Jobs.Queue;

/// <summary>
/// SQL of the queue. Times come from <c>clock_timestamp()</c>, not <c>now()</c> (the start of the transaction would extend a
/// lease too little in a longer transaction). Every change of a running job checks the fencing token
/// (<c>lease_owner</c>, <c>attempts</c>, <c>state = 'running'</c>).
/// </summary>
internal static class JobQueueSql
{
    public const string Enqueue = """
        INSERT INTO ops.jobs (tenant_id, shop_id, run_id, kind, resource_class, priority, payload, state,
                              dedupe_key, concurrency_key, attempts, max_attempts, not_before)
        VALUES (@tenant, @shop, @run, @kind, @class, @priority, @payload, 'queued',
                @dedupe, @concurrency, 0, @max_attempts, coalesce(@not_before, clock_timestamp()))
        ON CONFLICT (dedupe_key) DO NOTHING
        RETURNING id
        """;

    public const string SelectByDedupeKey = "SELECT id, state FROM ops.jobs WHERE dedupe_key = @dedupe";

    /// <summary>Delivered after COMMIT only, not at all after ROLLBACK.</summary>
    public const string Notify = "SELECT pg_notify('" + JobNames.NotificationChannel + "', @class)";

    /// <summary>Locks the run so that a concurrent cancellation waits for this transaction (and then cancels what it created).</summary>
    public const string RunCancelRequestedForShare = "SELECT cancel_requested FROM checks.runs WHERE id = @run FOR SHARE";

    public const string RunCancelRequested = "SELECT cancel_requested FROM checks.runs WHERE id = @run";

    private const string Ready = "j.state = 'queued' AND j.resource_class = @class AND j.not_before <= clock_timestamp()";

    /// <summary>Soft cap of running jobs per tenant and class (0 = none); two workers at once may exceed it by what they take.</summary>
    private const string UnderTenantCap = """
        (j.tenant_id IS NULL OR @tenant_cap = 0 OR
         (SELECT count(*) FROM ops.jobs r
           WHERE r.state = 'running' AND r.tenant_id = j.tenant_id AND r.resource_class = @class) < @tenant_cap)
        """;

    private const string KeyFree = """
        j.concurrency_key IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM ops.jobs r WHERE r.state = 'running' AND r.concurrency_key = j.concurrency_key)
        """;

    /// <summary>
    /// The job is the first ready job of its key in the order (priority, id). Without this, two workers could take two jobs of
    /// one key at once and the one that reaches the unique index first would win, even if it is the later job.
    /// </summary>
    private const string KeyHead = """
        NOT EXISTS (SELECT 1 FROM ops.jobs e
                    WHERE e.state = 'queued' AND e.concurrency_key = j.concurrency_key
                      AND e.not_before <= clock_timestamp() AND (e.priority, e.id) < (j.priority, j.id))
        """;

    /// <summary>
    /// The first ready job without a key, found once per statement through <c>ix_jobs_queued_unkeyed</c> (a per-row check
    /// made the planner scan the table for every candidate).
    /// </summary>
    private const string UnkeyedHead = """
        unkeyed_head AS (
          SELECT u.priority, u.id FROM ops.jobs u
          WHERE u.state = 'queued' AND u.resource_class = @class AND u.concurrency_key IS NULL AND u.not_before <= clock_timestamp()
          ORDER BY u.priority, u.id
          LIMIT 1)
        """;

    /// <summary>No ready job without a key comes before this one in the order (priority, id).</summary>
    private const string NoUnkeyedAhead = "NOT EXISTS (SELECT 1 FROM unkeyed_head h WHERE (h.priority, h.id) < (j.priority, j.id))";

    private const string TakeLease = """
        UPDATE ops.jobs j
        SET state = 'running', attempts = j.attempts + 1, lease_owner = @worker,
            lease_until = clock_timestamp() + @lease, heartbeat_at = clock_timestamp(),
            started_at = coalesce(j.started_at, clock_timestamp()), updated_at = clock_timestamp()
        FROM c WHERE j.id = c.id
        RETURNING j.id, j.kind, j.priority, j.tenant_id, j.shop_id, j.run_id, j.payload::text,
                  j.attempts, j.max_attempts, j.concurrency_key
        """;

    /// <summary>
    /// Jobs without a concurrency key, many in one statement. The per-row cap check sees only jobs that were running before
    /// the statement, so the locked candidates are ranked per tenant and only as many are taken as the cap still allows
    /// (the rest stays queued; the lock ends with the statement).
    /// </summary>
    public const string ClaimUnkeyed = $"""
        WITH locked AS (
          SELECT j.id, j.tenant_id, j.priority FROM ops.jobs j
          WHERE {Ready} AND j.concurrency_key IS NULL AND {UnderTenantCap}
          ORDER BY j.priority, j.id
          LIMIT @n
          FOR UPDATE SKIP LOCKED),
        running AS (
          SELECT r.tenant_id, count(*) AS n FROM ops.jobs r
          WHERE r.state = 'running' AND r.resource_class = @class AND r.tenant_id IN (SELECT tenant_id FROM locked)
          GROUP BY r.tenant_id),
        c AS (
          SELECT k.id
          FROM (SELECT l.id, l.tenant_id, row_number() OVER (PARTITION BY l.tenant_id ORDER BY l.priority, l.id) AS rn FROM locked l) k
          LEFT JOIN running x ON x.tenant_id = k.tenant_id
          WHERE k.tenant_id IS NULL OR @tenant_cap = 0 OR coalesce(x.n, 0) + k.rn <= @tenant_cap)
        {TakeLease}
        """;

    /// <summary>
    /// One job with a concurrency key: its key is not running and it is the key's first ready job. One at a time, because
    /// a statement cannot rank keys of the rows it locks; <c>ux_jobs_concurrency_running</c> (23505) stays the last safeguard.
    /// </summary>
    public const string ClaimKeyed = $"""
        WITH c AS (
          SELECT j.id FROM ops.jobs j
          WHERE {Ready} AND {KeyFree} AND {KeyHead} AND {UnderTenantCap}
          ORDER BY j.priority, j.id
          LIMIT 1
          FOR UPDATE SKIP LOCKED)
        {TakeLease}
        """;

    /// <summary><see cref="ClaimKeyed"/> limited to jobs that no ready job without a key precedes (keeps the order across both kinds).</summary>
    public const string ClaimKeyedAhead = $"""
        WITH {UnkeyedHead},
        c AS (
          SELECT j.id FROM ops.jobs j
          WHERE {Ready} AND {KeyFree} AND {KeyHead} AND {NoUnkeyedAhead} AND {UnderTenantCap}
          ORDER BY j.priority, j.id
          LIMIT 1
          FOR UPDATE SKIP LOCKED)
        {TakeLease}
        """;

    /// <summary>Name of the unique index that allows one running job per concurrency key.</summary>
    public const string ConcurrencyIndex = "ux_jobs_concurrency_running";

    private const string Fenced = "id = @id AND lease_owner = @worker AND attempts = @attempt AND state = 'running'";

    /// <summary>Extends the job lease and the domain lease the job holds (never shortens it).</summary>
    public const string Heartbeat = $"""
        WITH h AS (
          UPDATE ops.jobs SET lease_until = clock_timestamp() + @lease, heartbeat_at = clock_timestamp(), updated_at = clock_timestamp()
          WHERE {Fenced}
          RETURNING id, run_id, tenant_id),
        d AS (
          UPDATE ops.domains x SET lease_until = greatest(x.lease_until, clock_timestamp() + @lease), updated_at = clock_timestamp()
          FROM h WHERE x.lease_job_id = h.id AND x.lease_until IS NOT NULL)
        SELECT run_id, tenant_id FROM h
        """;

    public const string LockForCompletion = $"SELECT 1 FROM ops.jobs WHERE {Fenced} FOR UPDATE";

    public const string MarkSucceeded = """
        UPDATE ops.jobs SET state = 'succeeded', finished_at = clock_timestamp(), lease_owner = NULL, lease_until = NULL,
               last_error = NULL, updated_at = clock_timestamp()
        WHERE id = @id
        """;

    /// <summary>Growing backoff: base × 2^(attempts − 1), at most max, ±20 % jitter (<c>attempts</c> of the updated job).</summary>
    private const string Backoff =
        "make_interval(secs => least(@base * power(2::double precision, attempts - 1), @max) * (0.8 + random() * 0.4))";

    private const string FailFinal = "(@permanent OR attempts >= max_attempts)";

    public const string Fail = $"""
        UPDATE ops.jobs SET
          state = CASE WHEN {FailFinal} THEN 'failed' ELSE 'queued' END,
          not_before = CASE WHEN {FailFinal} THEN not_before
                            ELSE clock_timestamp() + coalesce(@after, {Backoff}) END,
          finished_at = CASE WHEN {FailFinal} THEN clock_timestamp() END,
          lease_owner = NULL, lease_until = NULL, last_error = @error, updated_at = clock_timestamp()
        WHERE {Fenced}
        RETURNING state
        """;

    /// <summary>Gives back only this claim's increment of <c>attempts</c>, so any later claim still gets a higher number.</summary>
    public const string Defer = $"""
        UPDATE ops.jobs SET state = 'queued', attempts = attempts - 1, not_before = clock_timestamp() + @delay,
               lease_owner = NULL, lease_until = NULL, last_error = @reason, updated_at = clock_timestamp()
        WHERE {Fenced}
        """;

    public const string MarkCanceled = $"""
        UPDATE ops.jobs SET state = 'canceled', finished_at = clock_timestamp(), lease_owner = NULL, lease_until = NULL,
               updated_at = clock_timestamp()
        WHERE {Fenced}
        """;

    public const string CancelQueued = """
        UPDATE ops.jobs SET state = 'canceled', finished_at = clock_timestamp(), updated_at = clock_timestamp()
        WHERE id = @id AND state = 'queued'
        """;

    public const string CancelRunQueued = """
        UPDATE ops.jobs SET state = 'canceled', finished_at = clock_timestamp(), updated_at = clock_timestamp()
        WHERE run_id = @run AND state = 'queued'
        """;

    public const string RequeueExpired = $"""
        WITH e AS (
          SELECT id FROM ops.jobs WHERE state = 'running' AND lease_until < clock_timestamp()
          ORDER BY lease_until
          LIMIT @max_jobs
          FOR UPDATE SKIP LOCKED)
        UPDATE ops.jobs j SET
          state = CASE WHEN j.attempts >= j.max_attempts THEN 'failed' ELSE 'queued' END,
          not_before = CASE WHEN j.attempts >= j.max_attempts THEN j.not_before
                            ELSE clock_timestamp() + {Backoff} END,
          finished_at = CASE WHEN j.attempts >= j.max_attempts THEN clock_timestamp() END,
          lease_owner = NULL, lease_until = NULL, last_error = '{JobErrorCodes.LeaseExpired}', updated_at = clock_timestamp()
        FROM e WHERE j.id = e.id
        RETURNING j.id, j.state
        """;

    public const string PausedClassesKey = "jobs.paused_classes";

    public const string Pause = $"""
        UPDATE ops.system_settings
        SET value = value || jsonb_build_object(@class::text, jsonb_build_object('reason', @reason::text, 'at', clock_timestamp())),
            updated_at = clock_timestamp()
        WHERE key = '{PausedClassesKey}'
        """;

    public const string Resume = $"""
        UPDATE ops.system_settings SET value = value - @class::text, updated_at = clock_timestamp()
        WHERE key = '{PausedClassesKey}'
        """;

    public const string PausedClasses = $"SELECT value::text FROM ops.system_settings WHERE key = '{PausedClassesKey}'";

    public const string DeleteFinished = """
        WITH d AS (
          SELECT id FROM ops.jobs
          WHERE (state IN ('succeeded', 'canceled') AND finished_at < clock_timestamp() - @succeeded)
             OR (state = 'failed' AND finished_at < clock_timestamp() - @failed)
          LIMIT @n
          FOR UPDATE SKIP LOCKED)
        DELETE FROM ops.jobs j USING d WHERE j.id = d.id
        """;
}
