using EshopGuard.Api.Contracts;
using EshopGuard.Api.Http;
using EshopGuard.Api.Problems;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Protocols;
using EshopGuard.Application.Tenants;

namespace EshopGuard.Api.Endpoints;

/// <summary>Protocols of the checks (change 11, AD 11): requested by an editor, rendered by the worker, given by a signed link.</summary>
public static class ProtocolEndpoints
{
    public static RouteGroupBuilder MapProtocolEndpoints(this RouteGroupBuilder shop)
    {
        shop.MapGet("/protocols", async (Guid shopId, ProtocolService service, CancellationToken ct) => TypedResults.Ok(await service.ListAsync(shopId, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound);

        shop.MapPost("/protocols", async (Guid shopId, ProtocolRequest? body, HttpContext context, ProtocolService service, CancellationToken ct) =>
                TypedResults.Accepted((string?)null,
                    await service.RequestAsync(context.User.RequireUserId(), shopId, body?.PeriodFrom, body?.PeriodTo, body?.Locale, ct)))
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ProtocolPeriodInvalid, ProblemCodes.LocaleNotEnabled, ProblemCodes.ProtocolNoCompletedRun,
                ProblemCodes.ValidationFailed);

        shop.MapGet("/protocols/{protocolId:guid}", async (Guid shopId, Guid protocolId, ProtocolService service, CancellationToken ct) =>
                TypedResults.Ok(await service.DetailAsync(shopId, protocolId, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ProtocolNotFound);

        shop.MapGet("/protocols/{protocolId:guid}/pdf", async (Guid shopId, Guid protocolId, ProtocolService service, BlobLinks links, CancellationToken ct) =>
            {
                var (key, fileName) = await service.PdfAsync(shopId, protocolId, ct);
                return TypedResults.Redirect(await links.LinkAsync(key, fileName, "application/pdf", ct));
            })
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ProtocolNotFound, ProblemCodes.ProtocolNotReady, ProblemCodes.ProtocolFailed);
        return shop;
    }
}
