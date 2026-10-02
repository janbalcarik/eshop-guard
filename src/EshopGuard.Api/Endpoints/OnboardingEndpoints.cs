using EshopGuard.Api.Contracts;
using EshopGuard.Api.Problems;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Shops;
using EshopGuard.Application.Shops.Onboarding;
using EshopGuard.Application.Shops.Scope;
using EshopGuard.Application.Tenants;

namespace EshopGuard.Api.Endpoints;

/// <summary>The free sample, the places of sale, the language versions, the scope, the quote and the state of the onboarding (change 10).</summary>
public static class OnboardingEndpoints
{
    public static RouteGroupBuilder MapOnboardingEndpoints(this RouteGroupBuilder shops)
    {
        shops.MapPost("/{shopId:guid}/sample", async (Guid tenantId, Guid shopId, HttpContext context, SampleService service, CancellationToken ct) =>
                TypedResults.Accepted($"/api/t/{tenantId:D}/shops/{shopId:D}/sample", await service.StartAsync(context.User.RequireUserId(), shopId, ct)))
            .RequireTenantRole(TenantRole.Admin)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.SampleAlreadyUsedForDomain, ProblemCodes.SampleNotAllowedInStatus,
                ProblemCodes.ShopOwnershipNotVerified, ProblemCodes.RateLimited);

        shops.MapGet("/{shopId:guid}/sample", async (Guid shopId, SampleResultReader reader, CancellationToken ct) => TypedResults.Ok(await reader.GetAsync(shopId, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.SampleNotStarted);

        shops.MapGet("/{shopId:guid}/markets", async (Guid shopId, MarketService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(shopId, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound);

        shops.MapPut("/{shopId:guid}/markets", async (Guid shopId, ConfirmMarketsRequest? body, HttpContext context, MarketService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ConfirmAsync(context.User.RequireUserId(), shopId, body!.Active, ct)))
            .RequireTenantRole(TenantRole.Admin)
            .Validate<ConfirmMarketsRequest>()
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.MarketsNoneSelected, ProblemCodes.MarketsUnsupported, ProblemCodes.MarketsUnknown,
                ProblemCodes.MarketsLockedDuringRun);

        shops.MapGet("/{shopId:guid}/languages", async (Guid shopId, LanguageVersionService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(shopId, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound);

        shops.MapPost("/{shopId:guid}/languages/{language}/confirmation", async (Guid shopId, string language, LanguageConfirmationRequest? body, HttpContext context,
                LanguageVersionService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ConfirmAsync(context.User.RequireUserId(), shopId, language, body!.BelongsToShop, ct)))
            .RequireTenantRole(TenantRole.Admin)
            .Validate<LanguageConfirmationRequest>()
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.LanguageNotFound, ProblemCodes.LanguageNotAwaitingConfirmation);

        shops.MapPut("/{shopId:guid}/languages/{language}/exclusion", async (Guid shopId, string language, LanguageExclusionRequest? body, HttpContext context,
                LanguageVersionService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ExcludeAsync(context.User.RequireUserId(), shopId, language, body!.Excluded, ct)))
            .RequireTenantRole(TenantRole.Admin)
            .Validate<LanguageExclusionRequest>()
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.LanguageNotFound, ProblemCodes.LanguageLastCheckedVersion, ProblemCodes.LanguageAwaitingConfirmation);

        shops.MapGet("/{shopId:guid}/scope", async (Guid shopId, ScopeService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(shopId, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ScopeBasisMissing, ProblemCodes.QuoteSampleNotFinished);

        shops.MapPost("/{shopId:guid}/quote", async (Guid shopId, QuoteRequest? body, HttpContext context, ScopeService service, CancellationToken ct) =>
                TypedResults.Ok(await service.QuoteAsync(context.User.RequireUserId(), shopId, body?.ActiveMarkets, body?.ExcludedLanguages, ct)))
            .RequireTenantRole(TenantRole.Admin)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.QuoteSampleNotFinished, ProblemCodes.QuoteBasisMissing, ProblemCodes.MarketsNoneSelected,
                ProblemCodes.MarketsUnsupported, ProblemCodes.MarketsUnknown, ProblemCodes.ScopeNoCheckableVersion, ProblemCodes.ScopeProductCountUnknown,
                ProblemCodes.BillingUnavailable);

        shops.MapGet("/{shopId:guid}/onboarding", async (Guid shopId, OnboardingStateService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(shopId, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound);
        return shops;
    }
}
