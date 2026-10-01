namespace EshopGuard.Data.Tenancy;

/// <summary>Default <see cref="ITenantContext"/> (registered as scoped).</summary>
public sealed class TenantContext : ITenantContext
{
    /// <inheritdoc />
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    public Guid? UserId { get; private set; }

    /// <inheritdoc />
    public void Set(Guid tenantId, Guid? userId = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Empty tenant id.", nameof(tenantId));
        }

        if (TenantId is { } current && (current != tenantId || UserId != userId))
        {
            throw new InvalidOperationException("The tenant context of this scope is already set to another tenant or user.");
        }

        TenantId = tenantId;
        UserId = userId;
    }

    /// <inheritdoc />
    public Guid RequireTenantId() => TenantId ?? throw new TenantNotSetException();
}
