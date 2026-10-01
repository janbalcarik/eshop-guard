using EshopGuard.Tests.Shared;

namespace EshopGuard.Data.Tests.Isolation;

/// <summary>
/// Tenants A and B, both with the e-shop <c>vegis.sk</c> and one row in each of the 41 tenant tables. New tenants on every
/// run: nothing has to be deleted, older rows are hidden by RLS, and other test projects using the same database are not disturbed.
/// </summary>
public sealed class TwoTenantsFixture : IAsyncLifetime
{
    internal SeededTenant A { get; private set; } = null!;

    internal SeededTenant B { get; private set; } = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await TestDatabase.EnsureMigratedAsync(ct);
        var (ruleSetId, priceListId) = await TestTenants.CreateCataloguesAsync(ct);
        A = await TenantDataSeeder.SeedAsync(await TestTenants.CreateAsync("Tenant A", ct), ruleSetId, priceListId, ct);
        B = await TenantDataSeeder.SeedAsync(await TestTenants.CreateAsync("Tenant B", ct), ruleSetId, priceListId, ct);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
