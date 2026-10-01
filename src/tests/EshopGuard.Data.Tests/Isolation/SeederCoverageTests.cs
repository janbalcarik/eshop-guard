using EshopGuard.Data.Configurations.Conventions;

namespace EshopGuard.Data.Tests.Isolation;

[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class SeederCoverageTests(PostgresTestDatabase database, TwoTenantsFixture tenants)
{
    [Fact]
    public async Task Seeder_CoversEveryTableWithRls()
    {
        var withRls = await database.For("Owner").ColumnAsync<string>("""
            SELECT n.nspname || '.' || c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relrowsecurity AND NOT c.relispartition ORDER BY 1
            """);
        Assert.Equal(TableNames.TenantTables.Order(StringComparer.Ordinal), withRls);
        Assert.Equal(TableNames.TenantTables.Order(StringComparer.Ordinal), tenants.A.Tables.Order(StringComparer.Ordinal));
        Assert.Equal(TableNames.TenantTables.Order(StringComparer.Ordinal), tenants.B.Tables.Order(StringComparer.Ordinal));
    }
}
