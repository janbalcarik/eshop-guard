using EshopGuard.Core.Jev;
using EshopGuard.Core.Profiles;
using EshopGuard.Core.Storage;
using EshopGuard.Data.Stores;
using Npgsql;

namespace EshopGuard.Data.Tests.Stores;

/// <summary>
/// A tenant never sees another tenant's answers, rewrites or profiles (task 3.6): the stores add the tenant to every query
/// and RLS stops a query that would not.
/// </summary>
[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class PgStoreIsolationTests
{
    private static readonly Dictionary<string, JevQuestion> Questions = new() { ["eco_claim"] = new() { Type = "noul", Instructions = "Q?" } };

    [Theory]
    [InlineData(JevCacheKind.Detail, "checks.jev_answers")]
    [InlineData(JevCacheKind.Sieve, "checks.sieve_answers")]
    public async Task JevAnswerOfTenantA_IsNotSeenByTenantB(JevCacheKind kind, string table)
    {
        var ct = TestContext.Current.CancellationToken;
        var a = await PgStores.NewTenantAsync("Isolation A");
        var b = await PgStores.NewTenantAsync("Isolation B");
        var key = JevCacheKeys.Create(kind, "jev-1.13.0", "eco-1", "en", Questions, "Spoločná veta " + Guid.NewGuid());
        await PgStores.JevCache(a).SetAsync(key, new JevResult { Model = "jev-1.13.0", Answers = new Dictionary<string, JevAnswer> { ["eco_claim"] = new() { Type = "noul", Noul = 0.7 } } }, ct);

        Assert.NotNull(await PgStores.JevCache(a).GetAsync(key, ct));
        Assert.Null(await PgStores.JevCache(b).GetAsync(key, ct));
        Assert.Empty(await PgStores.JevCache(b).GetManyAsync([key], ct));

        // Even a query without the tenant condition sees only the rows of the context tenant.
        await using var connection = await PgStores.Worker.Source.OpenConnectionAsync(ct);
        await using var transaction = await EshopGuard.Data.Tenancy.TenantSql.BeginAsync(connection, b.TenantId, ct: ct);
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {table} WHERE cache_key = $1", connection, transaction)
        {
            Parameters = { new NpgsqlParameter { Value = key.LegacyKey } },
        };
        Assert.Equal(0L, await command.ExecuteScalarAsync(ct));
    }

    [Fact]
    public async Task RewriteAndProfileOfTenantA_AreNotSeenByTenantB()
    {
        var ct = TestContext.Current.CancellationToken;
        var a = await PgStores.NewTenantAsync("Isolation A");
        var b = await PgStores.NewTenantAsync("Isolation B");
        var key = "rw:" + Guid.NewGuid().ToString("N");
        var site = $"shop-{Guid.NewGuid():N}.sk";
        await new PgRewriteCache(PgStores.Worker, a).SetAsync(key, "{\"changes\":[]}", "gpt-6.1-sol", ct);
        await new PgPageProfileStore(PgStores.Worker, a).AddAsync(new PageProfile { Id = site + "#1", Site = site, PromptVersion = "p1", Regions = [] }, ct);

        Assert.NotNull(await new PgRewriteCache(PgStores.Worker, a).GetAsync(key, ct));
        Assert.Null(await new PgRewriteCache(PgStores.Worker, b).GetAsync(key, ct));
        Assert.Single(await new PgPageProfileStore(PgStores.Worker, a).GetAsync(site, ct));
        Assert.Empty(await new PgPageProfileStore(PgStores.Worker, b).GetAsync(site, ct));
    }

    [Fact]
    public async Task CliTenant_IsCreatedOnlyByItsFunction_Idempotently()
    {
        var ct = TestContext.Current.CancellationToken;
        await TestDatabaseReady();

        var first = await CliTenant.EnsureAsync(PgStores.Worker, ct);
        var second = await CliTenant.EnsureAsync(PgStores.Worker, ct);

        Assert.Equal(CliTenant.Id, first);
        Assert.Equal(first, second);
        Assert.True(await CliTenant.ExistsAsync(PgStores.Worker, ct));

        // The worker role may not create other tenants itself.
        await using var command = PgStores.Worker.Source.CreateCommand(
            "INSERT INTO iam.tenants (name, country_code, locale, market_code, status) VALUES ('x', 'SK', 'sk', 'sk', 'active')");
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(ct));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    private static Task TestDatabaseReady() => EshopGuard.Tests.Shared.TestDatabase.EnsureMigratedAsync(TestContext.Current.CancellationToken);
}
