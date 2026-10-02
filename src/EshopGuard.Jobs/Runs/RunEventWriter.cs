using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Jobs.Runs;

/// <summary>
/// Writes progress events of a run (<c>checks.run_events</c>) and, in the same transaction, <c>pg_notify('run_events', run_id)</c>:
/// the notification arrives only after the commit, so the progress screen (SSE, change 10) never shows an uncommitted state.
/// Events carry codes and numbers only: no text of a page, no internal price, no name of a provider (K rozhodnutí 14).
/// </summary>
public static class RunEventWriter
{
    /// <summary>Notification channel; the payload is the run id.</summary>
    public const string Channel = "run_events";

    public static async Task WriteAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid runId, string level, string code, JsonObject? data, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await using var command = new NpgsqlCommand(
            """
            WITH inserted AS (
                INSERT INTO checks.run_events (tenant_id, run_id, at, level, code, data, created_at)
                VALUES ($1, $2, now(), $3, $4, $5, now())
                RETURNING run_id)
            SELECT pg_notify('run_events', run_id::text) FROM inserted
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = tenantId },
                new NpgsqlParameter { Value = runId },
                new NpgsqlParameter { Value = level },
                new NpgsqlParameter { Value = code },
                new NpgsqlParameter { Value = (object?)data?.ToJsonString() ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Jsonb },
            },
        };
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
