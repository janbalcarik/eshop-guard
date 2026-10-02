using EshopGuard.Api.Contracts;
using EshopGuard.Api.Http;
using EshopGuard.Api.Problems;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Fixes;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Tenants;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EshopGuard.Api.Endpoints;

/// <summary>
/// Bulk fixes (change 11, design E): one decision for the same sentence on many pages. Changes go over <c>If-Match</c>; a change
/// that queues the recheck of the wording answers <c>202</c>.
/// </summary>
public static class FixGroupEndpoints
{
    public static RouteGroupBuilder MapFixGroupEndpoints(this RouteGroupBuilder shop)
    {
        shop.MapGet("/fix-groups", async (Guid shopId, string? status, FixGroupService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListAsync(shopId, status, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ValidationFailed);

        shop.MapGet("/fix-groups/{groupId:guid}", async (Guid shopId, Guid groupId, string? cursor, HttpContext context, FixGroupService service, CancellationToken ct) =>
                Tagged(context, await service.DetailAsync(shopId, groupId, cursor, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.GroupNotFound, ProblemCodes.ValidationFailed);

        shop.MapPut("/fix-groups/{groupId:guid}/values", async (Guid shopId, Guid groupId, FixGroupValuesRequest? body, HttpContext context,
                FixGroupService service, CancellationToken ct) =>
                Changed(context, await service.SetValuesAsync(context.User.RequireUserId(), shopId, groupId, body?.Values, context.IfMatch(), ct)))
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.GroupNotFound, ProblemCodes.GroupPlaceholderUnknown, ProblemCodes.GroupLocked,
                ProblemCodes.ConcurrencyConflict, ProblemCodes.ShopSampleOnly, ProblemCodes.ValidationFailed);

        shop.MapPut("/fix-groups/{groupId:guid}/mode", async (Guid shopId, Guid groupId, FixGroupModeRequest? body, HttpContext context,
                FixGroupService service, CancellationToken ct) =>
                Changed(context, await service.SetModeAsync(context.User.RequireUserId(), shopId, groupId, body?.Mode, body?.CustomText, context.IfMatch(), ct)))
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.GroupNotFound, ProblemCodes.GroupCustomTextRequired, ProblemCodes.ProposalTextTooLong,
                ProblemCodes.GroupLocked, ProblemCodes.ConcurrencyConflict, ProblemCodes.ShopSampleOnly, ProblemCodes.ValidationFailed);

        shop.MapPut("/fix-groups/{groupId:guid}/excluded-pages", async (Guid shopId, Guid groupId, FixGroupExcludedPagesRequest? body, HttpContext context,
                FixGroupService service, CancellationToken ct) =>
                Changed(context, await service.ExcludePagesAsync(context.User.RequireUserId(), shopId, groupId, body?.PageIds, context.IfMatch(), ct)))
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.GroupNotFound, ProblemCodes.GroupPageNotInGroup, ProblemCodes.GroupNoPagesLeft,
                ProblemCodes.GroupLocked, ProblemCodes.ConcurrencyConflict, ProblemCodes.ShopSampleOnly, ProblemCodes.ValidationFailed);

        shop.MapPost("/fix-groups/{groupId:guid}/approve", async (Guid shopId, Guid groupId, HttpContext context, FixGroupService service, CancellationToken ct) =>
                Tagged(context, await service.ApproveAsync(context.User.RequireUserId(), shopId, groupId, context.IfMatch(), ct)))
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.GroupNotFound, ProblemCodes.GroupValueMissing, ProblemCodes.GroupCustomTextRequired,
                ProblemCodes.GroupRecheckPending, ProblemCodes.GroupRecheckFailed, ProblemCodes.GroupLocked, ProblemCodes.ConcurrencyConflict,
                ProblemCodes.FindingTransitionNotAllowed, ProblemCodes.ShopSampleOnly, ProblemCodes.ValidationFailed);

        shop.MapPost("/fix-groups/{groupId:guid}/approve-page", async (Guid shopId, Guid groupId, FixGroupPageRequest? body, HttpContext context,
                FixGroupService service, CancellationToken ct) =>
                {
                    var proposal = await service.ApprovePageAsync(context.User.RequireUserId(), shopId, groupId, body?.PageId, ct);
                    context.SetETag(proposal.Version);
                    return TypedResults.Ok(proposal);
                })
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.GroupNotFound, ProblemCodes.GroupPageNotInGroup, ProblemCodes.GroupPageNeedsIndividualFix,
                ProblemCodes.GroupValueMissing, ProblemCodes.GroupCustomTextRequired, ProblemCodes.GroupRecheckPending, ProblemCodes.GroupRecheckFailed,
                ProblemCodes.FindingTransitionNotAllowed, ProblemCodes.ShopSampleOnly, ProblemCodes.ValidationFailed);

        shop.MapPost("/fix-groups/{groupId:guid}/unapprove", async (Guid shopId, Guid groupId, HttpContext context, FixGroupService service, CancellationToken ct) =>
                Tagged(context, await service.UnapproveAsync(context.User.RequireUserId(), shopId, groupId, context.IfMatch(), ct)))
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.GroupNotFound, ProblemCodes.GroupAlreadyPublished, ProblemCodes.ConcurrencyConflict,
                ProblemCodes.FindingTransitionNotAllowed, ProblemCodes.ShopSampleOnly, ProblemCodes.ValidationFailed);
        return shop;
    }

    private static Ok<FixGroupDto> Tagged(HttpContext context, FixGroupDto group)
    {
        context.SetETag(group.Version);
        return TypedResults.Ok(group);
    }

    /// <summary>A change: <c>202</c> while the wording waits for its recheck (a job), otherwise <c>200</c>.</summary>
    private static Results<Ok<FixGroupDto>, Accepted<FixGroupDto>> Changed(HttpContext context, FixGroupDto group)
    {
        context.SetETag(group.Version);
        return group.Recheck.Status == "pending" ? TypedResults.Accepted((string?)null, group) : TypedResults.Ok(group);
    }
}
