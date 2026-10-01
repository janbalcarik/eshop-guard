using EshopGuard.Core.Storage;

namespace EshopGuard.Core.Tests;

public sealed class InMemoryJevCacheTests : JevCacheContractTests
{
    protected override IJevCache CreateCache() => new InMemoryJevCache();
}

public sealed class InMemoryPageStoreTests : PageStoreContractTests
{
    protected override IPageStore CreateStore() => new InMemoryPageStore();
}

public sealed class InMemoryPageContentStoreTests : PageContentStoreContractTests
{
    protected override IPageContentStore CreateStore() => new InMemoryPageContentStore();
}

public sealed class InMemoryUrlFrontierStoreTests : UrlFrontierStoreContractTests
{
    protected override IUrlFrontierStore CreateStore() => new InMemoryUrlFrontierStore();
}
