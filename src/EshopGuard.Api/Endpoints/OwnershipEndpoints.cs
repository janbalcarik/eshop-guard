using EshopGuard.Api.Contracts;
using EshopGuard.Api.Problems;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Shops;
using EshopGuard.Application.Shops.Ownership;
using EshopGuard.Application.Shops.Settings;
using EshopGuard.Application.Tenants;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EshopGuard.Api.Endpoints;

/// <summary>The verification of ownership and the settings of an e-shop (change 10).</summary>
public static class OwnershipEndpoints
{
    public static RouteGroupBuilder MapOwnershipAndSettingsEndpoints(this RouteGroupBuilder shops)
    {
        shops.MapGet("/{shopId:guid}/ownership", async (Guid shopId, OwnershipService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(shopId, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound);

        shops.MapPost("/{shopId:guid}/ownership/verifications", async (Guid tenantId, Guid shopId, CreateVerificationRequest? body, HttpContext context,
                OwnershipService service, CancellationToken ct) =>
            {
                var verification = await service.CreateAsync(context.User.RequireUserId(), shopId, body!.Method, ct);
                return TypedResults.Created($"/api/t/{tenantId:D}/shops/{shopId:D}/ownership/verifications/{verification.Id:D}", verification);
            })
            .RequireTenantRole(TenantRole.Admin)
            .Validate<CreateVerificationRequest>()
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.OwnershipMethodUnknown, ProblemCodes.OwnershipAlreadyVerified, ProblemCodes.RateLimited);

        shops.MapPost("/{shopId:guid}/ownership/verifications/{verificationId:guid}/check", async Task<Results<Ok<VerificationDto>, Accepted<VerificationDto>>> (
                Guid tenantId, Guid shopId, Guid verificationId, OwnershipService service, IJobCompletionAwaiter awaiter, CancellationToken ct) =>
            {
                var jobId = await service.CheckAsync(shopId, verificationId, ct);
                await awaiter.WaitAsync(jobId, ct);
                var verification = await service.GetVerificationAsync(shopId, verificationId, ct);
                return verification.Status == "pending"
                    ? TypedResults.Accepted($"/api/t/{tenantId:D}/shops/{shopId:D}/ownership", verification)
                    : TypedResults.Ok(verification);
            })
            .RequireTenantRole(TenantRole.Admin)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.OwnershipVerificationNotFound, ProblemCodes.RateLimited);

        shops.MapGet("/{shopId:guid}/settings", async (Guid shopId, ShopSettingsService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(shopId, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound);

        shops.MapPatch("/{shopId:guid}/settings", async (Guid shopId, UpdateShopSettingsRequest? body, HttpContext context, ShopSettingsService service, CancellationToken ct) =>
            {
                var name = body!.Name;
                if (name is { ValueKind: not (System.Text.Json.JsonValueKind.String or System.Text.Json.JsonValueKind.Null) })
                {
                    throw DomainException.Validation(new ValidationResult().Add("name", ProblemCodes.Fields.Required));
                }

                return TypedResults.Ok(await service.UpdateAsync(context.User.RequireUserId(), shopId,
                    name?.ValueKind == System.Text.Json.JsonValueKind.String ? name.Value.GetString() : null, name is not null,
                    body.Modules, body.CheckHiddenOnSave, body.Version, ct));
            })
            .RequireTenantRole(TenantRole.Admin)
            .Validate<UpdateShopSettingsRequest>()
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.SettingsNoModule, ProblemCodes.SettingsModuleUnavailable,
                ProblemCodes.SettingsHiddenCheckRequiresConnector, ProblemCodes.SettingsHiddenCheckUnsupportedPlatform, ProblemCodes.ConcurrencyConflict);
        return shops;
    }
}
