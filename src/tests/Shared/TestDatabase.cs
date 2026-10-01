using EshopGuard.Data;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Tests.Shared;

/// <summary>Brings the test database <c>eshopguard_test</c> to the current migrations, as <c>eshopguard_owner</c>.</summary>
internal static class TestDatabase
{
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
            await using var db = new EshopGuardDb(options.Options);
            await db.Database.MigrateAsync(ct);
            _migrated = true;
        }
        finally
        {
            Gate.Release();
        }
    }
}
