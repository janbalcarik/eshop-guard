using EshopGuard.Data.Configurations.Conventions;

namespace EshopGuard.Data.Tests.Catalog;

[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class RlsCatalogTests(PostgresTestDatabase database)
{
    internal const string Schemas = "'iam', 'shop', 'content', 'checks', 'fixes', 'billing', 'usage', 'ops', 'ref'";

    [Fact]
    public async Task EveryTable_IsTenantTableWithForcedRls_OrListedAsGlobal()
    {
        var tables = await database.For("Owner").RowsAsync($"""
            SELECT n.nspname || '.' || c.relname,
                   c.relrowsecurity,
                   c.relforcerowsecurity,
                   EXISTS (SELECT 1 FROM pg_policy p WHERE p.polrelid = c.oid AND p.polname = 'tenant_isolation'
                           AND pg_get_expr(p.polqual, p.polrelid) LIKE '%ops.current_tenant_id()%'
                           AND pg_get_expr(p.polwithcheck, p.polrelid) LIKE '%ops.current_tenant_id()%')
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname IN ({Schemas}) AND c.relkind IN ('r', 'p') AND NOT c.relispartition AND c.relname <> '__ef_migrations_history'
            """);

        var problems = new List<string>();
        foreach (var row in tables)
        {
            var (name, rls, force, policy) = ((string)row[0], (bool)row[1], (bool)row[2], (bool)row[3]);
            if (TableNames.TenantTables.Contains(name))
            {
                if (!rls || !force || !policy)
                {
                    problems.Add($"{name}: RLS={rls}, FORCE={force}, tenant_isolation={policy}");
                }
            }
            else if (!TableNames.GlobalTables.Contains(name))
            {
                problems.Add($"{name}: neither a tenant table nor listed as global");
            }
            else if (rls)
            {
                problems.Add($"{name}: global table with RLS");
            }
        }

        Assert.Empty(problems);
        Assert.Equal(TableNames.TenantTables.Count + TableNames.GlobalTables.Count, tables.Count);
    }
}
