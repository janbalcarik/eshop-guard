using Npgsql;

namespace EshopGuard.Data.Tests;

[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class RolesTests(PostgresTestDatabase database)
{
    [Theory]
    [InlineData("eshopguard_owner", false)]
    [InlineData("eshopguard_app", false)]
    [InlineData("eshopguard_worker", false)]
    [InlineData("eshopguard_cms", false)]
    [InlineData("eshopguard_admin", true)]
    public async Task Role_HasNoSuperuserAndBypassesRlsOnlyWhenAdmin(string role, bool bypassesRls)
    {
        await using var command = database.For("Owner").CreateCommand(
            "SELECT rolsuper, rolbypassrls, rolcanlogin, rolcreatedb, rolcreaterole, rolreplication FROM pg_roles WHERE rolname = $1");
        command.Parameters.Add(new NpgsqlParameter { Value = role });
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken), $"Role {role} does not exist.");
        Assert.False(reader.GetBoolean(0));
        Assert.Equal(bypassesRls, reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
        Assert.False(reader.GetBoolean(3));
        Assert.False(reader.GetBoolean(4));
        Assert.False(reader.GetBoolean(5));
    }

    [Theory]
    [InlineData("App", "CREATE TABLE ops.x (id int)")]
    [InlineData("App", "CREATE TABLE public.x (id int)")]
    [InlineData("Worker", "CREATE TABLE ops.x (id int)")]
    [InlineData("Worker", "CREATE TABLE public.x (id int)")]
    [InlineData("Cms", "CREATE TABLE ops.x (id int)")]
    [InlineData("Cms", "SELECT 1 FROM ops.__ef_migrations_history")]
    public async Task ApplicationRoles_CannotChangeSchemaOrReadOutsideTheirScope(string connection, string sql)
    {
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await database.For(connection).SqlStateAsync(sql));
    }

    [Fact]
    public async Task CmsRole_CanCreateTablesInSchemaCms()
    {
        Assert.Null(await database.For("Cms").SqlStateAsync("CREATE TABLE cms.t (id int)"));
    }
}
