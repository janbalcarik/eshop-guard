using EshopGuard.Api.Problems;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Tenants;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Tenancy;

namespace EshopGuard.Api.Tenancy;

/// <summary>
/// The tenant of <c>/api/t/{tenantId}</c> (AD 6), before anything touches its data: no session → <c>401
/// auth.unauthenticated</c>; no membership or a deleted tenant → <c>404 tenant.not_found</c> (a foreign tenant looks like a
/// missing one); suspended → <c>403 tenant.suspended</c> unless the endpoint allows it; a role below the endpoint's →
/// <c>403 auth.forbidden_role</c> with <c>params.requiredRole</c>. Then the tenant context is set, so every transaction runs with
/// <c>app.tenant_id</c> under RLS.
/// </summary>
public sealed class TenantAccessFilter(TenantAccessService access, ITenantContext tenantContext) : IEndpointFilter
{
    public const string RoleKey = "eg.role";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var http = context.HttpContext;
        var userId = http.User.UserId();
        if (userId is null)
        {
            return EgProblem.Result(http, ProblemCodes.AuthUnauthenticated, StatusCodes.Status401Unauthorized);
        }

        if (!Guid.TryParse(http.Request.RouteValues["tenantId"]?.ToString(), out var tenantId))
        {
            return EgProblem.Result(http, ProblemCodes.TenantNotFound, StatusCodes.Status404NotFound);
        }

        var metadata = http.GetEndpoint()?.Metadata;
        var required = metadata?.GetMetadata<TenantRoleMetadata>()?.Role
            ?? throw new InvalidOperationException("An endpoint under /api/t/ has no TenantRoleMetadata.");
        var found = await access.FindAsync(userId.Value, tenantId, http.RequestAborted).ConfigureAwait(false);
        if (found is null)
        {
            return EgProblem.Result(http, ProblemCodes.TenantNotFound, StatusCodes.Status404NotFound);
        }

        if (found.Status == TenantStatus.Suspended && metadata.GetMetadata<AllowSuspendedTenantMetadata>() is null)
        {
            return EgProblem.Result(http, ProblemCodes.TenantSuspended, StatusCodes.Status403Forbidden);
        }

        if (!found.Role.AtLeast(required))
        {
            return EgProblem.Result(http, ProblemCodes.AuthForbiddenRole, StatusCodes.Status403Forbidden,
                new Dictionary<string, object?> { ["requiredRole"] = required.Code() });
        }

        tenantContext.Set(tenantId, userId);
        http.Items[RoleKey] = found.Role;
        return await next(context).ConfigureAwait(false);
    }
}
