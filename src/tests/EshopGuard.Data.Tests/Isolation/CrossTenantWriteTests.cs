using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EshopGuard.Data.Tests.Isolation;

[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class CrossTenantWriteTests(TwoTenantsFixture tenants)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ef_AddingRowOfAnotherTenant_ThrowsBeforeAnyInsert()
    {
        var recorder = new CommandRecorder();
        var context = new TenantContext();
        context.Set(tenants.A.Tenant.TenantId);
        await using var db = TestDatabase.CreateDb("App", context, recorder);
        db.Add(new Shop { TenantId = tenants.B.Tenant.TenantId, Domain = "evil.sk", BaseUrl = "https://evil.sk/", BasePath = "/", HomeCountry = "sk", Platform = ShopPlatform.Other, SourceMode = ShopSourceMode.Web, Status = ShopStatus.Draft });

        var ex = await Assert.ThrowsAsync<CrossTenantWriteException>(() => db.ExecuteInTenantTransactionAsync(() => db.SaveChangesAsync(Ct), Ct));

        Assert.Equal("tenant.cross_write", ex.Code);
        Assert.Equal("tenant.cross_write: Shop", ex.Message);
        Assert.False(recorder.Sent("INSERT"));
    }

    [Fact]
    public async Task Ef_ChangingTenantOfRowWithoutTenantKey_ThrowsBeforeAnyUpdate()
    {
        var recorder = new CommandRecorder();
        var context = new TenantContext();
        context.Set(tenants.A.Tenant.TenantId);
        await using var db = TestDatabase.CreateDb("App", context, recorder);

        await Assert.ThrowsAsync<CrossTenantWriteException>(() => db.ExecuteInTenantTransactionAsync(async () =>
        {
            var notification = await db.Notifications.FirstAsync(Ct);
            notification.TenantId = tenants.B.Tenant.TenantId;
            await db.SaveChangesAsync(Ct);
        }, Ct));
        Assert.False(recorder.Sent("UPDATE"));
    }

    [Fact]
    public async Task Ef_ChangingTenantThatIsPartOfAKey_IsRefusedByEf()
    {
        await using var db = TestDatabase.CreateDb("App", tenants.A.Tenant.TenantId);

        // (tenant_id, id) is the alternate key behind the composite foreign keys; EF refuses to change it at all.
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.ExecuteInTenantTransactionAsync(async () =>
        {
            var run = await db.Runs.SingleAsync(r => r.Id == tenants.A.RunId, Ct);
            run.TenantId = tenants.B.Tenant.TenantId;
            await db.SaveChangesAsync(Ct);
        }, Ct));
    }

    [Fact]
    public async Task Ef_RowWithoutTenant_GetsTheContextTenant()
    {
        await using var db = TestDatabase.CreateDb("App", tenants.A.Tenant.TenantId);
        var run = new Run { ShopId = tenants.A.ShopId, Kind = RunKind.Recheck, Trigger = RunTrigger.System, Status = RunStatus.Queued, Priority = 1 };
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            db.Add(run);
            await db.SaveChangesAsync(Ct);
        }, Ct);

        Assert.Equal(tenants.A.Tenant.TenantId, run.TenantId);
    }

    [Fact]
    public async Task Sql_InsertWithAnotherTenant_IsRefusedByRls()
    {
        var state = await IsolationSql.InTenantAsync("App", tenants.A.Tenant.TenantId, (conn, tx) => IsolationSql.SqlStateAsync(conn, tx,
            "INSERT INTO shop.shops (id, tenant_id, domain, base_url, base_path, home_country, platform, source_mode, status) VALUES (uuidv7(), $1, 'evil.sk', 'https://evil.sk/', '/', 'sk', 'other', 'web', 'draft')",
            tenants.B.Tenant.TenantId));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, state);
    }

    public static TheoryData<string> Tables() => [.. TableNames.TenantTables];

    [Theory]
    [MemberData(nameof(Tables))]
    public async Task Sql_MovingRowsToAnotherTenant_IsRefusedByRls(string table)
    {
        var state = await IsolationSql.InTenantAsync("App", tenants.A.Tenant.TenantId, (conn, tx) =>
            IsolationSql.SqlStateAsync(conn, tx, $"UPDATE {table} SET tenant_id = $1", tenants.B.Tenant.TenantId));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, state);
    }
}
