using System.Text.Json;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Queue;

namespace EshopGuard.Jobs.Fixes;

/// <summary>
/// Jobs of publishing (change 11, AD 8; their handlers are change 15's): <c>publish.fix</c> writes a publication into the
/// e-shop, <c>publish.rollback</c> puts the original back. P1, class <c>io</c>, one at a time per connector of the e-shop
/// (<c>concurrency_key = connector:{shopId}</c>). Payloads carry only the id of the publication.
/// </summary>
public static class PublishJobs
{
    public const string PublishKind = "publish.fix";
    public const string RollbackKind = "publish.rollback";

    public const int MaxAttempts = 3;

    public static string ConcurrencyKey(Guid shopId) => $"connector:{shopId:D}";

    public static JobRequest Publish(Guid tenantId, Guid shopId, Guid publicationId) => Request(PublishKind, "publish", tenantId, shopId, publicationId);

    public static JobRequest Rollback(Guid tenantId, Guid shopId, Guid publicationId) => Request(RollbackKind, "rollback", tenantId, shopId, publicationId);

    private static JobRequest Request(string kind, string prefix, Guid tenantId, Guid shopId, Guid publicationId) => new(
        kind, JobResourceClass.Io, JobPriority.P1, JsonDocument.Parse($$"""{"publication_id":"{{publicationId:D}}"}"""),
        TenantId: tenantId, ShopId: shopId, DedupeKey: $"{prefix}:{publicationId:N}", ConcurrencyKey: ConcurrencyKey(shopId), MaxAttempts: MaxAttempts);
}
