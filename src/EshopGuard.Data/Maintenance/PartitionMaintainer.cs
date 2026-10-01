using System.Globalization;
using EshopGuard.Data.Connections;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EshopGuard.Data.Maintenance;

/// <summary>
/// Creates monthly partitions ahead of time through <c>ops.ensure_monthly_partitions</c> (worker and admin only; the
/// worker schedules it in change 4) and reports how far ahead they reach (for monitoring in change 17).
/// </summary>
public sealed class PartitionMaintainer(EshopGuardDataSource dataSource, ILogger<PartitionMaintainer> logger)
{
    /// <summary>The four tables partitioned by month.</summary>
    public static IReadOnlyList<string> MonthlyTables { get; } = ["shop.connector_events", "checks.run_events", "usage.usage_records", "ops.audit_log"];

    /// <summary>Creates missing partitions for the current month (UTC) and <paramref name="monthsAhead"/> more; returns their names.</summary>
    public async Task<IReadOnlyList<string>> EnsureMonthlyPartitionsAsync(int monthsAhead, CancellationToken ct = default)
    {
        await using var command = dataSource.Source.CreateCommand("SELECT name FROM ops.ensure_monthly_partitions($1) AS name");
        command.Parameters.Add(new NpgsqlParameter { Value = monthsAhead });
        var created = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                created.Add(reader.GetString(0));
            }
        }

        logger.LogInformation("partitions.ensured {Count} {Partitions}", created.Count, string.Join(", ", created));
        return created;
    }

    /// <summary>First day (UTC) of the last month covered by a partition, for each monthly table (<c>null</c> = no partition).</summary>
    public async Task<IReadOnlyDictionary<string, DateOnly?>> GetMonthlyHorizonAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT n.nspname || '.' || p.relname, pg_get_expr(c.relpartbound, c.oid)
            FROM pg_class p
            JOIN pg_namespace n ON n.oid = p.relnamespace
            LEFT JOIN pg_inherits i ON i.inhparent = p.oid
            LEFT JOIN pg_class c ON c.oid = i.inhrelid
            WHERE n.nspname || '.' || p.relname = ANY($1)
            """;
        await using var command = dataSource.Source.CreateCommand(sql);
        command.Parameters.Add(new NpgsqlParameter { Value = MonthlyTables.ToArray() });
        var horizon = MonthlyTables.ToDictionary(t => t, _ => (DateOnly?)null);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            if (reader.IsDBNull(1))
            {
                continue;
            }

            // FOR VALUES FROM ('2026-10-01 00:00:00+00') TO ('2026-11-01 00:00:00+00')
            var bound = reader.GetString(1);
            var to = bound[(bound.IndexOf(" TO ('", StringComparison.Ordinal) + 6)..];
            var upper = DateTimeOffset.Parse(to[..to.IndexOf('\'', StringComparison.Ordinal)], CultureInfo.InvariantCulture).UtcDateTime;
            var lastMonth = DateOnly.FromDateTime(upper).AddMonths(-1);
            var table = reader.GetString(0);
            if (horizon[table] is not { } current || lastMonth > current)
            {
                horizon[table] = lastMonth;
            }
        }

        return horizon;
    }
}
