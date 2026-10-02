namespace EshopGuard.Data.Tenancy;

/// <summary>Default <see cref="ITenantContext"/> (registered as scoped).</summary>
public sealed class TenantContext : ITenantContext
{
    /// <inheritdoc />
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    public Guid? UserId { get; private set; }

    /// <inheritdoc />
    public bool UserScope { get; private set; }

    /// <inheritdoc />
    public void SetUser(Guid? userId)
    {
        if (UserScope && UserId is { } current && userId != current)
        {
            throw new InvalidOperationException("The user of this scope is already set to another user.");
        }

        UserScope = true;
        UserId = userId ?? UserId;
    }

    /// <inheritdoc />
    public void Set(Guid tenantId, Guid? userId = null)
    {
        userId ??= UserId;
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
