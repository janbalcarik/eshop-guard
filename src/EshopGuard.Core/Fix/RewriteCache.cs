using System.Globalization;
using EshopGuard.Core.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Fix;

/// <summary>
/// Cache of rewrites: the same page with the same findings, model and prompt is never paid for twice.
/// </summary>
public interface IRewriteCache
{
    /// <summary>Returns the cached answer (JSON and model), or null.</summary>
    Task<(string Json, string? Model)?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Stores an answer.</summary>
    Task SetAsync(string key, string json, string? model, CancellationToken ct = default);
}

/// <summary>
/// No cache: with <c>--no-cache</c> and with the mock client.
/// </summary>
internal sealed class NullRewriteCache : IRewriteCache
{
    public Task<(string Json, string? Model)?> GetAsync(string key, CancellationToken ct = default) =>
        Task.FromResult<(string Json, string? Model)?>(null);

    public Task SetAsync(string key, string json, string? model, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>
/// Table <c>rewrite_cache(key, answer_json, model, created_at)</c> in the SQLite file of the Jev cache.
/// </summary>
internal sealed class SqliteRewriteCache : IRewriteCache
{
    private readonly string _path;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private volatile bool _schemaReady;

    public SqliteRewriteCache(IOptions<EshopGuardOptions> options, ILogger<SqliteRewriteCache> logger)
    {
        _path = Path.GetFullPath(options.Value.Cache.Path);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 30,
        }.ToString();
        logger.LogDebug("Rewrite cache: {Path}", _path);
    }

    public async Task<(string Json, string? Model)?> GetAsync(string key, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT answer_json, model FROM rewrite_cache WHERE key = $key";
        command.Parameters.AddWithValue("$key", key);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    public async Task SetAsync(string key, string json, string? model, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO rewrite_cache (key, answer_json, model, created_at) VALUES ($key, $json, $model, $createdAt)";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$json", json);
        command.Parameters.AddWithValue("$model", (object?)model ?? DBNull.Value);
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
                        CREATE TABLE IF NOT EXISTS rewrite_cache (
                            key TEXT PRIMARY KEY,
                            answer_json TEXT NOT NULL,
                            model TEXT,
                            created_at TEXT NOT NULL
                        );
                        """;
                    await command.ExecuteNonQueryAsync(ct);
                    _schemaReady = true;
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
