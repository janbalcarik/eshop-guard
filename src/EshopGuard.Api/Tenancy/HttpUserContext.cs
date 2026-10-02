using System.Security.Claims;
using EshopGuard.Application;
using EshopGuard.Data.Tenancy;

namespace EshopGuard.Api.Tenancy;

/// <summary>Claims of the session (AD 7): <c>sub</c>, <c>amr</c> and <c>auth_time</c> (Unix seconds).</summary>
public static class SessionClaims
{
    public const string Subject = "sub";
    public const string Method = "amr";
    public const string AuthTime = "auth_time";

    public static Guid? UserId(this ClaimsPrincipal principal) =>
        principal?.Identity?.IsAuthenticated == true && Guid.TryParse(principal.FindFirstValue(Subject), out var id) ? id : null;

    public static Guid RequireUserId(this ClaimsPrincipal principal) =>
        principal.UserId() ?? throw new Application.Problems.DomainException(Application.Problems.ProblemCodes.AuthUnauthenticated, StatusCodes.Status401Unauthorized);

    public static DateTimeOffset? AuthenticatedAt(this ClaimsPrincipal principal) =>
        long.TryParse(principal?.FindFirstValue(AuthTime), out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;
}

/// <summary>
/// Per request, after the authentication: <see cref="RequestContext"/> (client address, <c>Accept-Language</c>, trace id) and
/// the user scope of <see cref="ITenantContext"/> (<c>app.user_id</c> of the signed-in user, or none). A tenant comes only from
/// the address through <see cref="TenantAccessFilter"/>.
/// </summary>
public sealed class HttpUserContextMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context, RequestContext request, ITenantContext tenant)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(tenant);
        request.Ip = context.Connection.RemoteIpAddress;
        request.AcceptLanguage = context.Request.Headers.AcceptLanguage.ToString() is { Length: > 0 and <= 512 } language ? language : null;
        request.TraceId = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;
        tenant.SetUser(context.User.UserId());
        return next(context);
    }
}
