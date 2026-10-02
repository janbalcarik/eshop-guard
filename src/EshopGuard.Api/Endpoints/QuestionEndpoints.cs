using EshopGuard.Api.Contracts;
using EshopGuard.Api.Problems;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Fixes;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Tenants;

namespace EshopGuard.Api.Endpoints;

/// <summary>Questions for the merchant and their answers (change 11, design D).</summary>
public static class QuestionEndpoints
{
    public static RouteGroupBuilder MapQuestionEndpoints(this RouteGroupBuilder shop)
    {
        shop.MapGet("/questions", async (Guid shopId, string? scope, string? status, int? limit, QuestionService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListAsync(shopId, scope, status, limit, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ValidationFailed);

        shop.MapPost("/questions/{questionId:guid}/answer", async (Guid shopId, Guid questionId, AnswerQuestionRequest? body, HttpContext context,
                QuestionService service, CancellationToken ct) =>
                TypedResults.Ok(await service.AnswerAsync(context.User.RequireUserId(), shopId, questionId, body?.Answer, ct)))
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.QuestionNotFound, ProblemCodes.QuestionAnswerLocked, ProblemCodes.BudgetDailyLimitReached,
                ProblemCodes.FindingTransitionNotAllowed, ProblemCodes.ShopSampleOnly, ProblemCodes.ValidationFailed);
        return shop;
    }
}
