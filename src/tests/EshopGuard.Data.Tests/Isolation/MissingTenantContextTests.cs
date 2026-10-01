using EshopGuard.Data.Tenancy;
using EshopGuard.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EshopGuard.Data.Tests.Isolation;

[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class MissingTenantContextTests(TwoTenantsFixture tenants)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sql_WithoutContext_FailsInsteadOfReturningNothing()
    {
        await using var source = NpgsqlDataSource.Create(TestConfiguration.ConnectionString("App"));
        await using var command = source.CreateCommand("SELECT count(*) FROM shop.shops");
        var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteScalarAsync(Ct));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
        Assert.Equal("app.tenant_id is not set", ex.MessageText);
    }

    [Fact]
    public async Task Sql_OnSameConnectionAfterTenantTransaction_FailsInsteadOfReturningRows()
    {
        await using var source = NpgsqlDataSource.Create(TestConfiguration.ConnectionString("App"));
        await using var conn = await source.OpenConnectionAsync(Ct);
        await using (var tx = await TenantSql.BeginAsync(conn, tenants.A.Tenant.TenantId, ct: Ct))
        {
            await using var inside = new NpgsqlCommand("SELECT count(*) FROM checks.findings", conn, tx);
            Assert.True((long)(await inside.ExecuteScalarAsync(Ct))! > 0);
            await tx.CommitAsync(Ct);
        }

        await using var after = new NpgsqlCommand("SELECT * FROM checks.findings", conn);
        var ex = await Assert.ThrowsAsync<PostgresException>(() => after.ExecuteReaderAsync(Ct));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
    }

    [Fact]
    public async Task Sql_InvalidTenantText_FailsOnConversion()
    {
        await using var source = NpgsqlDataSource.Create(TestConfiguration.ConnectionString("App"));
        await using var conn = await source.OpenConnectionAsync(Ct);
        await using var tx = await conn.BeginTransactionAsync(Ct);
        await using (var set = new NpgsqlCommand("SELECT set_config('app.tenant_id', $1, true)", conn, tx))
        {
            set.Parameters.Add(new NpgsqlParameter { Value = "x' OR '1'='1" });
            await set.ExecuteNonQueryAsync(Ct);
        }

        await using var query = new NpgsqlCommand("SELECT * FROM shop.shops", conn, tx);
        var ex = await Assert.ThrowsAsync<PostgresException>(() => query.ExecuteReaderAsync(Ct));
        Assert.Equal(PostgresErrorCodes.InvalidTextRepresentation, ex.SqlState);
    }

    [Fact]
    public async Task Ef_WithoutContext_ThrowsAndSendsNothing()
    {
        var recorder = new CommandRecorder();
        await using var db = TestDatabase.CreateDb("App", new TenantContext(), recorder);

        var ex = await Assert.ThrowsAsync<TenantNotSetException>(() => db.Findings.CountAsync(Ct));

        Assert.Equal("tenant.not_set", ex.Code);
        Assert.Empty(recorder.Commands);
    }

    [Fact]
    public async Task Ef_TenantTransactionWithoutContext_Throws()
    {
        await using var db = TestDatabase.CreateDb("App", new TenantContext());
        await Assert.ThrowsAsync<TenantNotSetException>(() => db.ExecuteInTenantTransactionAsync(() => Task.CompletedTask, Ct));
    }
}
