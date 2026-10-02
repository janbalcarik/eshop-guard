using EshopGuard.Data.Entities.Shops;

namespace EshopGuard.Application.Fixes;

/// <summary>
/// Writing approved fixes into the e-shop (the contract for change 15). This change only asks whether a field can be written
/// and queues the job <c>publish.fix</c>; the conflict check, the write, keeping the original and the states
/// <c>published</c> / <c>conflict</c> / <c>failed</c> are the job's of change 15. Without a registered implementation every
/// publication is <c>409 publication.connector_unavailable</c> and nothing is written.
/// </summary>
public interface IFixPublisher
{
    /// <summary>
    /// True when the platform can write the field of the page; false (a template, an Upgates product without a code,
    /// BiznisWeb without the partner agreement) means the text is only for „Kopírovať text“ (<c>copy_only</c>).
    /// </summary>
    bool CanPublish(ShopPlatform platform, string field, FixPublishPage page);
}

/// <summary>The page a field belongs to, as the publisher needs it.</summary>
public sealed record FixPublishPage(Guid PageId, string? ExternalId, string? PageType, bool IsTemplate);
