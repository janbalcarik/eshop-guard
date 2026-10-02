using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Problems;
using EshopGuard.Application.RateLimits;
using EshopGuard.Data;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Shops;

namespace EshopGuard.Application.Shops;

/// <summary>
/// The recognition of the platform (change 10, AD 2): its state, a new recognition (one at a time,
/// <c>409 detection.in_progress</c>, bucket <c>shops:detect:shop:*</c>) and the platform chosen by the client on 3b
/// (<c>platformSource = user</c>; a later recognition keeps it).
/// </summary>
public sealed class PlatformService(EshopGuardDb db, ShopReader reader, ShopService shops, AuthRateLimits limits, SecurityAuditWriter audit)
{
    /// <summary>The platforms a client may choose (<c>unknown</c> is not a choice).</summary>
    public static readonly IReadOnlyList<string> Choosable =
        SnakeCaseEnumConverter<ShopPlatform>.AllTexts.Where(p => p != "unknown").ToList();

    public async Task<DetectionDto> GetAsync(Guid shopId, CancellationToken ct) => await db.ExecuteInTenantTransactionAsync(async () =>
        await reader.DetectionAsync(await reader.ReadAsync(shopId, ct).ConfigureAwait(false), ct).ConfigureAwait(false), ct).ConfigureAwait(false);

    /// <summary><c>POST …/detection</c>: enqueues a new recognition; returns the id of the job to wait for.</summary>
    public async Task<long> RedetectAsync(Guid shopId, CancellationToken ct)
    {
        await db.ExecuteInTenantTransactionAsync(() => reader.ReadAsync(shopId, ct), ct).ConfigureAwait(false);
        await limits.TakeAsync(ShopLimits.Detect, shopId.ToString("D"), ct).ConfigureAwait(false);
        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var shop = await reader.RequireAsync(shopId, ct, forUpdate: true).ConfigureAwait(false);
            if ((await reader.DetectionAsync(shop, ct).ConfigureAwait(false)).Status == ShopDetectionState.Pending)
            {
                throw new DomainException(ProblemCodes.DetectionInProgress, 409);
            }

            return await shops.EnqueueDetectionAsync(shop, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    /// <summary><c>PUT …/platform</c>: the platform chosen by the client (<c>400 platform.unknown</c> for another value).</summary>
    public async Task<ShopDto> SetAsync(Guid userId, Guid shopId, string? platform, CancellationToken ct)
    {
        if (platform is null || !Choosable.Contains(platform, StringComparer.Ordinal))
        {
            throw new DomainException(ProblemCodes.PlatformUnknown, 400, new Dictionary<string, object?> { ["allowed"] = Choosable });
        }

        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var shop = await reader.RequireAsync(shopId, ct, forUpdate: true).ConfigureAwait(false);
            var state = ShopDetectionState.Read(shop.Detection) ?? new ShopDetectionState { Status = ShopDetectionState.Done };
            shop.Platform = SnakeCaseEnumConverter<ShopPlatform>.FromText(platform);
            shop.Detection = JsonDocument.Parse((state with { PlatformSource = ShopDetectionState.SourceUser }).ToJson());
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await audit.WriteAsync(new AuditEvent(AuditActions.ShopPlatformSet, shop.TenantId, userId, "shop", shop.Id.ToString("D"),
                new JsonObject { ["platform"] = platform }), ct).ConfigureAwait(false);
            return await reader.DtoAsync(shop, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }
}
