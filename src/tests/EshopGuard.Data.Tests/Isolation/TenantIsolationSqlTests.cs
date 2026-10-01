using EshopGuard.Data.Configurations.Conventions;
using Npgsql;

namespace EshopGuard.Data.Tests.Isolation;

[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class TenantIsolationSqlTests(TwoTenantsFixture tenants)
{
    public static TheoryData<string, string> TablesAndRoles()
    {
        var data = new TheoryData<string, string>();
        foreach (var table in TableNames.TenantTables)
        {
            data.Add(table, "App");
            data.Add(table, "Worker");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(TablesAndRoles))]
    public async Task Table_ReturnsOnlyRowsOfTheContextTenant(string table, string role)
    {
        var (all, foreign) = await IsolationSql.InTenantAsync(role, tenants.A.Tenant.TenantId, async (conn, tx) =>
        {
            await using var command = new NpgsqlCommand($"SELECT count(*), count(*) FILTER (WHERE tenant_id IS DISTINCT FROM $1) FROM {table}", conn, tx);
            command.Parameters.Add(new NpgsqlParameter { Value = tenants.A.Tenant.TenantId });
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            await reader.ReadAsync(TestContext.Current.CancellationToken);
            return (reader.GetInt64(0), reader.GetInt64(1));
        });

        Assert.True(all > 0, $"{table}: no rows of tenant A visible");
        Assert.Equal(0, foreign);
    }
}
