using EshopGuard.Data.Tests.Isolation;
using Npgsql;

namespace EshopGuard.Data.Tests.Partitioning;

[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class PartitioningTests(PostgresTestDatabase database, TwoTenantsFixture tenants)
{
    [Theory]
    [InlineData("content.pages", "h", 16)]
    [InlineData("content.page_versions", "h", 32)]
    [InlineData("checks.jev_answers", "h", 32)]
    [InlineData("checks.sieve_answers", "h", 32)]
    public async Task HashTables_HaveTheirPartitionCount(string table, string strategy, int partitions)
    {
        Assert.Equal(strategy, await database.For("Owner").ScalarAsync<string>($"SELECT partstrat::text FROM pg_partitioned_table WHERE partrelid = '{table}'::regclass"));
        Assert.Equal(partitions, await database.For("Owner").ScalarAsync<long>($"SELECT count(*) FROM pg_inherits WHERE inhparent = '{table}'::regclass"));
    }

    [Theory]
    [InlineData("shop.connector_events")]
    [InlineData("checks.run_events")]
    [InlineData("usage.usage_records")]
    [InlineData("ops.audit_log")]
    public async Task MonthlyTables_HaveThisMonthAndThreeMore(string table)
    {
        Assert.Equal("r", await database.For("Owner").ScalarAsync<string>($"SELECT partstrat::text FROM pg_partitioned_table WHERE partrelid = '{table}'::regclass"));
        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var m = 0; m <= 3; m++)
        {
            var name = $"{table}_y{month.AddMonths(m):yyyy}m{month.AddMonths(m):MM}";
            Assert.True(await database.For("Owner").ScalarAsync<bool>($"SELECT to_regclass('{name}') IS NOT NULL"), name);
        }
    }

    [Fact]
    public async Task RowForAMonthWithoutPartition_IsRefused()
    {
        var state = await IsolationSql.InTenantAsync("Worker", tenants.A.Tenant.TenantId, (conn, tx) => IsolationSql.SqlStateAsync(conn, tx,
            "INSERT INTO checks.run_events (tenant_id, run_id, at, level, code) VALUES ($1, $2, '2035-01-15T00:00:00Z', 'info', 'test')",
            tenants.A.Tenant.TenantId, tenants.A.RunId));
        Assert.Equal(PostgresErrorCodes.CheckViolation, state);
    }

    [Fact]
    public async Task SentenceLookup_ScansOnlyThePartitionOfTheShop()
    {
        // A realistic e-shop (3 000 current page versions) inside a rolled-back transaction of the table owner.
        // There is no GIN index on segment_hashes: under RLS PostgreSQL could not use it (arraycontains is not LEAKPROOF),
        // so it was dropped on 1. 10. 2026 (measured ~10 ms for 20 000 pages through the tenant index).
        var ct = TestContext.Current.CancellationToken;
        await using var conn = await database.For("Owner").OpenConnectionAsync(ct);
        await using var tx = await EshopGuard.Data.Tenancy.TenantSql.BeginAsync(conn, tenants.A.Tenant.TenantId, ct: ct);
        var shopId = Guid.CreateVersion7();
        var setup = $"""
            INSERT INTO shop.shops (id, tenant_id, domain, base_url, base_path, home_country, platform, source_mode, status)
            VALUES ('{shopId}', '{tenants.A.Tenant.TenantId}', 'big-{shopId:N}.sk', 'https://big.sk/', '/', 'sk', 'other', 'web', 'active');
            INSERT INTO content.pages (id, tenant_id, shop_id, url, url_hash, source, status, first_seen_at, rotation_bucket)
            SELECT uuidv7(), '{tenants.A.Tenant.TenantId}', '{shopId}', 'https://big.sk/' || g, g, 'crawl', 'active', now(), g % 7
            FROM generate_series(1, 3000) g;
            INSERT INTO content.page_versions (id, tenant_id, shop_id, page_id, fetched_at, segment_hashes, is_current)
            SELECT uuidv7(), tenant_id, shop_id, id, now(),
                   ARRAY(SELECT (url_hash * 1000 + k)::bigint FROM generate_series(1, 40) k) || CASE WHEN url_hash % 1000 = 0 THEN ARRAY[22::bigint] ELSE ARRAY[]::bigint[] END,
                   true
            FROM content.pages WHERE shop_id = '{shopId}';
            ANALYZE content.page_versions;
            """;
        await using (var command = new NpgsqlCommand(setup, conn, tx))
        {
            await command.ExecuteNonQueryAsync(ct);
        }

        var lines = new List<string>();
        await using (var explain = new NpgsqlCommand($"EXPLAIN SELECT page_id FROM content.page_versions WHERE shop_id = '{shopId}' AND is_current AND segment_hashes @> ARRAY[22::bigint]", conn, tx))
        await using (var reader = await explain.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                lines.Add(reader.GetString(0));
            }
        }

        await tx.RollbackAsync(ct);
        var plan = string.Join('\n', lines);
        var partitions = System.Text.RegularExpressions.Regex.Matches(plan, @"page_versions_p\d\d").Select(m => m.Value).Distinct().ToList();
        Assert.Single(partitions);
    }
}
