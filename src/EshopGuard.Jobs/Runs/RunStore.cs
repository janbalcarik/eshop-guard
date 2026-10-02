using System.Text.Json.Nodes;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Jobs.Runs;

/// <summary>A run as its handlers see it (<c>checks.runs</c> with its e-shop).</summary>
public sealed record RunRow(
    Guid Id,
    Guid TenantId,
    Guid ShopId,
    RunKind Kind,
    RunStatus Status,
    IReadOnlyList<string> Jurisdictions,
    IReadOnlyList<string> Modules,
    bool CancelRequested,
    Guid? OrderId,
    JsonObject Progress,
    JsonObject Estimate,
    JsonObject Stats,
    string ShopDomain,
    string ShopBaseUrl,
    string ShopHomeCountry)
{
    public bool IsFinal => Status is RunStatus.Finished or RunStatus.Partial or RunStatus.Failed or RunStatus.Canceled;

    public bool IsSample => Kind == RunKind.FreeSample;
}

/// <summary>Plain SQL over <c>checks.runs</c> in a transaction of the run's tenant.</summary>
public static class RunStore
{
    /// <summary>The run with its e-shop; <paramref name="forUpdate"/> locks the row until the end of the transaction (barrier of the steps).</summary>
    public static async Task<RunRow?> LoadAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId, bool forUpdate, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            $"""
            SELECT r.id, r.tenant_id, r.shop_id, r.kind, r.status, r.jurisdictions, r.modules, r.cancel_requested, r.order_id,
                   coalesce(r.progress, '{"{}"}'::jsonb)::text, coalesce(r.estimate, '{"{}"}'::jsonb)::text, coalesce(r.stats, '{"{}"}'::jsonb)::text,
                   s.domain, s.base_url, s.home_country
            FROM checks.runs r JOIN shop.shops s ON s.id = r.shop_id AND s.tenant_id = r.tenant_id
            WHERE r.id = $1
            {(forUpdate ? "FOR UPDATE OF r" : "")}
            """, connection, transaction)
        {
            Parameters = { new NpgsqlParameter { Value = runId } },
        };
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }

        return new RunRow(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            SnakeCaseEnumConverter<RunKind>.FromText(reader.GetString(3)),
            SnakeCaseEnumConverter<RunStatus>.FromText(reader.GetString(4)),
            reader.GetFieldValue<string[]>(5),
            reader.GetFieldValue<string[]>(6),
            reader.GetBoolean(7),
            reader.IsDBNull(8) ? null : reader.GetGuid(8),
            JsonNode.Parse(reader.GetString(9))!.AsObject(),
            JsonNode.Parse(reader.GetString(10))!.AsObject(),
            JsonNode.Parse(reader.GetString(11))!.AsObject(),
            reader.GetString(12),
            reader.GetString(13),
            reader.GetString(14));
    }

    /// <summary>Replaces one of the JSON columns <c>progress</c>, <c>estimate</c> or <c>stats</c>.</summary>
    public static async Task SetJsonAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId, string column, JsonObject value, CancellationToken ct)
    {
        if (column is not ("progress" or "estimate" or "stats"))
        {
            throw new ArgumentOutOfRangeException(nameof(column), column, "Only progress, estimate and stats.");
        }

        await using var command = new NpgsqlCommand($"UPDATE checks.runs SET {column} = $2, updated_at = now() WHERE id = $1", connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = runId },
                new NpgsqlParameter { Value = value.ToJsonString(), NpgsqlDbType = NpgsqlDbType.Jsonb },
            },
        };
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Jurisdictions and modules of the run (the free sample learns them from the analysis of the places of sale).</summary>
    public static async Task SetScopeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId, IReadOnlyList<string> jurisdictions, IReadOnlyList<string> modules, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("UPDATE checks.runs SET jurisdictions = $2, modules = $3, updated_at = now() WHERE id = $1", connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = runId },
                new NpgsqlParameter { Value = jurisdictions.ToArray() },
                new NpgsqlParameter { Value = modules.ToArray() },
            },
        };
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>A nested object of a JSON column, created when missing.</summary>
    public static JsonObject Section(JsonObject root, params string[] path)
    {
        var current = root;
        foreach (var name in path)
        {
            if (current[name] is not JsonObject next)
            {
                next = new JsonObject();
                current[name] = next;
            }

            current = next;
        }

        return current;
    }

    /// <summary>Integer value of a JSON property, 0 when missing (read from the database or set in memory as int or long).</summary>
    public static long Long(JsonObject node, string name) => node[name] is not JsonValue value ? 0
        : value.TryGetValue<long>(out var number) ? number
        : value.TryGetValue<int>(out var small) ? small
        : 0;

    /// <summary>Decimal value of a JSON property, 0 when missing.</summary>
    public static decimal Decimal(JsonObject node, string name) => node[name] is JsonValue value && value.TryGetValue<decimal>(out var number) ? number : 0m;
}

