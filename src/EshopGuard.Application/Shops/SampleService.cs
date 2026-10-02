using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Problems;
using EshopGuard.Application.RateLimits;
using EshopGuard.Application.Shops.Ownership;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Runs;

namespace EshopGuard.Application.Shops;

/// <summary>
/// The free sample of an e-shop (change 10, AD 4): only from <c>draft</c>, the policy of ownership, the bucket of the tenant
/// (<c>shops:sample:tenant:*</c>, before the claim, so a refused sample claims nothing), then the run of change 8, which claims
/// the domain, creates the run and its first job and moves the e-shop to <c>sample</c> in one transaction. A domain used by
/// anyone before is <c>409 sample.already_used_for_domain</c> without a word about who used it.
/// </summary>
public sealed class SampleService(
    EshopGuardDb db,
    ShopReader reader,
    IRunService runs,
    ShopOwnershipPolicy ownership,
    AuthRateLimits limits,
    SecurityAuditWriter audit,
    SampleResultReader results)
{
    public async Task<SampleDto> StartAsync(Guid userId, Guid shopId, CancellationToken ct)
    {
        var tenantId = db.TenantContext.RequireTenantId();
        var shop = await db.ExecuteInTenantTransactionAsync(() => reader.ReadAsync(shopId, ct), ct).ConfigureAwait(false);
        if (shop.Status != ShopStatus.Draft)
        {
            throw new DomainException(ProblemCodes.SampleNotAllowedInStatus, 409, new Dictionary<string, object?> { ["status"] = ShopReader.Text(shop.Status) });
        }

        await ownership.EnsureAsync(tenantId, shopId, OwnershipPolicyOptions.Sample, ct).ConfigureAwait(false);
        await limits.TakeAsync(ShopLimits.Sample, tenantId.ToString("D"), ct).ConfigureAwait(false);
        var result = await runs.CreateFreeSampleAsync(shopId, userId, ct).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw result.ErrorCode switch
            {
                RunCodes.SampleAlreadyUsed => new DomainException(ProblemCodes.SampleAlreadyUsedForDomain, 409),
                RunCodes.ShopNotFound => new DomainException(ProblemCodes.ShopNotFound, 404),
                _ => new DomainException(result.ErrorCode!, 409),
            };
        }

        await db.ExecuteInTenantTransactionAsync(() => audit.WriteAsync(new AuditEvent(AuditActions.SampleStarted, tenantId, userId, "shop", shopId.ToString("D"),
            new JsonObject { ["runId"] = result.RunId!.Value.ToString("D") }), ct), ct).ConfigureAwait(false);
        return await results.GetAsync(shopId, ct).ConfigureAwait(false);
    }
}
