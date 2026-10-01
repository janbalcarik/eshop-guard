using EshopGuard.Core.Cache;
using EshopGuard.Core.Options;
using EshopGuard.Core.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace EshopGuard.Core.Tests;

/// <summary>The SQLite cache of the CLI (until change 5b) has the behaviour of every cache.</summary>
public sealed class SqliteJevCacheContractTests : JevCacheContractTests, IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("EshopGuard-cache-contract-").FullName;

    protected override IJevCache CreateCache()
    {
        var options = new EshopGuardOptions();
        options.Cache.Path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".sqlite");
        return new SqliteJevCache(Microsoft.Extensions.Options.Options.Create(options), NullLogger<SqliteJevCache>.Instance);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }
}
