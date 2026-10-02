using EshopGuard.Api.Contracts;
using EshopGuard.Api.Http;
using EshopGuard.Api.Problems;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Fixes;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Tenants;

namespace EshopGuard.Api.Endpoints;

/// <summary>
/// Publication of the accepted fixes through the connector and „Kopírovať text“ (change 11, AD 8). This change queues only;
/// the write is the job of change 15.
/// </summary>
public static class PublicationEndpoints
{
    public static RouteGroupBuilder MapPublicationEndpoints(this RouteGroupBuilder shop)
    {
        shop.MapGet("/pages/{pageId:guid}/fixed-text", async (Guid shopId, Guid pageId, string? field, PublicationService service, CancellationToken ct) =>
                TypedResults.Ok(await service.FixedTextAsync(shopId, pageId, field, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.PageNotFound, ProblemCodes.PageNoAcceptedChanges, ProblemCodes.ValidationFailed);

        shop.MapPost("/publications", async (Guid shopId, PublicationRequest? body, HttpContext context, PublicationService service, CancellationToken ct) =>
                TypedResults.Accepted((string?)null,
                    await service.RequestAsync(context.User.RequireUserId(), shopId, body?.PageIds, body?.ProposalIds, body?.GroupId, ct)))
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.PageNotFound, ProblemCodes.ProposalNotFound, ProblemCodes.GroupNotFound,
                ProblemCodes.PublicationNotAvailable, ProblemCodes.PublicationConnectorUnavailable, ProblemCodes.PublicationNothingToPublish,
                ProblemCodes.ShopSampleOnly, ProblemCodes.ValidationFailed);

        shop.MapGet("/publications", async (Guid shopId, string? status, string? cursor, int? limit, PublicationService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListAsync(shopId, status, cursor, limit, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ValidationFailed);

        shop.MapGet("/publications/{publicationId:guid}", async (Guid shopId, Guid publicationId, PublicationService service, CancellationToken ct) =>
                TypedResults.Ok(await service.DetailAsync(shopId, publicationId, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.PublicationNotFound);

        shop.MapPost("/publications/{publicationId:guid}/rollback", async (Guid shopId, Guid publicationId, HttpContext context, PublicationService service,
                CancellationToken ct) =>
                TypedResults.Accepted((string?)null, await service.RollbackAsync(context.User.RequireUserId(), shopId, publicationId, ct)))
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.PublicationNotFound, ProblemCodes.PublicationNotRollbackable,
                ProblemCodes.PublicationNotAvailable, ProblemCodes.PublicationConnectorUnavailable, ProblemCodes.ShopSampleOnly);
        return shop;
    }
}
