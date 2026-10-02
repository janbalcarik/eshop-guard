using EshopGuard.Application.Contracts;
using EshopGuard.Application.Localization;

namespace EshopGuard.Api.Endpoints;

/// <summary><c>/api/ref</c>: enabled languages and the markets shown on the web (<c>preview</c>, <c>live</c>).</summary>
public static class RefEndpoints
{
    public static RouteGroupBuilder MapRefEndpoints(this RouteGroupBuilder api)
    {
        var reference = api.MapGroup("/ref").WithTags("ref");
        reference.MapGet("/locales", async (IRefCatalog catalog, CancellationToken ct) =>
            TypedResults.Ok((await catalog.GetLocalesAsync(ct)).Where(l => l.Enabled).Select(l => new LocaleDto(l.Code, l.Name)).ToList()));
        reference.MapGet("/markets", async (IRefCatalog catalog, CancellationToken ct) =>
            TypedResults.Ok((await catalog.GetMarketsAsync(ct)).Where(m => m.WebStatus is "preview" or "live")
                .Select(m => new MarketDto(m.Code, m.CountryCode, m.DefaultLocale, m.UiLocales, m.Currency, m.WebStatus, m.ChecksStatus)).ToList()));
        return reference;
    }
}