/// <summary>
/// The allowed transitions of a run (design of change 8, "Stavový automat"). A transition is
/// <c>UPDATE … WHERE id = @run AND status = @from</c> with the event <c>run.status</c>; when the row is in another state (a
/// concurrent change, a repeated job), nothing is written and the caller goes no further. Final states never change.
/// </summary>
public static class RunStateMachine
{
    private static readonly HashSet<(RunStatus From, RunStatus To)> Allowed =
    [
        (RunStatus.Queued, RunStatus.Discovering),
        (RunStatus.Discovering, RunStatus.AwaitingPayment),
        (RunStatus.AwaitingPayment, RunStatus.Crawling),
        (RunStatus.Discovering, RunStatus.Crawling),
        (RunStatus.Crawling, RunStatus.Profiling),
        (RunStatus.Profiling, RunStatus.Segmenting),
        (RunStatus.Segmenting, RunStatus.Evaluating),
        (RunStatus.Evaluating, RunStatus.Ruling),
        (RunStatus.Ruling, RunStatus.Rewriting),
        (RunStatus.Rewriting, RunStatus.Finished),
        (RunStatus.Rewriting, RunStatus.Partial),
    ];

    /// <summary>The final states.</summary>
    public static IReadOnlySet<RunStatus> Final { get; } = new HashSet<RunStatus> { RunStatus.Finished, RunStatus.Partial, RunStatus.Failed, RunStatus.Canceled };

    /// <summary>Whether the machine allows the transition (any non-final state may end <c>failed</c> or <c>canceled</c>).</summary>
    public static bool IsAllowed(RunStatus from, RunStatus to) =>
        !Final.Contains(from) && (Allowed.Contains((from, to)) || to is RunStatus.Failed or RunStatus.Canceled);

    /// <summary>Moves the run from <paramref name="from"/> to <paramref name="to"/>; false when the run is not in <paramref name="from"/>.</summary>
    /// <param name="error">Code of a failure (column <c>error</c>), only with <see cref="RunStatus.Failed"/>.</param>
    public static async Task<bool> TryTransitionAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid runId, RunStatus from, RunStatus to, CancellationToken ct, string? error = null)
    {
        if (!IsAllowed(from, to))
        {
            return false;
        }

        var final = Final.Contains(to);
        await using var command = new NpgsqlCommand(
            """
            UPDATE checks.runs
            SET status = $3,
                error = coalesce($4, error),
                started_at = CASE WHEN started_at IS NULL AND $3 <> 'queued' THEN now() ELSE started_at END,
                finished_at = CASE WHEN $5 THEN now() ELSE finished_at END,
                updated_at = now()
            WHERE id = $1 AND status = $2
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = runId },
                new NpgsqlParameter { Value = SnakeCaseEnumConverter<RunStatus>.ToText(from) },
                new NpgsqlParameter { Value = SnakeCaseEnumConverter<RunStatus>.ToText(to) },
                new NpgsqlParameter { Value = (object?)error ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text },
                new NpgsqlParameter { Value = final },
            },
        };
        if (await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) != 1)
        {
            return false;
        }

        await RunEventWriter.WriteAsync(connection, transaction, tenantId, runId, "info", RunCodes.EventStatus,
            new JsonObject { ["from"] = SnakeCaseEnumConverter<RunStatus>.ToText(from), ["to"] = SnakeCaseEnumConverter<RunStatus>.ToText(to) }, ct).ConfigureAwait(false);
        return true;
    }
}
