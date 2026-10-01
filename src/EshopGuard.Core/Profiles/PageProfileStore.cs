using System.Globalization;
using System.Text.Json;
using EshopGuard.Core.Options;
using EshopGuard.Core.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Profiles;

/// <summary>
/// Table <c>page_profiles(id, site, profile_json, created_at)</c> in the SQLite file of the Jev cache. Reading a file that
/// does not exist yet returns nothing and creates nothing, so runs that write no profile leave no file behind.
/// </summary>
internal sealed class SqlitePageProfileStore : IPageProfileStore
{
    internal static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly string _path;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private volatile bool _schemaReady;

    public SqlitePageProfileStore(IOptions<EshopGuardOptions> options, ILogger<SqlitePageProfileStore> logger)
    {
        _path = Path.GetFullPath(options.Value.Cache.Path);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 30,
        }.ToString();
        logger.LogDebug("Page profiles: {Path}", _path);
    }

    public async Task<IReadOnlyList<PageProfile>> GetAsync(string site, CancellationToken ct = default)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT profile_json FROM page_profiles WHERE site = $site ORDER BY created_at, id";
        command.Parameters.AddWithValue("$site", site);
        var profiles = new List<PageProfile>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (JsonSerializer.Deserialize<PageProfile>(reader.GetString(0), Json) is { } profile)
            {
                profiles.Add(profile);
            }
        }

        return profiles;
    }

    public async Task AddAsync(PageProfile profile, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO page_profiles (id, site, profile_json, created_at) VALUES ($id, $site, $json, $createdAt)";
        command.Parameters.AddWithValue("$id", profile.Id);
        command.Parameters.AddWithValue("$site", profile.Site);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(profile, Json));
        command.Parameters.AddWithValue("$createdAt", profile.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
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
                        CREATE TABLE IF NOT EXISTS page_profiles (
                            id TEXT PRIMARY KEY,
                            site TEXT NOT NULL,
                            profile_json TEXT NOT NULL,
                            created_at TEXT NOT NULL
                        );
                        CREATE INDEX IF NOT EXISTS page_profiles_site ON page_profiles (site);
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
