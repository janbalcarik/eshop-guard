using System.Text.Json;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Languages;
using EshopGuard.Core.Pipeline;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Jobs.Runs;

/// <summary>One crawl scope of a run as the steps use it (<c>checks.run_scopes</c>).</summary>
internal sealed record RunScopeRow(
    string ScopeKey,
    string BaseUrl,
    string? Language,
    VersionCrawlScope? Scope,
    RobotsSnapshot Robots,
    UrlFrontierState Frontier,
    PaceState Pace,
    bool Exhausted,
    int Batches)
{
    /// <summary>The site the scope belongs to, with its language version.</summary>
    public SiteScope Site => new(new Uri(BaseUrl)) { Version = Scope };

    /// <summary>Domain of the scope for the lease and the concurrency key (another domain of a version has its own).</summary>
    public string Domain => UrlTools.StripWww(new Uri(BaseUrl).Host.ToLowerInvariant());
}

/// <summary>Plain SQL over <c>checks.run_scopes</c>, always in a transaction of the run's tenant.</summary>
internal static class RunScopeStore
{
    private static JsonSerializerOptions Json => PipelineJson.Options;

    public static async Task InsertAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid runId, RunScopeRow scope, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO checks.run_scopes (tenant_id, run_id, scope_key, base_url, language, scope, robots, frontier, pace, exhausted, batches, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, now(), now())
            ON CONFLICT (run_id, scope_key) DO UPDATE SET scope = excluded.scope, robots = excluded.robots, frontier = excluded.frontier,
                pace = excluded.pace, exhausted = excluded.exhausted, batches = excluded.batches, updated_at = now()
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = tenantId },
                new NpgsqlParameter { Value = runId },
                new NpgsqlParameter { Value = scope.ScopeKey },
                new NpgsqlParameter { Value = scope.BaseUrl },
                new NpgsqlParameter { Value = (object?)scope.Language ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text },
                new NpgsqlParameter { Value = scope.Scope is null ? DBNull.Value : JsonSerializer.Serialize(scope.Scope, Json), NpgsqlDbType = NpgsqlDbType.Jsonb },
                new NpgsqlParameter { Value = JsonSerializer.Serialize(scope.Robots, Json), NpgsqlDbType = NpgsqlDbType.Jsonb },
                new NpgsqlParameter { Value = PipelineJson.Serialize(scope.Frontier), NpgsqlDbType = NpgsqlDbType.Jsonb },
                new NpgsqlParameter { Value = JsonSerializer.Serialize(scope.Pace, Json), NpgsqlDbType = NpgsqlDbType.Jsonb },
                new NpgsqlParameter { Value = scope.Exhausted },
                new NpgsqlParameter { Value = scope.Batches },
            },
        };
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>All scopes of the run, ordered by their key (the order gives each its index in the keys of jobs).</summary>
    public static async Task<List<RunScopeRow>> LoadAllAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT scope_key, base_url, language, scope::text, robots::text, frontier::text, pace::text, exhausted, batches
            FROM checks.run_scopes WHERE run_id = $1 ORDER BY scope_key COLLATE "C"
            """, connection, transaction)
        {
            Parameters = { new NpgsqlParameter { Value = runId } },
        };
        var rows = new List<RunScopeRow>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(new RunScopeRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : JsonSerializer.Deserialize<VersionCrawlScope>(reader.GetString(3), Json),
                JsonSerializer.Deserialize<RobotsSnapshot>(reader.GetString(4), Json)!,
                PipelineJson.Deserialize<UrlFrontierState>(reader.GetString(5)),
                JsonSerializer.Deserialize<PaceState>(reader.GetString(6), Json)!,
                reader.GetBoolean(7),
                reader.GetInt32(8)));
        }

        return rows;
    }

    /// <summary>Saves the state after a batch of downloads.</summary>
    public static async Task SaveBatchAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId, string scopeKey, UrlFrontierState frontier, PaceState pace, bool exhausted, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE checks.run_scopes SET frontier = $3, pace = $4, exhausted = $5, batches = batches + 1, updated_at = now()
            WHERE run_id = $1 AND scope_key = $2
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = runId },
                new NpgsqlParameter { Value = scopeKey },
                new NpgsqlParameter { Value = PipelineJson.Serialize(frontier), NpgsqlDbType = NpgsqlDbType.Jsonb },
                new NpgsqlParameter { Value = JsonSerializer.Serialize(pace, Json), NpgsqlDbType = NpgsqlDbType.Jsonb },
                new NpgsqlParameter { Value = exhausted },
            },
        };
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
