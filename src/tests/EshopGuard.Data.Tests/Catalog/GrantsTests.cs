using EshopGuard.Data.Configurations.Conventions;

namespace EshopGuard.Data.Tests.Catalog;

/// <summary>Privileges of the application roles according to the matrix of the change 3 design.</summary>
[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class GrantsTests(PostgresTestDatabase database)
{
    private const string Siud = "SIUD";

    /// <summary>Table → privileges (S, I, U, D) of app, worker and admin; "" = none.</summary>
    private static readonly Dictionary<string, (string App, string Worker, string Admin)> Matrix = BuildMatrix();

    private static Dictionary<string, (string, string, string)> BuildMatrix()
    {
        var m = TableNames.TenantTables.ToDictionary(t => t, _ => (Siud, Siud, Siud));
        foreach (var t in new[] { "iam.tenants", "iam.users", "iam.user_logins", "iam.user_tokens" }) m[t] = ("SIU", "S", Siud);
        foreach (var t in new[] { "checks.rule_sets", "billing.price_lists", "billing.price_tiers", "billing.volume_discounts", "billing.promo_codes", "ref.markets", "ref.locales" }) m[t] = ("S", "SIU", Siud);
        m["shop.free_sample_claims"] = ("SI", "SI", Siud);
        m["billing.stripe_events"] = ("SIU", "SU", Siud);
        m["usage.usage_records"] = (string.Empty, "SIU", "S");
        m["usage.usage_daily"] = (string.Empty, "SIU", "S");
        m["ops.jobs"] = ("SIU", Siud, Siud);
        m["ops.workers"] = ("S", Siud, Siud);
        m["ops.domains"] = ("S", Siud, Siud);
        m["ops.rate_limit_buckets"] = ("SIU", "SIU", Siud);
        m["ops.system_settings"] = ("S", "SU", Siud);
        return m;
    }

    [Fact]
    public void Matrix_CoversEveryTable() =>
        Assert.Equal(TableNames.TenantTables.Concat(TableNames.GlobalTables).Order(StringComparer.Ordinal), Matrix.Keys.Order(StringComparer.Ordinal));

    [Fact]
    public async Task Roles_HaveExactlyTheMatrixPrivileges()
    {
        var problems = new List<string>();
        foreach (var (table, expected) in Matrix)
        {
            foreach (var (role, privileges) in new[] { ("eshopguard_app", expected.App), ("eshopguard_worker", expected.Worker), ("eshopguard_admin", expected.Admin) })
            {
                foreach (var (letter, privilege) in new[] { ('S', "SELECT"), ('I', "INSERT"), ('U', "UPDATE"), ('D', "DELETE") })
                {
                    var has = await database.For("Owner").ScalarAsync<bool>($"SELECT has_table_privilege('{role}', '{table}', '{privilege}')");
                    if (has != privileges.Contains(letter, StringComparison.Ordinal))
                    {
                        problems.Add($"{role} {privilege} {table}: {has}");
                    }
                }
            }
        }

        Assert.Empty(problems);
    }

    [Fact]
    public async Task ApplicationRoles_NeverGetTruncateReferencesOrTrigger()
    {
        var granted = await database.For("Owner").ColumnAsync<string>($"""
            SELECT DISTINCT r.rolname || ' ' || a.privilege_type || ' ' || n.nspname || '.' || c.relname
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            CROSS JOIN LATERAL aclexplode(c.relacl) a JOIN pg_roles r ON r.oid = a.grantee
            WHERE n.nspname IN ({RlsCatalogTests.Schemas}) AND r.rolname IN ('eshopguard_app', 'eshopguard_worker')
              AND a.privilege_type IN ('TRUNCATE', 'REFERENCES', 'TRIGGER')
            """);
        Assert.Empty(granted);
    }

    [Fact]
    public async Task Partitions_HaveNoPrivilegesForApplicationRoles()
    {
        var granted = await database.For("Owner").ColumnAsync<string>($"""
            SELECT DISTINCT r.rolname || ' ' || n.nspname || '.' || c.relname
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            CROSS JOIN LATERAL aclexplode(c.relacl) a JOIN pg_roles r ON r.oid = a.grantee
            WHERE c.relispartition AND r.rolname IN ('eshopguard_app', 'eshopguard_worker', 'eshopguard_admin')
            """);
        Assert.Empty(granted);
        Assert.Equal(Npgsql.PostgresErrorCodes.InsufficientPrivilege, await database.For("App").SqlStateAsync("SELECT * FROM content.pages_p00"));
    }

    [Fact]
    public async Task CmsAndPublic_HaveNothingInTheNineSchemas()
    {
        var granted = await database.For("Owner").ColumnAsync<string>($"""
            SELECT DISTINCT coalesce(r.rolname, 'PUBLIC') || ' ' || n.nspname || '.' || c.relname
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            CROSS JOIN LATERAL aclexplode(c.relacl) a LEFT JOIN pg_roles r ON r.oid = a.grantee
            WHERE n.nspname IN ({RlsCatalogTests.Schemas}) AND (a.grantee = 0 OR r.rolname = 'eshopguard_cms')
            """);
        Assert.Empty(granted);
        Assert.Equal(Npgsql.PostgresErrorCodes.InsufficientPrivilege, await database.For("Cms").SqlStateAsync("SELECT * FROM iam.users"));
        Assert.Equal(Npgsql.PostgresErrorCodes.InsufficientPrivilege, await database.For("Cms").SqlStateAsync("SELECT * FROM shop.shops"));
    }

    [Fact]
    public async Task Truncate_AsApp_IsRefused()
    {
        Assert.Equal(Npgsql.PostgresErrorCodes.InsufficientPrivilege, await database.For("App").SqlStateAsync("TRUNCATE shop.shops"));
    }
}
