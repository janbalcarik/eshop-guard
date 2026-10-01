using System.Text.Json;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Storage;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Tenancy;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Data.Stores;

/// <summary>
/// Jev answers of a tenant in PostgreSQL: detailed questions in <c>checks.jev_answers</c>, the sieve in
/// <c>checks.sieve_answers</c>, both keyed by the legacy cache key (<see cref="JevCacheKey.LegacyKey"/>) under RLS. The
/// first stored answer of a key stays. The same class serves the CLI (tenant <c>cli</c>) and the worker.
/// </summary>
public sealed class PgJevCache(EshopGuardDataSource dataSource, IStoreTenant tenant, ILogger<PgJevCache> logger) : IJevCache
{
    /// <summary>Keys per query; arrays this long are still one round trip.</summary>
    private const int KeysPerQuery = 5_000;

    public async Task<JevResult?> GetAsync(JevCacheKey key, CancellationToken ct = default) =>
        (await GetManyAsync([key], ct).ConfigureAwait(false)).GetValueOrDefault(key);

    public async Task<IReadOnlyDictionary<JevCacheKey, JevResult>> GetManyAsync(IReadOnlyList<JevCacheKey> keys, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var found = new Dictionary<JevCacheKey, JevResult>();
        if (keys.Count == 0)
        {
            return found;
        }

        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenant.TenantId, ct: ct).ConfigureAwait(false);
        foreach (var kind in keys.GroupBy(k => k.Kind))
        {
            var byLegacy = kind.GroupBy(k => k.LegacyKey, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
            foreach (var chunk in byLegacy.Keys.Chunk(KeysPerQuery))
            {
                await using var command = new NpgsqlCommand(
                    $"SELECT cache_key, response::text FROM {Table(kind.Key)} WHERE tenant_id = $1 AND cache_key = ANY($2)", connection, transaction)
                {
                    Parameters =
                    {
                        new NpgsqlParameter { Value = tenant.TenantId },
                        new NpgsqlParameter { Value = chunk, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text },
                    },
                };
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    var legacy = reader.GetString(0);
                    if (Parse(legacy, reader.GetString(1)) is { } result)
                    {
                        foreach (var key in byLegacy[legacy])
                        {
                            found[key] = result;
                        }
                    }
                }
            }
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return found;
    }

    public async Task SetAsync(JevCacheKey key, JevResult result, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenant.TenantId, ct: ct).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            $"""
            INSERT INTO {Table(key.Kind)} (tenant_id, cache_key, response, model, question_set_hash)
            VALUES ($1, $2, $3, $4, $5)
            ON CONFLICT (tenant_id, cache_key) DO NOTHING
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = tenant.TenantId },
                new NpgsqlParameter { Value = key.LegacyKey },
                new NpgsqlParameter { Value = JevCacheJson.Serialize(result), NpgsqlDbType = NpgsqlDbType.Jsonb },
                new NpgsqlParameter { Value = result.Model ?? "" },
                new NpgsqlParameter { Value = (object?)HashBytes(key.QuestionSetHash) ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Bytea },
            },
        };
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    private static string Table(JevCacheKind kind) => kind == JevCacheKind.Sieve ? "checks.sieve_answers" : "checks.jev_answers";

    /// <summary>The 32 bytes of a <c>sha256:…</c> hash, for cleaning up answers of retired question sets.</summary>
    internal static byte[]? HashBytes(string? hash)
    {
        const string prefix = "sha256:";
        return hash is { Length: 71 } && hash.StartsWith(prefix, StringComparison.Ordinal) ? Convert.FromHexString(hash.AsSpan(prefix.Length)) : null;
    }

    private JevResult? Parse(string key, string json)
    {
        try
        {
            return JevCacheJson.Deserialize(json);
        }
        catch (JsonException ex)
        {
            // An unreadable answer is asked again, never guessed.
            logger.LogWarning("Cached Jev answer {Key} is not valid ({Message}); it is asked again", key, ex.Message);
            return null;
        }
    }
}
