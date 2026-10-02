namespace EshopGuard.Data.Tenancy;

/// <summary>
/// Tenant of the current unit of work (one API request, one worker job). Scoped; set once.
/// </summary>
public interface ITenantContext
{
    /// <summary>Current tenant, or <c>null</c> when not set.</summary>
    Guid? TenantId { get; }

    /// <summary>Current user (for <c>app.user_id</c>), or <c>null</c>.</summary>
    Guid? UserId { get; }

    /// <summary>
    /// The unit of work runs for a user (or an anonymous request of the sign-in) before a tenant is chosen: its transactions get
    /// <c>app.user_id</c> and the tenant <see cref="TenantSql.NoTenant"/>, which owns no row, so tenant tables show only what
    /// the policies by user allow (own memberships, an invitation by its token) and every write of tenant data fails.
    /// </summary>
    bool UserScope { get; }

    /// <summary>Sets the tenant; setting a different tenant again throws <see cref="InvalidOperationException"/>.</summary>
    void Set(Guid tenantId, Guid? userId = null);

    /// <summary>Starts the scope of a user (<see cref="UserScope"/>); <paramref name="userId"/> null for an anonymous request.</summary>
    void SetUser(Guid? userId);

    /// <summary>Current tenant, or <see cref="TenantNotSetException"/>.</summary>
    Guid RequireTenantId();
}
