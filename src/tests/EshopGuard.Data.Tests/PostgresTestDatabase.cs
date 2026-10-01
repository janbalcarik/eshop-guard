using EshopGuard.Tests.Shared;
using Npgsql;

namespace EshopGuard.Data.Tests;

/// <summary>
/// Test database <c>eshopguard_test</c> after migrations (as <c>eshopguard_owner</c>), with a pool per role.
/// Without configuration every test of the collection fails with the name of the missing key.
/// </summary>
public sealed class PostgresTestDatabase : IAsyncLifetime
{
    private readonly Dictionary<string, NpgsqlDataSource> _sources = [];

    /// <inheritdoc />
    public async ValueTask InitializeAsync() => await TestDatabase.EnsureMigratedAsync();

    /// <summary>Pool for <c>ConnectionStrings:{name}</c> (Owner, App, Worker, Admin, Cms).</summary>
    public NpgsqlDataSource For(string name)
    {
        lock (_sources)
        {
            if (!_sources.TryGetValue(name, out var source))
            {
                source = NpgsqlDataSource.Create(TestConfiguration.ConnectionString(name));
                _sources[name] = source;
            }

            return source;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var source in _sources.Values)
        {
            await source.DisposeAsync();
        }
    }
}

/// <summary>Collection of tests that need PostgreSQL.</summary>
[CollectionDefinition(Name)]
public sealed class DbCollection : ICollectionFixture<PostgresTestDatabase>, ICollectionFixture<Isolation.TwoTenantsFixture>
{
    /// <summary>Collection name.</summary>
    public const string Name = "Db";
}

/// <summary>Helpers for SQL in tests.</summary>
internal static class Sql
{
    public static async Task<T?> ScalarAsync<T>(this NpgsqlDataSource source, string sql)
    {
        await using var command = source.CreateCommand(sql);
        var value = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return value is DBNull or null ? default : (T)value;
    }

    public static async Task<List<object[]>> RowsAsync(this NpgsqlDataSource source, string sql)
    {
        await using var command = source.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var rows = new List<object[]>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            var row = new object[reader.FieldCount];
            reader.GetValues(row);
            rows.Add(row);
        }

        return rows;
    }

    public static async Task<List<T>> ColumnAsync<T>(this NpgsqlDataSource source, string sql)
    {
        await using var command = source.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var values = new List<T>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            values.Add(reader.GetFieldValue<T>(0));
        }

        return values;
    }

    /// <summary>Runs the statement in a rolled-back transaction and returns the SQLSTATE of the failure, or null.</summary>
    public static async Task<string?> SqlStateAsync(this NpgsqlDataSource source, string sql)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync(ct);
            return null;
        }
        catch (PostgresException ex)
        {
            return ex.SqlState;
        }
        finally
        {
            await transaction.RollbackAsync(ct);
        }
    }
}
