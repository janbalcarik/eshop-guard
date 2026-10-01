using System.Globalization;
using System.Text.Json;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Options;
using EshopGuard.Core.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Cache;

/// <summary>
/// Cache in a local SQLite file: table <c>cache(key, response_json, created_at)</c>, keyed by
/// <see cref="JevCacheKey.LegacyKey"/> (the key before change 5, so stored answers stay valid). Transitional: change 5b
/// moves the CLI cache to PostgreSQL.
/// </summary>
internal sealed class SqliteJevCache : IJevCache
{
    private readonly string _path;
    private readonly string _connectionString;
    private readonly ILogger<SqliteJevCache> _logger;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private volatile bool _schemaReady;

    public SqliteJevCache(IOptions<EshopGuardOptions> options, ILogger<SqliteJevCache> logger)
    {
        _path = Path.GetFullPath(options.Value.Cache.Path);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 30,
        }.ToString();
        _logger = logger;
    }

    /// <summary>Keys per query of <see cref="GetManyAsync"/> (SQLite allows 32 766 parameters).</summary>
    private const int KeysPerQuery = 500;

    public async Task<JevResult?> GetAsync(JevCacheKey key, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT response_json FROM cache WHERE key = $key";
        command.Parameters.AddWithValue("$key", key.LegacyKey);
        return await command.ExecuteScalarAsync(ct) is string json ? Parse(key.LegacyKey, json) : null;
    }

    /// <summary>One <c>IN</c> query per 500 keys.</summary>
    public async Task<IReadOnlyDictionary<JevCacheKey, JevResult>> GetManyAsync(IReadOnlyList<JevCacheKey> keys, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var found = new Dictionary<JevCacheKey, JevResult>();
        if (keys.Count == 0)
        {
            return found;
        }

        var byLegacy = keys.GroupBy(k => k.LegacyKey, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        await using var connection = await OpenAsync(ct);
        foreach (var chunk in byLegacy.Keys.Chunk(KeysPerQuery))
        {
            await using var command = connection.CreateCommand();
            var names = chunk.Select((_, i) => "$k" + i.ToString(CultureInfo.InvariantCulture)).ToList();
            command.CommandText = $"SELECT key, response_json FROM cache WHERE key IN ({string.Join(", ", names)})";
            for (var i = 0; i < chunk.Length; i++)
            {
                command.Parameters.AddWithValue(names[i], chunk[i]);
            }

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
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

        return found;
    }

    private JevResult? Parse(string key, string json)
    {
        try
        {
            return JsonSerializer.Deserialize<JevResult>(json, JevClient.Json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Cached response {Key} is not valid, ignoring it", key);
            return null;
        }
    }

    public async Task SetAsync(JevCacheKey key, JevResult result, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO cache (key, response_json, created_at) VALUES ($key, $json, $createdAt)";
        command.Parameters.AddWithValue("$key", key.LegacyKey);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(result, JevClient.Json));
        command.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        if (!_schemaReady)
        {
            await _schemaLock.WaitAsync(ct);
            try
            {
                if (!_schemaReady)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                    await using var setup = new SqliteConnection(_connectionString);
                    await setup.OpenAsync(ct);
                    await using var command = setup.CreateCommand();
                    command.CommandText = """
                        PRAGMA journal_mode = WAL;
                        CREATE TABLE IF NOT EXISTS cache (
                            key TEXT PRIMARY KEY,
                            response_json TEXT NOT NULL,
                            created_at TEXT NOT NULL
                        );
                        """;
                    await command.ExecuteNonQueryAsync(ct);
                    _schemaReady = true;
                    _logger.LogInformation("Jev cache: {Path}", _path);
                }
            }
            finally
            {
                _schemaLock.Release();
            }
        }

        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
