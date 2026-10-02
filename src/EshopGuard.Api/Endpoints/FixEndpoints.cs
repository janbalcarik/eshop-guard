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

/// <summary>„Oprava stránky“ and the proposals of fixes (change 11, design C). Changes go over <c>If-Match</c>.</summary>
public static class FixEndpoints
{
    public static RouteGroupBuilder MapFixEndpoints(this RouteGroupBuilder shop)
    {
        shop.MapGet("/pages/{pageId:guid}/review", async (Guid shopId, Guid pageId, string? tab, string? language, PageReviewService service, CancellationToken ct) =>
                TypedResults.Ok(await service.GetAsync(shopId, pageId, tab, language, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.PageNotFound, ProblemCodes.ValidationFailed);

        shop.MapGet("/proposals/{proposalId:guid}", async (Guid shopId, Guid proposalId, HttpContext context, FixProposalService service, CancellationToken ct) =>
                Tagged(context, await service.GetAsync(shopId, proposalId, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ProposalNotFound);

        shop.MapPut("/proposals/{proposalId:guid}/alternative", async (Guid shopId, Guid proposalId, SelectAlternativeRequest? body, HttpContext context,
                FixProposalService service, CancellationToken ct) =>
                Tagged(context, await service.SelectAlternativeAsync(context.User.RequireUserId(), shopId, proposalId, body?.Key, context.IfMatch(), ct)))
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ProposalNotFound, ProblemCodes.ProposalAlternativeUnknown, ProblemCodes.ProposalLocked,
                ProblemCodes.ConcurrencyConflict, ProblemCodes.ShopSampleOnly, ProblemCodes.ValidationFailed);

        shop.MapPut("/proposals/{proposalId:guid}/text", async (Guid shopId, Guid proposalId, EditProposalTextRequest? body, HttpContext context,
                FixProposalService service, CancellationToken ct) =>
                Accepted(context, await service.EditTextAsync(context.User.RequireUserId(), shopId, proposalId, body?.Text, context.IfMatch(), ct)))
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ProposalNotFound, ProblemCodes.ProposalTextEmpty, ProblemCodes.ProposalTextUnchanged,
                ProblemCodes.ProposalTextTooLong, ProblemCodes.ProposalLocked, ProblemCodes.ConcurrencyConflict, ProblemCodes.ShopSampleOnly, ProblemCodes.ValidationFailed);

        shop.MapPut("/proposals/{proposalId:guid}/placeholders", async (Guid shopId, Guid proposalId, ProposalPlaceholdersRequest? body, HttpContext context,
                FixProposalService service, CancellationToken ct) =>
                Accepted(context, await service.FillPlaceholdersAsync(context.User.RequireUserId(), shopId, proposalId, body?.Values, context.IfMatch(), ct)))
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ProposalNotFound, ProblemCodes.ProposalPlaceholderUnknown, ProblemCodes.ProposalPlaceholderEmpty,
                ProblemCodes.ProposalLocked, ProblemCodes.ConcurrencyConflict, ProblemCodes.ShopSampleOnly, ProblemCodes.ValidationFailed);

        shop.MapPost("/proposals/{proposalId:guid}/accept", async (Guid shopId, Guid proposalId, HttpContext context, FixProposalService service, CancellationToken ct) =>
                Tagged(context, await service.AcceptAsync(context.User.RequireUserId(), shopId, proposalId, context.IfMatch(), ct)))
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ProposalNotFound, ProblemCodes.ProposalRecheckPending, ProblemCodes.ProposalRecheckFailed,
                ProblemCodes.ProposalPlaceholderMissing, ProblemCodes.ProposalLocked, ProblemCodes.ConcurrencyConflict, ProblemCodes.FindingTransitionNotAllowed,
                ProblemCodes.ShopSampleOnly, ProblemCodes.ValidationFailed);

        shop.MapPost("/proposals/{proposalId:guid}/reject", async (Guid shopId, Guid proposalId, HttpContext context, FixProposalService service, CancellationToken ct) =>
                Tagged(context, await service.RejectAsync(context.User.RequireUserId(), shopId, proposalId, context.IfMatch(), ct)))
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ProposalNotFound, ProblemCodes.ProposalLocked, ProblemCodes.ConcurrencyConflict,
                ProblemCodes.ShopSampleOnly, ProblemCodes.ValidationFailed);

        shop.MapPost("/proposals/{proposalId:guid}/unaccept", async (Guid shopId, Guid proposalId, HttpContext context, FixProposalService service, CancellationToken ct) =>
                Tagged(context, await service.UnacceptAsync(context.User.RequireUserId(), shopId, proposalId, context.IfMatch(), ct)))
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ProposalNotFound, ProblemCodes.ProposalAlreadyPublished, ProblemCodes.ProposalLocked,
                ProblemCodes.ConcurrencyConflict, ProblemCodes.ShopSampleOnly, ProblemCodes.ValidationFailed);
        return shop;
    }

    private static Ok<ProposalDto> Tagged(HttpContext context, ProposalDto proposal)
    {
        context.SetETag(proposal.Version);
        return TypedResults.Ok(proposal);
    }

    /// <summary>A change of the text: <c>202</c>, the recheck runs as a job.</summary>
    private static Accepted<ProposalDto> Accepted(HttpContext context, ProposalDto proposal)
    {
        context.SetETag(proposal.Version);
        return TypedResults.Accepted((string?)null, proposal);
    }
}
