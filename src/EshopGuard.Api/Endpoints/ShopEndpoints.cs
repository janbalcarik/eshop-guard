using EshopGuard.Api.Contracts;
using EshopGuard.Api.Problems;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Shops;
using EshopGuard.Application.Tenants;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EshopGuard.Api.Endpoints;

/// <summary>
/// <c>/api/t/{tenantId}/shops</c>: e-shops, the recognition of the platform and the way of reading the texts (change 10).
/// Reading for every member (<c>viewer</c>), changes for <c>admin</c> and <c>owner</c>.
/// </summary>
public static class ShopEndpoints
{
    public static RouteGroupBuilder MapShopEndpoints(this RouteGroupBuilder tenant)
    {
        var shops = tenant.MapGroup("/shops").WithTags("shops");

        shops.MapGet("", async (ShopService service, CancellationToken ct) => TypedResults.Ok(await service.ListAsync(ct)))
            .RequireTenantRole(TenantRole.Viewer);

        shops.MapPost("", async (Guid tenantId, CreateShopRequest? body, HttpContext context, ShopService service, IJobCompletionAwaiter awaiter, CancellationToken ct) =>
            {
                var (shopId, jobId) = await service.CreateAsync(context.User.RequireUserId(), body!.Url, ct);
                await awaiter.WaitAsync(jobId, ct);
                return TypedResults.Created($"/api/t/{tenantId:D}/shops/{shopId:D}", await service.GetAsync(shopId, ct));
            })
            .RequireTenantRole(TenantRole.Admin)
            .Validate<CreateShopRequest>()
            .ProducesProblemCodes(ProblemCodes.ShopUrlInvalid, ProblemCodes.ShopUrlNotAllowed, ProblemCodes.ShopAlreadyExists, ProblemCodes.RateLimited);

        shops.MapGet("/{shopId:guid}", async (Guid shopId, ShopService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(shopId, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound);

        shops.MapPatch("/{shopId:guid}", async (Guid shopId, RenameShopRequest? body, HttpContext context, ShopService service, CancellationToken ct) =>
                TypedResults.Ok(await service.RenameAsync(context.User.RequireUserId(), shopId, body!.Name, body.Version, ct)))
            .RequireTenantRole(TenantRole.Admin)
            .Validate<RenameShopRequest>()
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ConcurrencyConflict);

        shops.MapDelete("/{shopId:guid}", async (Guid shopId, HttpContext context, ShopService service, CancellationToken ct) =>
            {
                await service.DeleteAsync(context.User.RequireUserId(), shopId, ct);
                return TypedResults.NoContent();
            })
            .RequireTenantRole(TenantRole.Admin)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ShopSubscriptionActive, ProblemCodes.ShopRunInProgress);

        shops.MapGet("/{shopId:guid}/detection", async (Guid shopId, PlatformService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(shopId, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound);

        shops.MapPost("/{shopId:guid}/detection", async Task<Results<Ok<DetectionDto>, Accepted<DetectionDto>>> (
                Guid tenantId, Guid shopId, PlatformService service, IJobCompletionAwaiter awaiter, CancellationToken ct) =>
            {
                var jobId = await service.RedetectAsync(shopId, ct);
                await awaiter.WaitAsync(jobId, ct);
                var detection = await service.GetAsync(shopId, ct);
                return detection.Status == "pending"
                    ? TypedResults.Accepted($"/api/t/{tenantId:D}/shops/{shopId:D}/detection", detection)
                    : TypedResults.Ok(detection);
            })
            .RequireTenantRole(TenantRole.Admin)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.DetectionInProgress, ProblemCodes.RateLimited);

        shops.MapPut("/{shopId:guid}/platform", async (Guid shopId, SetPlatformRequest? body, HttpContext context, PlatformService service, CancellationToken ct) =>
                TypedResults.Ok(await service.SetAsync(context.User.RequireUserId(), shopId, body!.Platform, ct)))
            .RequireTenantRole(TenantRole.Admin)
            .Validate<SetPlatformRequest>()
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.PlatformUnknown);

        shops.MapPut("/{shopId:guid}/source", async (Guid shopId, SetSourceRequest? body, HttpContext context, SourceModeService service, CancellationToken ct) =>
                TypedResults.Ok(await service.SetAsync(context.User.RequireUserId(), shopId, body!.Mode,
                    body.Feed is null ? null : new FeedInput(body.Feed.Url, body.Feed.Format), ct)))
            .RequireTenantRole(TenantRole.Admin)
            .Validate<SetSourceRequest>()
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ShopConnectorNotConnected, ProblemCodes.ShopRunInProgress,
                ProblemCodes.FeedUrlInvalid, ProblemCodes.FeedFormatUnknown);
        return shops;
    }
}
