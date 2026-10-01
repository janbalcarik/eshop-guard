using EshopGuard.Data.Connections;
using EshopGuard.Data.Stores;
using EshopGuard.Data.Tests.Isolation;
using EshopGuard.Tests.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace EshopGuard.Data.Tests.Stores;

/// <summary>Pool of <c>eshopguard_worker</c> on the test database and a fresh tenant per test class.</summary>
internal static class PgStores
{
    private static readonly Lazy<EshopGuardDataSource> Source = new(() => new EshopGuardDataSource(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Worker"] = TestConfiguration.ConnectionString("Worker"),
        }).Build(),
        DatabaseRole.Worker));

    public static EshopGuardDataSource Worker => Source.Value;

    public static async Task<IStoreTenant> NewTenantAsync(string name)
    {
        await TestDatabase.EnsureMigratedAsync(TestContext.Current.CancellationToken);
        var tenant = await TestTenants.CreateAsync(name, TestContext.Current.CancellationToken);
        return new FixedStoreTenant(tenant.TenantId);
    }

    public static PgJevCache JevCache(IStoreTenant tenant) => new(Worker, tenant, NullLogger<PgJevCache>.Instance);
}
