using System.Data.Common;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Data.Tests.Isolation;
using EshopGuard.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace EshopGuard.Data.Tests.Tenancy;

[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class TenantTransactionTests(TwoTenantsFixture tenants)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveChanges_OfOneEntity_RunsInTransactionWithTenant_AndTheSettingEndsWithIt()
    {
        var probe = new TenantSettingProbe();
        var context = new TenantContext();
        context.Set(tenants.A.Tenant.TenantId);
        await using var db = TestDatabase.CreateDb("App", context, probe);
        await db.Database.OpenConnectionAsync(Ct);

        db.Add(new Shop { Domain = $"t{Guid.NewGuid():N}.sk", BaseUrl = "https://t.sk/", BasePath = "/", HomeCountry = "sk", Platform = ShopPlatform.Other, SourceMode = ShopSourceMode.Web, Status = ShopStatus.Draft });
        await db.SaveChangesAsync(Ct);

        Assert.Equal(tenants.A.Tenant.TenantId.ToString("D"), probe.SeenDuringInsert);
        await using var after = new NpgsqlCommand("SELECT coalesce(current_setting('app.tenant_id', true), '')", (NpgsqlConnection)db.Database.GetDbConnection());
        Assert.Equal(string.Empty, await after.ExecuteScalarAsync(Ct));
    }

    [Fact]
    public void SettingAnotherTenant_IsRefused()
    {
        var context = new TenantContext();
        context.Set(tenants.A.Tenant.TenantId);

        Assert.Throws<InvalidOperationException>(() => context.Set(tenants.B.Tenant.TenantId));
        Assert.Equal(tenants.A.Tenant.TenantId, context.TenantId);
        context.Set(tenants.A.Tenant.TenantId);
    }

    /// <summary>Reads <c>app.tenant_id</c> on the connection and transaction of the INSERT, right before it runs.</summary>
    private sealed class TenantSettingProbe : DbCommandInterceptor
    {
        public string? SeenDuringInsert { get; private set; }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO shop.shops", StringComparison.Ordinal))
            {
                await using var probe = new NpgsqlCommand("SELECT current_setting('app.tenant_id')", (NpgsqlConnection)command.Connection!, (NpgsqlTransaction?)command.Transaction);
                SeenDuringInsert = (string?)await probe.ExecuteScalarAsync(cancellationToken);
            }

            return result;
        }
    }
}
