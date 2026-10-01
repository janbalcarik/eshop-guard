using Npgsql;

namespace EshopGuard.Data.Tests.Isolation;

/// <summary>
/// References across tenants fail on the composite foreign keys even for <c>eshopguard_admin</c>, which bypasses RLS.
/// </summary>
[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class CompositeForeignKeyTests(PostgresTestDatabase database, TwoTenantsFixture tenants)
{
    [Fact]
    public async Task PageOfTenantA_WithShopOfTenantB_Fails()
    {
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, await database.For("Admin").SqlStateAsync(
            $"INSERT INTO content.pages (id, tenant_id, shop_id, url, url_hash, source, status, first_seen_at, rotation_bucket) VALUES (uuidv7(), '{tenants.A.Tenant.TenantId}', '{tenants.B.ShopId}', 'https://x/', 1, 'crawl', 'active', now(), 0)"));
    }

    [Fact]
    public async Task OccurrenceOfTenantA_WithPageOfTenantB_Fails()
    {
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, await database.For("Admin").SqlStateAsync(
            $"INSERT INTO checks.finding_occurrences (tenant_id, finding_id, page_id, shop_id) VALUES ('{tenants.A.Tenant.TenantId}', '{tenants.A.FindingId}', '{tenants.B.PageId}', '{tenants.B.ShopId}')"));
    }

    [Fact]
    public async Task ProposalOfTenantA_WithGroupOfTenantB_Fails()
    {
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, await database.For("Admin").SqlStateAsync(
            $"""
            INSERT INTO fixes.fix_proposals (id, tenant_id, shop_id, page_id, page_version_id, group_id, field, original_text, proposed_text, recheck_status, status)
            VALUES (uuidv7(), '{tenants.A.Tenant.TenantId}', '{tenants.A.ShopId}', '{tenants.A.PageId}', '{tenants.A.PageVersionId}', '{tenants.B.FixGroupId}', 'description', 'a', 'b', 'pending', 'proposed')
            """));
    }

    [Fact]
    public async Task SameRows_WithReferencesInsideTenantA_Succeed()
    {
        Assert.Null(await database.For("Admin").SqlStateAsync(
            $"INSERT INTO content.pages (id, tenant_id, shop_id, url, url_hash, source, status, first_seen_at, rotation_bucket) VALUES (uuidv7(), '{tenants.A.Tenant.TenantId}', '{tenants.A.ShopId}', 'https://x/', {Random.Shared.NextInt64()}, 'crawl', 'active', now(), 0)"));
    }
}
