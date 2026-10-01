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

    /// <summary>Sets the tenant; setting a different tenant again throws <see cref="InvalidOperationException"/>.</summary>
    void Set(Guid tenantId, Guid? userId = null);

    /// <summary>Current tenant, or <see cref="TenantNotSetException"/>.</summary>
    Guid RequireTenantId();
}
