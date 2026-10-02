using EshopGuard.Application.Fixes;
using EshopGuard.Data.Entities.Shops;

namespace EshopGuard.Api.Tests.Fakes;

/// <summary>
/// The publisher of change 15 as the tests need it: every field of a page can be written except a template of the whole site
/// (<c>copy_only</c>). It writes nothing; the job <c>publish.fix</c> stays queued.
/// </summary>
internal sealed class FakeFixPublisher : IFixPublisher
{
    public bool CanPublish(ShopPlatform platform, string field, FixPublishPage page) => !page.IsTemplate;
}
