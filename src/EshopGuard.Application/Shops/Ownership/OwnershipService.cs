using System.Security.Cryptography;
using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Problems;
using EshopGuard.Application.RateLimits;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Shops.Ownership;

/// <summary>
/// The verification of the ownership of an e-shop (change 10, AD 9): a verification <c>meta</c> or <c>dns</c> with a random
/// token (22 characters base64url, public by nature: it stands in the page or in DNS), its check as the job
/// <c>shop.verify_ownership</c> of the worker (P0, bucket <c>shops:verify:shop:*</c>). <c>connector</c> is written by change 15.
/// </summary>
public sealed class OwnershipService(
    EshopGuardDb db, ShopReader reader, IJobQueue queue, AuthRateLimits limits, SecurityAuditWriter audit, IOptions<OwnershipPolicyOptions> policy)
{
    public async Task<OwnershipDto> GetAsync(Guid shopId, CancellationToken ct) => await db.ExecuteInTenantTransactionAsync(async () =>
    {
        var shop = await reader.ReadAsync(shopId, ct).ConfigureAwait(false);
        var verifications = await db.ShopVerifications.AsNoTracking().Where(v => v.ShopId == shopId).OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id)
            .ToListAsync(ct).ConfigureAwait(false);
        return new OwnershipDto(
            shop.OwnershipVerifiedAt is not null, shop.OwnershipVerifiedAt, shop.VerificationMethod is { } method ? ShopReader.Text(method) : null,
            (policy.Value.RequiredBefore ?? []).Where(g => g != OwnershipPolicyOptions.None).ToList(),
            verifications.Select(v => ToDto(v, shop.Domain)).ToList());
    }, ct).ConfigureAwait(false);

    public async Task<VerificationDto> GetVerificationAsync(Guid shopId, Guid verificationId, CancellationToken ct) => await db.ExecuteInTenantTransactionAsync(async () =>
    {
        var shop = await reader.ReadAsync(shopId, ct).ConfigureAwait(false);
        var verification = await db.ShopVerifications.AsNoTracking().FirstOrDefaultAsync(v => v.Id == verificationId && v.ShopId == shopId, ct).ConfigureAwait(false)
            ?? throw new DomainException(ProblemCodes.OwnershipVerificationNotFound, 404);
        return ToDto(verification, shop.Domain);
    }, ct).ConfigureAwait(false);

    /// <summary><c>POST …/ownership/verifications</c>: a new verification (<c>400 ownership.method_unknown</c>, <c>409 ownership.already_verified</c>).</summary>
    public async Task<VerificationDto> CreateAsync(Guid userId, Guid shopId, string? method, CancellationToken ct)
    {
        var verificationMethod = method switch
        {
            "meta" => VerificationMethod.Meta,
            "dns" => VerificationMethod.Dns,
            _ => throw new DomainException(ProblemCodes.OwnershipMethodUnknown, 400, new Dictionary<string, object?> { ["allowed"] = new[] { "meta", "dns" } }),
        };
        await limits.TakeAsync(ShopLimits.Verify, shopId.ToString("D"), ct).ConfigureAwait(false);
        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var shop = await reader.RequireAsync(shopId, ct, forUpdate: true).ConfigureAwait(false);
            if (shop.OwnershipVerifiedAt is not null)
            {
                throw new DomainException(ProblemCodes.OwnershipAlreadyVerified, 409);
            }

            var verification = new ShopVerification
            {
                ShopId = shopId,
                Method = verificationMethod,
                Token = Base64Url(RandomNumberGenerator.GetBytes(16)),
                Status = ShopVerificationStatus.Pending,
            };
            db.ShopVerifications.Add(verification);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await audit.WriteAsync(new AuditEvent(AuditActions.OwnershipVerificationCreated, shop.TenantId, userId, "shop", shopId.ToString("D"),
                new JsonObject { ["method"] = method, ["verificationId"] = verification.Id.ToString("D") }), ct).ConfigureAwait(false);
            return ToDto(verification, shop.Domain);
        }, ct).ConfigureAwait(false);
    }

    /// <summary><c>POST …/verifications/{id}/check</c>: enqueues the check; returns the id of the job to wait for.</summary>
    public async Task<long> CheckAsync(Guid shopId, Guid verificationId, CancellationToken ct)
    {
        await GetVerificationAsync(shopId, verificationId, ct).ConfigureAwait(false);
        await limits.TakeAsync(ShopLimits.Verify, shopId.ToString("D"), ct).ConfigureAwait(false);
        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var shop = await reader.RequireAsync(shopId, ct, forUpdate: true).ConfigureAwait(false);
            var verification = await db.ShopVerifications.FirstAsync(v => v.Id == verificationId, ct).ConfigureAwait(false);
            verification.Status = ShopVerificationStatus.Pending;
            verification.FailureCode = null;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            var enqueued = await queue.EnqueueAsync(ShopJobs.VerifyOwnership(shop.TenantId, shopId, verificationId), DbSql.Transaction(db), ct).ConfigureAwait(false);
            return enqueued.JobId;
        }, ct).ConfigureAwait(false);
    }

    public static VerificationDto ToDto(ShopVerification verification, string domain)
    {
        ArgumentNullException.ThrowIfNull(verification);
        var meta = verification.Method == VerificationMethod.Meta;
        return new VerificationDto(
            verification.Id,
            ShopReader.Text(verification.Method),
            ShopReader.Text(verification.Status),
            verification.Token,
            new VerificationInstructionsDto(
                meta ? OwnershipTokens.MetaTag(verification.Token) : null,
                meta ? null : OwnershipTokens.DnsName(domain),
                meta ? null : OwnershipTokens.DnsValue(verification.Token)),
            verification.CheckedAt,
            verification.FailureCode);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
