using EshopGuard.Data.Tenancy;
using EshopGuard.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EshopGuard.Data.Tests;

[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class MigrationTests(PostgresTestDatabase database)
{
    [Fact]
    public async Task History_ContainsInitialAndIsOwnedByOwner()
    {
        Assert.Equal(1L, await database.For("Owner").ScalarAsync<long>(
            "SELECT count(*) FROM ops.__ef_migrations_history WHERE \"MigrationId\" LIKE '%\\_Initial'"));
        Assert.Equal("eshopguard_owner", await database.For("Owner").ScalarAsync<string>(
            "SELECT tableowner::text FROM pg_tables WHERE schemaname = 'ops' AND tablename = '__ef_migrations_history'"));
    }

    [Theory]
    [InlineData("App")]
    [InlineData("Worker")]
    [InlineData("Admin")]
    public async Task ApplicationRoles_CanReadHistory(string connection)
    {
        Assert.True(await database.For(connection).ScalarAsync<long>("SELECT count(*) FROM ops.__ef_migrations_history") >= 1);
    }

    [Fact]
    public async Task Migrate_AsAppRole_IsRefused()
    {
        var options = new DbContextOptionsBuilder<EshopGuardDb>();
        options.UseEshopGuardNpgsql(TestConfiguration.ConnectionString("App"));
        await using var db = new EshopGuardDb(options.Options, new TenantContext());
        // Pretend nothing is applied: the app role must not be able to create anything.
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("CREATE TABLE ops.__probe (id int)", TestContext.Current.CancellationToken));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
    }

    [Fact]
    public async Task Migrate_AsAppRole_IsRefusedEvenWithNothingPending()
    {
        // EF Core locks the history table before migrating; the app role may only read it.
        var options = new DbContextOptionsBuilder<EshopGuardDb>();
        options.UseEshopGuardNpgsql(TestConfiguration.ConnectionString("App"));
        await using var db = new EshopGuardDb(options.Options, new TenantContext());
        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.MigrateAsync(TestContext.Current.CancellationToken));
        var postgres = ex as PostgresException ?? ex.InnerException as PostgresException;
        Assert.NotNull(postgres);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, postgres.SqlState);
    }
}
