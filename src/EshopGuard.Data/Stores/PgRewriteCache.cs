using EshopGuard.Core.Storage;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Data.Stores;

/// <summary>Rewrites of a tenant in <c>fixes.rewrite_cache</c> (key of page, findings, model and prompt) under RLS.</summary>
public sealed class PgRewriteCache(EshopGuardDataSource dataSource, IStoreTenant tenant) : IRewriteCache
{
    public async Task<(string Json, string? Model)?> GetAsync(string key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenant.TenantId, ct: ct).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT answer::text, model FROM fixes.rewrite_cache WHERE tenant_id = $1 AND key = $2", connection, transaction)
        {
            Parameters = { new NpgsqlParameter { Value = tenant.TenantId }, new NpgsqlParameter { Value = key } },
        };
        (string Json, string? Model)? answer = null;
        await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var model = reader.GetString(1);
                answer = (reader.GetString(0), model.Length == 0 ? null : model);
            }
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return answer;
    }

    public async Task SetAsync(string key, string json, string? model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(json);
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenant.TenantId, ct: ct).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO fixes.rewrite_cache (tenant_id, key, answer, model) VALUES ($1, $2, $3, $4)
            ON CONFLICT (tenant_id, key) DO UPDATE SET answer = excluded.answer, model = excluded.model, created_at = now()
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = tenant.TenantId },
                new NpgsqlParameter { Value = key },
                new NpgsqlParameter { Value = json, NpgsqlDbType = NpgsqlDbType.Jsonb },
                new NpgsqlParameter { Value = model ?? "" },
            },
        };
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }
}
