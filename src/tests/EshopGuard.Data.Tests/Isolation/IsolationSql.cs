using EshopGuard.Data.Tenancy;
using EshopGuard.Tests.Shared;
using Npgsql;

namespace EshopGuard.Data.Tests.Isolation;

/// <summary>Plain SQL as an application role inside a transaction with <c>app.tenant_id</c>.</summary>
internal static class IsolationSql
{
    public static async Task<T> InTenantAsync<T>(string connection, Guid tenantId, Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> work)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(TestConfiguration.ConnectionString(connection));
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var tx = await TenantSql.BeginAsync(conn, tenantId, ct: ct);
        try
        {
            return await work(conn, tx);
        }
        finally
        {
            await tx.RollbackAsync(ct);
        }
    }

    /// <summary>Runs the statement and returns its SQLSTATE, or <c>null</c> when it succeeded.</summary>
    public static async Task<string?> SqlStateAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, params object[] parameters)
    {
        await tx.SaveAsync("s", TestContext.Current.CancellationToken);
        try
        {
            await using var command = new NpgsqlCommand(sql, conn, tx);
            foreach (var value in parameters)
            {
                command.Parameters.Add(new NpgsqlParameter { Value = value });
            }

            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            return null;
        }
        catch (PostgresException ex)
        {
            await tx.RollbackAsync("s", TestContext.Current.CancellationToken);
            return ex.SqlState;
        }
    }
}
