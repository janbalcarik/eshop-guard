using System.Text.Json.Nodes;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Tenancy;
using Npgsql;

namespace EshopGuard.Jobs.Runs;

/// <summary>
/// Where a run stands in the queue: <see cref="Position"/> = runs of its kind ahead of it (0 once it runs, null when it does
/// not wait for the workers: waiting for payment or ended); <see cref="EstimatedFinishAt"/> = null without measured history.
/// </summary>
public sealed record RunQueueEstimate(int? Position, DateTimeOffset? EstimatedFinishAt);

/// <summary>Position of a run in the queue and an estimate of its end (the screen of the free sample, change 10).</summary>
public interface IRunQueueEstimator
{
    /// <summary>The estimate for a run of the tenant of the caller; both values null when the run does not exist.</summary>
    Task<RunQueueEstimate> EstimateAsync(Guid runId, CancellationToken ct = default);
}

/// <summary>
/// <para>
/// Position: the runs of the same kind (free sample, or full analysis) whose first job (<c>run.discover</c>) waits in the
/// queue and was added earlier, of all tenants (only their number is read, from <c>ops.jobs</c>), plus the other running runs
/// of the same tenant, which hold the caps of the tenant.
/// </para>
/// <para>
/// End: what is left of the run, step by step, at the average duration of the step (from the start of its first job to the
/// end of its last one) in the runs of the same kind that ended in the last hour, and for every run ahead the duration of a
/// whole run. A step in progress counts with the share of its batches (pages for the crawl) still to do. Without a run of the
/// same kind ended in the last hour the estimate is null: nothing is made up. Runs ahead are counted one after another,
/// although the workers take several at once, so the estimate is on the late side.
/// </para>
/// </summary>
public sealed class RunQueueEstimator(EshopGuardDataSource dataSource, ITenantContext tenant, TimeProvider time) : IRunQueueEstimator
{
    /// <summary>The steps in their order and the states of the run in which each is the current one.</summary>
    internal static readonly (string Kind, RunStatus[] Statuses)[] Steps =
    [
        (RunJobKinds.Discover, [RunStatus.Queued, RunStatus.Discovering]),
        (RunJobKinds.Markets, [RunStatus.Queued, RunStatus.Discovering]),
        (RunJobKinds.Fetch, [RunStatus.Crawling]),
        (RunJobKinds.Profile, [RunStatus.Profiling]),
        (RunJobKinds.Segment, [RunStatus.Segmenting]),
        (RunJobKinds.Sieve, [RunStatus.Evaluating]),
        (RunJobKinds.PlanEvaluate, [RunStatus.Evaluating]),
        (RunJobKinds.Evaluate, [RunStatus.Evaluating]),
        (RunJobKinds.Rules, [RunStatus.Ruling]),
        (RunJobKinds.Rewrite, [RunStatus.Rewriting]),
        (RunJobKinds.Finalize, [RunStatus.Rewriting]),
    ];

    private static readonly string[] Active = ["discovering", "crawling", "profiling", "segmenting", "evaluating", "ruling", "rewriting"];

    public async Task<RunQueueEstimate> EstimateAsync(Guid runId, CancellationToken ct = default)
    {
        var tenantId = tenant.RequireTenantId();
        var now = time.GetUtcNow();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, tenant.UserId, ct).ConfigureAwait(false);
        var run = await LoadAsync(connection, transaction, runId, ct).ConfigureAwait(false);
        if (run is null || run.Value.Status is RunStatus.AwaitingPayment or RunStatus.Finished or RunStatus.Partial or RunStatus.Failed or RunStatus.Canceled)
        {
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return new RunQueueEstimate(null, null);
        }

        var (kind, status, progress) = run.Value;
        var kindText = SnakeCaseEnumConverter<RunKind>.ToText(kind);
        var position = status == RunStatus.Queued ? await PositionAsync(connection, transaction, runId, kindText, ct).ConfigureAwait(false) : 0;
        var steps = await StepDurationsAsync(connection, transaction, kindText, now, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return new RunQueueEstimate(position, Estimate(status, progress, position, steps, now));
    }

