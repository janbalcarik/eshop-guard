using EshopGuard.Api.Problems;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Tenants;

namespace EshopGuard.Api.Tenancy;

/// <summary>The lowest role of a member that may call the endpoint (AD 6).</summary>
public sealed class TenantRoleMetadata(TenantRole role)
{
    public TenantRole Role { get; } = role;
}

/// <summary>The endpoint works also in a suspended tenant (reading, paying; K rozhodnutí 10).</summary>
public sealed class AllowSuspendedTenantMetadata;

public static class TenantRoleExtensions
{
    public static TBuilder RequireTenantRole<TBuilder>(this TBuilder builder, TenantRole role)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.WithMetadata(new TenantRoleMetadata(role));
        if (role > TenantRole.Viewer)
        {
            builder.ProducesProblemCodes(ProblemCodes.AuthForbiddenRole);
        }

        return builder;
    }

    public static TBuilder AllowSuspendedTenant<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new AllowSuspendedTenantMetadata());

    /// <summary>The role of the caller in the tenant of the address, set by <see cref="TenantAccessFilter"/>.</summary>
    public static TenantRole CallerRole(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Items[TenantAccessFilter.RoleKey] is TenantRole role
            ? role
            : throw new InvalidOperationException("The tenant access filter did not run.");
    }
}
