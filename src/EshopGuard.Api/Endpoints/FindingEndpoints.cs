using EshopGuard.Api.Contracts;
using EshopGuard.Api.Problems;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Findings;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Tenants;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace EshopGuard.Api.Endpoints;

/// <summary>Overview, pages, findings, search, export and the decisions about a finding (change 11, design A and B).</summary>
public static class FindingEndpoints
{
    /// <summary>The group <c>/shops/{shopId}</c> of the endpoints of change 11.</summary>
    public static RouteGroupBuilder MapShopWorkGroup(this RouteGroupBuilder tenant) => tenant.MapGroup("/shops/{shopId:guid}").WithTags("fixes");

    public static RouteGroupBuilder MapFindingEndpoints(this RouteGroupBuilder shop)
    {
        shop.MapGet("/overview", async (Guid shopId, OverviewService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(shopId, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound);

        shop.MapGet("/pages", async (Guid shopId, string? tab, string? language, string? q, string? cursor, int? limit, PageWorkQueryService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListAsync(shopId, tab, language, q, cursor, limit, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ValidationFailed);

        shop.MapGet("/pages/tabs", async (Guid shopId, string? language, PageWorkQueryService service, CancellationToken ct) =>
                TypedResults.Ok(await service.TabsAsync(shopId, language, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound);

        shop.MapGet("/findings", async (Guid shopId, [AsParameters] FindingQuery query, string? cursor, int? limit, FindingQueryService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListAsync(shopId, query.Filter(), cursor, limit, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ValidationFailed);

        shop.MapGet("/findings/tabs", async (Guid shopId, string? language, FindingQueryService service, CancellationToken ct) =>
                TypedResults.Ok(await service.TabsAsync(shopId, language, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound);

        shop.MapGet("/findings/export.csv", async (Guid shopId, [AsParameters] FindingQuery query, string? locale, HttpContext context,
                FindingsCsvExporter exporter, UserLocales locales, CancellationToken ct) =>
            {
                var userId = context.User.RequireUserId();
                var export = await exporter.ExportAsync(userId, shopId, query.Filter(), locale ?? await locales.LocaleAsync(userId, ct), ct);
                if (export.Truncated)
                {
                    context.Response.Headers["X-EshopGuard-Truncated"] = "true";
                }

                return TypedResults.File(export.Content, "text/csv; charset=utf-8", export.FileName);
            })
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ValidationFailed, ProblemCodes.LocaleNotEnabled);

        shop.MapGet("/findings/{findingId:guid}", async (Guid shopId, Guid findingId, FindingQueryService service, CancellationToken ct) =>
                TypedResults.Ok(await service.DetailAsync(shopId, findingId, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.FindingNotFound);

        shop.MapPost("/findings/{findingId:guid}/keep", async (Guid shopId, Guid findingId, FindingDecisionRequest? body, HttpContext context,
                FindingDecisionService service, CancellationToken ct) =>
                TypedResults.Ok(await service.KeepAsync(context.User.RequireUserId(), shopId, findingId, body?.ReasonCode, ct)))
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.FindingNotFound, ProblemCodes.FindingTransitionNotAllowed, ProblemCodes.ShopSampleOnly, ProblemCodes.ValidationFailed);

        shop.MapPost("/findings/{findingId:guid}/dismiss", async (Guid shopId, Guid findingId, FindingDecisionRequest? body, HttpContext context,
                FindingDecisionService service, CancellationToken ct) =>
                TypedResults.Ok(await service.DismissAsync(context.User.RequireUserId(), shopId, findingId, body?.ReasonCode, ct)))
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.FindingNotFound, ProblemCodes.FindingTransitionNotAllowed, ProblemCodes.ShopSampleOnly, ProblemCodes.ValidationFailed);

        shop.MapPost("/findings/{findingId:guid}/reopen", async (Guid shopId, Guid findingId, HttpContext context, FindingDecisionService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ReopenAsync(context.User.RequireUserId(), shopId, findingId, ct)))
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.FindingNotFound, ProblemCodes.FindingTransitionNotAllowed, ProblemCodes.ShopSampleOnly);

        shop.MapGet("/search", async (Guid shopId, string? q, SearchService service, CancellationToken ct) => TypedResults.Ok(await service.SearchAsync(shopId, q, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.SearchQueryTooShort);
        return shop;
    }

    /// <summary><c>GET /api/catalog/rule-texts</c>: texts of the rules in a language, with <c>ETag</c> and a private cache of an hour.</summary>
    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder api)
    {
        var catalog = api.MapGroup("/catalog").WithTags("catalog").RequireAuthorization();
        catalog.MapGet("/rule-texts", async Task<Results<Ok<RuleTextCatalogDto>, StatusCodeHttpResult>> (string? locale, string? ruleSetIds, HttpContext context,
                RuleTextCatalog service, CancellationToken ct) =>
            {
                var ids = new List<Guid>();
                foreach (var part in (ruleSetIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    ids.Add(Guid.TryParse(part, out var id)
                        ? id
                        : throw DomainException.Validation(new ValidationResult().Add("ruleSetIds", ProblemCodes.Fields.ValueNotAllowed)));
                }

                var (result, etag) = await service.GetAsync(locale, ids, ct);
                context.Response.Headers.ETag = etag;
                context.Response.Headers.CacheControl = "private, max-age=3600";
                return context.Request.Headers.IfNoneMatch.ToString() == etag ? TypedResults.StatusCode(StatusCodes.Status304NotModified) : TypedResults.Ok(result);
            })
            .ProducesProblemCodes(ProblemCodes.ValidationFailed, ProblemCodes.LocaleNotEnabled, ProblemCodes.CatalogLocaleIncomplete, ProblemCodes.AuthUnauthenticated);
        return catalog;
    }
}

/// <summary>Filters of the list and the export of findings from the query string.</summary>
public sealed class FindingQuery
{
    public string? Checkability { get; init; }

    public string? Status { get; init; }

    public string? Module { get; init; }

    public string? Language { get; init; }

    public string? Jurisdiction { get; init; }

    public string? Q { get; init; }

    public string? GroupBy { get; init; }

    public FindingFilter Filter() => new(Checkability, Status, Module, Language, Jurisdiction, Q, GroupBy);
}
