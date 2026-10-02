using System.Text.Json.Nodes;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Tenancy;
using Npgsql;

namespace EshopGuard.Jobs.Runs;

/// <summary>Progress of a run as the user may see it: pages and batches of the steps.</summary>
public sealed record RunProgressView(long? PagesPlanned, long PagesFetched, long PagesProcessed, IReadOnlyDictionary<string, RunStepView> Steps);

/// <summary>Batches of one step done out of planned.</summary>
public sealed record RunStepView(long Done, long Total);

/// <summary>
/// A run for the API (change 10): state, times, progress, what was not checked, the summary of a free sample and its scope
/// basis. Taken over field by field from <c>checks.runs</c>, so the internal estimate (<c>estimate.internal</c>), amounts in
/// USD, counts of paid calls and names of services never get here (design of change 8, "Odhad interních nákladů").
/// </summary>
public sealed record RunView(
    Guid Id,
    Guid ShopId,
    RunKind Kind,
    RunStatus Status,
    string? Error,
    bool CancelRequested,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    RunProgressView Progress,
    int? PagesChecked,
    int? Findings,
    JsonObject? FindingsBySeverity,
    JsonObject? Unchecked,
    JsonObject? Sample,
    JsonObject? ScopeBasis);

/// <summary>Reads runs of the tenant of the caller (<see cref="ITenantContext"/>) for the API.</summary>
public interface IRunReadModel
{
    /// <summary>The run, or null when it does not exist for the tenant.</summary>
    Task<RunView?> GetAsync(Guid runId, CancellationToken ct = default);

    /// <summary>The newest run of the kind for the e-shop (the free sample of the onboarding), or null.</summary>
    Task<RunView?> GetLatestAsync(Guid shopId, RunKind kind, CancellationToken ct = default);
}

/// <inheritdoc cref="IRunReadModel"/>
public sealed class RunReadModel(EshopGuardDataSource dataSource, ITenantContext tenant) : IRunReadModel
{
    /// <summary>Steps whose batches the progress counts.</summary>
    private static readonly string[] StepNames = ["sieve", "evaluate", "rewrite"];

    private const string Select = """
        SELECT id, shop_id, kind, status, error, cancel_requested, created_at, started_at, finished_at,
               coalesce(progress, '{}'::jsonb)::text, coalesce(stats, '{}'::jsonb)::text, coalesce(estimate -> 'basis', 'null'::jsonb)::text
        FROM checks.runs
        """;

    public Task<RunView?> GetAsync(Guid runId, CancellationToken ct = default) =>
        ReadAsync(Select + " WHERE id = $1", [runId], ct);

    public Task<RunView?> GetLatestAsync(Guid shopId, RunKind kind, CancellationToken ct = default) =>
        ReadAsync(Select + " WHERE shop_id = $1 AND kind = $2 ORDER BY created_at DESC, id DESC LIMIT 1", [shopId, SnakeCaseEnumConverter<RunKind>.ToText(kind)], ct);

    /// <summary>The view of one row: only the fields the user may see.</summary>
    public static RunView ToView(
        Guid id, Guid shopId, RunKind kind, RunStatus status, string? error, bool cancelRequested, DateTimeOffset createdAt,
        DateTimeOffset? startedAt, DateTimeOffset? finishedAt, JsonObject progress, JsonObject stats, JsonObject? basis)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(stats);
        var steps = new Dictionary<string, RunStepView>(StringComparer.Ordinal);
        foreach (var name in StepNames)
        {
            if (progress["steps"]?[name] is JsonObject step)
            {
                steps[name] = new RunStepView(RunStore.Long(step, "done"), RunStore.Long(step, "total"));
            }
        }

        var view = new RunProgressView(
            progress["pages_planned"] is null ? null : RunStore.Long(progress, "pages_planned"),
            RunStore.Long(progress, "pages_fetched"),
            RunStore.Long(progress, "pages_processed"),
            steps);
        return new RunView(
            id, shopId, kind, status, error, cancelRequested, createdAt, startedAt, finishedAt, view,
            stats["pages_checked"] is null ? null : (int)RunStore.Long(stats, "pages_checked"),
            stats["findings"] is null ? null : (int)RunStore.Long(stats, "findings"),
            Copy(stats["findings_by_severity"]),
            Copy(stats["unchecked"]),
            Copy(stats["sample"]),
            Copy(basis));
    }

    private async Task<RunView?> ReadAsync(string sql, object[] parameters, CancellationToken ct)
    {
        var tenantId = tenant.RequireTenantId();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, tenant.UserId, ct).ConfigureAwait(false);
        RunView? view = null;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            foreach (var parameter in parameters)
            {
                command.Parameters.Add(new NpgsqlParameter { Value = parameter });
            }

            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                view = ToView(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    SnakeCaseEnumConverter<RunKind>.FromText(reader.GetString(2)),
                    SnakeCaseEnumConverter<RunStatus>.FromText(reader.GetString(3)),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetBoolean(5),
                    reader.GetFieldValue<DateTimeOffset>(6),
                    reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                    reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
                    JsonNode.Parse(reader.GetString(9))!.AsObject(),
                    JsonNode.Parse(reader.GetString(10))!.AsObject(),
                    JsonNode.Parse(reader.GetString(11)) as JsonObject);
            }
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return view;
    }

    private static JsonObject? Copy(JsonNode? node) => node is JsonObject value ? value.DeepClone().AsObject() : null;
}
