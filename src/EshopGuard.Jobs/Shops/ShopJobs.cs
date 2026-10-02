using System.Text.Json;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Queue;

namespace EshopGuard.Jobs.Shops;

/// <summary>
/// The interactive jobs of an e-shop (change 10): recognition of the platform and the check of ownership. Both are P0 (the
/// user waits), class <c>fetch</c>, payload only ids. No dedupe key: a repeated recognition is a new job, a second one while
/// one runs is refused by the API under the lock of the e-shop row (<c>detection.in_progress</c>).
/// </summary>
public static class ShopJobs
{
    public const string DetectPlatformKind = "shop.detect_platform";
    public const string VerifyOwnershipKind = "shop.verify_ownership";

    /// <summary>Attempts of a job (a crashed worker; failures of the site are results, not retries).</summary>
    public const int MaxAttempts = 2;

    public static JobRequest DetectPlatform(Guid tenantId, Guid shopId) => new(
        DetectPlatformKind, JobResourceClass.Fetch, JobPriority.P0,
        JsonDocument.Parse($$"""{"shop_id":"{{shopId:D}}"}"""), TenantId: tenantId, ShopId: shopId, MaxAttempts: MaxAttempts);

    public static JobRequest VerifyOwnership(Guid tenantId, Guid shopId, Guid verificationId) => new(
        VerifyOwnershipKind, JobResourceClass.Fetch, JobPriority.P0,
        JsonDocument.Parse($$"""{"shop_id":"{{shopId:D}}","verification_id":"{{verificationId:D}}"}"""),
        TenantId: tenantId, ShopId: shopId, MaxAttempts: MaxAttempts);
}

/// <summary>Options of the jobs of an e-shop: <c>Shops:Detection</c>.</summary>
public sealed class ShopJobsOptions
{
    public const string SectionName = "Shops:Detection";

    /// <summary>Largest home page read for the recognition and the check of ownership (bytes, proposal 1 MB).</summary>
    public long MaxBytes { get; set; } = 1024 * 1024;

    /// <summary>Time for robots.txt and the home page together (seconds, proposal 10).</summary>
    public int TimeoutSeconds { get; set; } = 10;
}
