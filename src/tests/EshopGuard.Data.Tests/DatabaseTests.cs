namespace EshopGuard.Data.Tests;

[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class DatabaseTests(PostgresTestDatabase database)
{
    [Fact]
    public async Task TestDatabase_IsOwnedByOwnerRole()
    {
        Assert.Equal("eshopguard_owner", await database.For("Owner").ScalarAsync<string>(
            "SELECT pg_get_userbyid(datdba)::text FROM pg_database WHERE datname = current_database()"));
    }

    [Fact]
    public async Task DatabaseAcl_HasNoEntryForPublic()
    {
        var acl = await database.For("Owner").ScalarAsync<string>(
            "SELECT datacl::text FROM pg_database WHERE datname = current_database()");
        Assert.NotNull(acl);
        // A PUBLIC entry has an empty grantee: "=Tc/owner".
        Assert.DoesNotContain("{=", acl, StringComparison.Ordinal);
        Assert.DoesNotContain(",=", acl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublicSchema_HasNoPrivilegesForPublic()
    {
        Assert.False(await database.For("Owner").ScalarAsync<bool>(
            "SELECT has_schema_privilege('public', 'public', 'CREATE') OR has_schema_privilege('public', 'public', 'USAGE')"));
    }

    [Fact]
    public async Task SchemaCms_IsOwnedByCmsRole()
    {
        Assert.Equal("eshopguard_cms", await database.For("Owner").ScalarAsync<string>(
            "SELECT pg_get_userbyid(nspowner)::text FROM pg_namespace WHERE nspname = 'cms'"));
    }

    [Fact]
    public async Task Database_UsesBuiltinUtf8Locale()
    {
        Assert.Equal("b:C.UTF-8", await database.For("Owner").ScalarAsync<string>(
            "SELECT datlocprovider::text || ':' || datlocale FROM pg_database WHERE datname = current_database()"));
    }
}
