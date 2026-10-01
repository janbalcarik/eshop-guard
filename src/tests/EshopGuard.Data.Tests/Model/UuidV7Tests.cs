using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Tenancy;
using EshopGuard.Data.Tests.Isolation;
using EshopGuard.Tests.Shared;
using Npgsql;

namespace EshopGuard.Data.Tests.Model;

[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class UuidV7Tests(TwoTenantsFixture tenants)
{
    private static int Version(Guid id) => id.Version;

    [Fact]
    public async Task IdsFromEf_AreVersion7_AndOrderedInPostgres()
    {
        await using var db = TestDatabase.CreateDb("App", tenants.A.Tenant.TenantId);
        var first = new Run { ShopId = tenants.A.ShopId, Kind = RunKind.Recheck, Trigger = RunTrigger.System, Status = RunStatus.Queued, Priority = 1 };
        await Task.Delay(2, TestContext.Current.CancellationToken);
        var second = new Run { ShopId = tenants.A.ShopId, Kind = RunKind.Recheck, Trigger = RunTrigger.System, Status = RunStatus.Queued, Priority = 1 };
        db.AddRange(first, second);
        await db.ExecuteInTenantTransactionAsync(() => db.SaveChangesAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        Assert.Equal(7, Version(first.Id));
        Assert.Equal(7, Version(second.Id));
        var ordered = await IsolationSql.InTenantAsync("App", tenants.A.Tenant.TenantId, async (conn, tx) =>
        {
            await using var command = new NpgsqlCommand("SELECT $1::uuid < $2::uuid", conn, tx);
            command.Parameters.Add(new NpgsqlParameter { Value = first.Id });
            command.Parameters.Add(new NpgsqlParameter { Value = second.Id });
            return (bool)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
        });
        Assert.True(ordered);
    }

    [Fact]
    public async Task IdFromDatabaseDefault_IsVersion7()
    {
        var id = await IsolationSql.InTenantAsync("App", tenants.A.Tenant.TenantId, async (conn, tx) =>
        {
            await using var command = new NpgsqlCommand(
                "INSERT INTO checks.runs (tenant_id, shop_id, kind, trigger, status, priority) VALUES ($1, $2, 'recheck', 'system', 'queued', 1) RETURNING id", conn, tx);
            command.Parameters.Add(new NpgsqlParameter { Value = tenants.A.Tenant.TenantId });
            command.Parameters.Add(new NpgsqlParameter { Value = tenants.A.ShopId });
            return (Guid)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
        });
        Assert.Equal(7, Version(id));
    }
}
