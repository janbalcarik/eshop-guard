using EshopGuard.Api.Problems;
using EshopGuard.Application.Options;
using EshopGuard.Application.Problems;
using Microsoft.Extensions.Options;

namespace EshopGuard.Api.Tenancy;

/// <summary>The endpoint is for the administrators of EshopGuard only (<c>Admin:UserIds</c>).</summary>
public sealed class PlatformAdminMetadata;

/// <summary>
/// Lets through only a signed-in user named in <c>Admin:UserIds</c> (change 12, the admin API of price lists): without a session
/// <c>401 auth.unauthenticated</c>, anyone else <c>403 auth.forbidden_role</c>. Fail-closed: an empty list lets nobody in.
/// </summary>
public sealed class PlatformAdminFilter(IOptionsMonitor<PlatformAdminOptions> options) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        if (context.HttpContext.User.Identity?.IsAuthenticated != true || context.HttpContext.User.UserId() is not { } userId)
        {
            throw new DomainException(ProblemCodes.AuthUnauthenticated, StatusCodes.Status401Unauthorized);
        }

        if (!options.CurrentValue.IsAdmin(userId))
        {
            throw new DomainException(ProblemCodes.AuthForbiddenRole, StatusCodes.Status403Forbidden);
        }

        return await next(context).ConfigureAwait(false);
    }
}

public static class PlatformAdminExtensions
{
    public static TBuilder RequirePlatformAdmin<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.WithMetadata(new PlatformAdminMetadata());
        builder.AddEndpointFilter<TBuilder, PlatformAdminFilter>();
        builder.ProducesProblemCodes(ProblemCodes.AuthUnauthenticated, ProblemCodes.AuthForbiddenRole);
        return builder;
    }
}
