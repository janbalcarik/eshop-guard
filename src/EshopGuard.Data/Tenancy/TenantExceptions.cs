namespace EshopGuard.Data.Tenancy;

/// <summary>Tenant data accessed without a tenant context; nothing was sent to the database.</summary>
public sealed class TenantNotSetException() : InvalidOperationException("tenant.not_set: no tenant in the tenant context")
{
    /// <summary>Error code.</summary>
    public string Code => "tenant.not_set";
}

/// <summary>A row of another tenant was about to be written; the message names only the entity type, never values.</summary>
public sealed class CrossTenantWriteException(string entityType) : InvalidOperationException($"tenant.cross_write: {entityType}")
{
    /// <summary>Error code.</summary>
    public string Code => "tenant.cross_write";

    /// <summary>Name of the entity type.</summary>
    public string EntityType { get; } = entityType;
}
