using EshopGuard.Data;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Tests.Shared;

/// <summary>Brings the test database <c>eshopguard_test</c> to the current migrations, as <c>eshopguard_owner</c>.</summary>
internal static class TestDatabase
{
    /// <summary>
    /// Context for <c>ConnectionStrings:{connection}</c> of <c>eshopguard_test</c> with the same conventions and interceptors
    /// as the application, plus optional extra interceptors (e.g. capturing commands).
    /// </summary>
    public static EshopGuardDb CreateDb(string connection, ITenantContext tenantContext, params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] extra)
    {
        var options = new DbContextOptionsBuilder<EshopGuardDb>();
        options.UseEshopGuardNpgsql(TestConfiguration.ConnectionString(connection)).UseEshopGuardInterceptors(TimeProvider.System);
        if (extra.Length > 0)
        {
            options.AddInterceptors(extra);
        }

        return new EshopGuardDb(options.Options, tenantContext);
    }

    /// <summary>Context with a tenant set.</summary>
    public static EshopGuardDb CreateDb(string connection, Guid tenantId)
    {
        var context = new TenantContext();
        context.Set(tenantId);
        return CreateDb(connection, context);
    }

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _migrated;

    /// <summary>Applies pending migrations once per test process (EF Core locks the history table across processes).</summary>
    public static async Task EnsureMigratedAsync(CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            if (_migrated)
            {
                return;
            }

            var options = new DbContextOptionsBuilder<EshopGuardDb>();
            options.UseEshopGuardNpgsql(TestConfiguration.ConnectionString("Owner"));
            await using var db = new EshopGuardDb(options.Options, new TenantContext());
            await db.Database.MigrateAsync(ct);
            _migrated = true;
        }
        finally
        {
            Gate.Release();
        }
    }
}
