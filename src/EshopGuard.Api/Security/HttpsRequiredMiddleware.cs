using EshopGuard.Api.Problems;
using EshopGuard.Application.Problems;

namespace EshopGuard.Api.Security;

/// <summary>
/// <c>/api</c> only over HTTPS: the session and CSRF cookies are <c>Secure</c> and antiforgery refuses plain HTTP. Behind the
/// reverse proxy (Caddy) the scheme comes from <c>X-Forwarded-Proto</c> of a known proxy (<c>Proxy:KnownProxies</c>, loopback by
/// default). A request over plain HTTP gets <c>400 request.https_required</c> instead of an error of the server.
/// </summary>
public sealed class HttpsRequiredMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Request.Path.StartsWithSegments("/api", StringComparison.Ordinal) && !context.Request.IsHttps
            ? EgProblem.WriteAsync(context, EgProblem.Create(context, ProblemCodes.HttpsRequired, StatusCodes.Status400BadRequest))
            : next(context);
    }
}
