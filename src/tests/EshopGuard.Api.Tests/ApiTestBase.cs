using EshopGuard.Tests.Shared;
using Npgsql;

namespace EshopGuard.Api.Tests;

/// <summary>
/// Base of the tests of change 9 against <c>eshopguard_test</c> (role <c>eshopguard_app</c> in the API): migrations applied,
/// unique e-mails. The languages <c>sk</c> and <c>cs</c> are enabled only in the catalog of the factory
/// (<see cref="ApiFactory"/>), never in the shared database, where they stay off as seeded.
/// </summary>
[Trait("Category", "Db")]
public abstract class ApiTestBase : IAsyncLifetime
{
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A new e-mail address of the test.</summary>
    protected static string NewEmail(string name = "jana") => $"{name}.{Guid.NewGuid():N}@bylinkovo-test.sk";

    public virtual async ValueTask InitializeAsync() => await TestDatabase.EnsureMigratedAsync(Ct);

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    internal static ApiFactory Factory(Microsoft.Extensions.Time.Testing.FakeTimeProvider? time = null, string? ipHashKey = null,
        IReadOnlyDictionary<string, string?>? settings = null, Action<Microsoft.Extensions.DependencyInjection.IServiceCollection>? services = null) =>
        new(TestConfiguration.ConnectionString("App"), time: time, ipHashKey: ipHashKey, settings: settings, services: services);

    /// <summary>Runs SQL as eshopguard_admin (BYPASSRLS: setup and checks across tenants that RLS hides from the application).</summary>
    protected internal static async Task<int> AdminAsync(string sql, params object?[] parameters)
    {
        await using var connection = new NpgsqlConnection(TestConfiguration.ConnectionString("Admin"));
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = parameter ?? DBNull.Value });
        }

        return await command.ExecuteNonQueryAsync(Ct);
    }

    /// <summary>Rows of a query as eshopguard_admin.</summary>
    protected internal static async Task<List<object?[]>> AdminRowsAsync(string sql, params object?[] parameters)
    {
        await using var connection = new NpgsqlConnection(TestConfiguration.ConnectionString("Admin"));
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = parameter ?? DBNull.Value });
        }

        var rows = new List<object?[]>();
        await using var reader = await command.ExecuteReaderAsync(Ct);
        while (await reader.ReadAsync(Ct))
        {
            var row = new object[reader.FieldCount];
            reader.GetValues(row);
            rows.Add(row.Select(v => v is DBNull ? null : v).ToArray());
        }

        return rows;
    }

    protected internal static async Task<T?> AdminScalarAsync<T>(string sql, params object?[] parameters)
    {
        var rows = await AdminRowsAsync(sql, parameters);
        return rows.Count == 0 || rows[0][0] is null ? default : (T)rows[0][0]!;
    }
}
