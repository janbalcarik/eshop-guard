namespace EshopGuard.Data.Tests.Catalog;

/// <summary>
/// The default price lists and the privileges of billing (change 12, tasks 2.2 and 2.4): SK/EUR and CZ/CZK as drafts with five
/// tiers without gaps or overlaps, the tier <c>custom</c> without a price, no volume discount; the worker writes only the
/// columns of a tenant that Stripe reports and audits the global price lists.
/// </summary>
[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class BillingSeedTests(PostgresTestDatabase database)
{
    [Theory]
    [InlineData("SK 2026", "sk", "EUR")]
    [InlineData("CZ 2026", "cz", "CZK")]
    public async Task DefaultPriceList_HasFiveTiersWithoutGapsOrOverlaps(string name, string market, string currency)
    {
        var owner = database.For("Owner");
        var id = await owner.ScalarAsync<Guid>(
            $"SELECT id FROM billing.price_lists WHERE name = '{name}' AND market_code = '{market}' AND currency = '{currency}' AND status = 'draft'");
        Assert.Equal(30, await owner.ScalarAsync<int>($"SELECT notice_days FROM billing.price_lists WHERE id = '{id}'"));
        Assert.Equal(2m, await owner.ScalarAsync<decimal>($"SELECT fair_use_other_pages_factor FROM billing.price_lists WHERE id = '{id}'"));
        Assert.Equal(0L, await owner.ScalarAsync<long>($"SELECT count(*) FROM billing.volume_discounts WHERE price_list_id = '{id}'"));

        var tiers = await owner.RowsAsync(
            $"SELECT code, min_products, max_products, analysis_price IS NULL, monitoring_monthly IS NULL FROM billing.price_tiers WHERE price_list_id = '{id}' ORDER BY min_products");
        Assert.Equal(["t500", "t2000", "t5000", "t20000", "custom"], tiers.Select(t => (string)t[0]));
        Assert.Equal(0, (int)tiers[0][1]);
        for (var i = 1; i < tiers.Count; i++)
        {
            Assert.Equal((int)tiers[i - 1][2] + 1, (int)tiers[i][1]);
        }

        Assert.IsType<DBNull>(tiers[^1][2]);
        Assert.All(tiers.Take(4), t => Assert.False((bool)t[3] || (bool)t[4]));
        Assert.True((bool)tiers[^1][3] && (bool)tiers[^1][4]);
    }

    [Fact]
    public async Task Worker_UpdatesOnlyTheColumnsOfATenantFromStripe()
    {
        var owner = database.For("Owner");
        Assert.True(await owner.ScalarAsync<bool>("SELECT has_column_privilege('eshopguard_worker', 'iam.tenants', 'tax_id_status', 'UPDATE')"));
        Assert.True(await owner.ScalarAsync<bool>("SELECT has_column_privilege('eshopguard_worker', 'iam.tenants', 'stripe_customer_id', 'UPDATE')"));
        Assert.False(await owner.ScalarAsync<bool>("SELECT has_column_privilege('eshopguard_worker', 'iam.tenants', 'ic_dph', 'UPDATE')"));
        Assert.False(await owner.ScalarAsync<bool>("SELECT has_column_privilege('eshopguard_worker', 'iam.tenants', 'legal_name', 'UPDATE')"));
    }

    [Fact]
    public async Task AuditOfPriceLists_OnlyByTheWorker_WithoutATenant()
    {
        var policy = await database.For("Owner").RowsAsync(
            "SELECT roles::text, cmd, with_check FROM pg_policies WHERE schemaname = 'ops' AND tablename = 'audit_log' AND policyname = 'audit_log_insert_price_list'");
        var row = Assert.Single(policy);
        Assert.Equal("{eshopguard_worker}", (string)row[0]);
        Assert.Equal("INSERT", (string)row[1]);
        Assert.Contains("price_list.%", (string)row[2], StringComparison.Ordinal);
    }
}