    /// <summary>
    /// The end of a run from the average durations of the steps (seconds by kind of job, null without history): the rest of
    /// the current step by its progress, the steps after it, and a whole run for each run ahead.
    /// </summary>
    internal static DateTimeOffset? Estimate(RunStatus status, JsonObject progress, int position, IReadOnlyDictionary<string, double>? steps, DateTimeOffset now)
    {
        if (steps is null)
        {
            return null;
        }

        var current = Array.FindIndex(Steps, s => s.Statuses.Contains(status));
        if (current < 0)
        {
            return null;
        }

        double Of(string kind) => steps.GetValueOrDefault(kind);
        var whole = Steps.Sum(s => Of(s.Kind));
        var left = 0.0;
        for (var i = current; i < Steps.Length; i++)
        {
            var (kind, statuses) = Steps[i];
            left += statuses.Contains(status) ? Of(kind) * Remaining(kind, progress) : Of(kind);
        }

        return now + TimeSpan.FromSeconds(left + (position * whole));
    }

    /// <summary>The share of a step still to do by the progress of the run (1 when unknown).</summary>
    internal static double Remaining(string kind, JsonObject progress)
    {
        var (done, total) = kind switch
        {
            RunJobKinds.Fetch => (RunStore.Long(progress, "pages_fetched"), RunStore.Long(progress, "pages_planned")),
            RunJobKinds.Sieve or RunJobKinds.Evaluate or RunJobKinds.Rewrite when progress["steps"]?[kind[4..]] is JsonObject step =>
                (RunStore.Long(step, "done"), RunStore.Long(step, "total")),
            _ => (0L, 0L),
        };
        return total > 0 ? Math.Clamp(1 - ((double)done / total), 0, 1) : 1;
    }

    private static async Task<(RunKind Kind, RunStatus Status, JsonObject Progress)?> LoadAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT kind, status, coalesce(progress, '{}'::jsonb)::text FROM checks.runs WHERE id = $1", connection, transaction)
        {
            Parameters = { new NpgsqlParameter { Value = runId } },
        };
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }

        return (SnakeCaseEnumConverter<RunKind>.FromText(reader.GetString(0)), SnakeCaseEnumConverter<RunStatus>.FromText(reader.GetString(1)),
            JsonNode.Parse(reader.GetString(2))!.AsObject());
    }

    /// <summary>Runs of the kind waiting before this one (all tenants, a number only) and the other running runs of the tenant.</summary>
    private static async Task<int> PositionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId, string kind, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            WITH own AS (
                SELECT min(id) AS id FROM ops.jobs WHERE run_id = $1 AND kind = 'run.discover'
            )
            SELECT (SELECT count(*) FROM ops.jobs j, own
                    WHERE j.kind = 'run.discover' AND j.state = 'queued' AND j.id < own.id AND j.payload ->> 'run_kind' = $2)
                 + (SELECT count(*) FROM checks.runs r WHERE r.id <> $1 AND r.kind = $2 AND r.status = ANY($3))
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = runId },
                new NpgsqlParameter { Value = kind },
                new NpgsqlParameter { Value = Active },
            },
        };
        return (int)(long)(await command.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
    }

    /// <summary>
    /// Average seconds of each step in the runs of the kind that ended in the last hour (a step a run did not need counts as 0
    /// for it); null when no such run ended.
    /// </summary>
    private static async Task<Dictionary<string, double>?> StepDurationsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string kind, DateTimeOffset now, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            WITH recent AS (
                SELECT f.run_id FROM ops.jobs f
                JOIN ops.jobs d ON d.run_id = f.run_id AND d.kind = 'run.discover' AND d.payload ->> 'run_kind' = $1
                WHERE f.kind = 'run.finalize' AND f.state = 'succeeded' AND f.finished_at > $2
            ), steps AS (
                SELECT j.run_id, j.kind, extract(epoch FROM max(j.finished_at) - min(j.started_at))::float8 AS seconds
                FROM ops.jobs j JOIN recent r ON r.run_id = j.run_id
                WHERE j.state = 'succeeded' AND j.started_at IS NOT NULL AND j.finished_at IS NOT NULL
                GROUP BY j.run_id, j.kind
            )
            SELECT s.kind, sum(s.seconds) / (SELECT count(*) FROM recent), (SELECT count(*) FROM recent)
            FROM steps s GROUP BY s.kind
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = kind },
                new NpgsqlParameter { Value = now - TimeSpan.FromHours(1) },
            },
        };
        var steps = new Dictionary<string, double>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            steps[reader.GetString(0)] = reader.GetDouble(1);
        }

        return steps.Count == 0 ? null : steps;
    }
}
