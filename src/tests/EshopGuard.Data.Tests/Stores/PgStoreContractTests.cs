using EshopGuard.Core.Storage;
using EshopGuard.Core.Tests;
using EshopGuard.Data.Stores;

namespace EshopGuard.Data.Tests.Stores;

/// <summary>The PostgreSQL cache has the behaviour of every cache (change 5), as <c>eshopguard_worker</c> under RLS.</summary>
[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class PgJevCacheContractTests : JevCacheContractTests, IAsyncLifetime
{
    private IStoreTenant _tenant = null!;

    public async ValueTask InitializeAsync() => _tenant = await PgStores.NewTenantAsync("Jev cache");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected override IJevCache CreateCache() => PgStores.JevCache(_tenant);
}

[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class PgRewriteCacheContractTests : RewriteCacheContractTests, IAsyncLifetime
{
    private IStoreTenant _tenant = null!;

    public async ValueTask InitializeAsync() => _tenant = await PgStores.NewTenantAsync("Rewrite cache");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected override IRewriteCache CreateCache() => new PgRewriteCache(PgStores.Worker, _tenant);
}

[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class PgPageProfileStoreContractTests : PageProfileStoreContractTests, IAsyncLifetime
{
    private IStoreTenant _tenant = null!;

    public async ValueTask InitializeAsync() => _tenant = await PgStores.NewTenantAsync("Profiles");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected override IPageProfileStore CreateStore() => new PgPageProfileStore(PgStores.Worker, _tenant);
}
