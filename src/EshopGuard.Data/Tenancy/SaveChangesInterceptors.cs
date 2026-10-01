using EshopGuard.Data.Entities.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EshopGuard.Data.Tenancy;

/// <summary>
/// Fills <c>TenantId</c> of new tenant rows from the context and refuses a row of another tenant or a change of
/// <c>TenantId</c> before anything is sent to the database.
/// </summary>
public sealed class TenantSaveChangesInterceptor : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Check(eventData.Context);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Check(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void Check(DbContext? context)
    {
        if (context is not EshopGuardDb db)
        {
            return;
        }

        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.Entity is ITenantOwned && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var property = entry.Property("TenantId");
            var tenant = db.TenantContext.RequireTenantId();
            var current = (Guid?)property.CurrentValue;
            switch (entry.State)
            {
                case EntityState.Added when current is null || current == Guid.Empty:
                    // Also ops.audit_log: through EF a row always belongs to the context tenant
                    // (system actions with a null tenant are written by the admin role in plain SQL).
                    property.CurrentValue = tenant;
                    break;
                case EntityState.Added when current != tenant:
                    throw new CrossTenantWriteException(entry.Metadata.ClrType.Name);
                case EntityState.Modified or EntityState.Deleted when (Guid?)property.OriginalValue != tenant || current != tenant:
                    throw new CrossTenantWriteException(entry.Metadata.ClrType.Name);
            }
        }
    }
}

/// <summary>Sets <c>CreatedAt</c> on insert and <c>UpdatedAt</c> on insert and update.</summary>
public sealed class TimestampSaveChangesInterceptor(TimeProvider time) : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = time.GetUtcNow();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Entity is IHasCreatedAt created)
            {
                created.CreatedAt = now;
            }

            if (entry.State is EntityState.Added or EntityState.Modified && entry.Entity is IHasTimestamps updated)
            {
                updated.UpdatedAt = now;
            }
        }
    }
}

/// <summary>
/// Turns a delete of an <see cref="ISoftDeletable"/> into setting <c>DeletedAt</c>. A real delete only through
/// <see cref="TenantDbContextExtensions.HardDelete"/> (used by the tenant deletion job).
/// </summary>
public sealed class SoftDeleteSaveChangesInterceptor(TimeProvider time) : SaveChangesInterceptor
{
    internal const string HardDeleteAnnotation = "EshopGuard:HardDelete";

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Soften(eventData.Context);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Soften(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Soften(DbContext? context)
    {
        if (context is not EshopGuardDb db)
        {
            return;
        }

        foreach (var entry in db.ChangeTracker.Entries<ISoftDeletable>().Where(e => e.State == EntityState.Deleted).ToList())
        {
            if (db.IsHardDelete(entry.Entity))
            {
                continue;
            }

            entry.State = EntityState.Modified;
            entry.Entity.DeletedAt = time.GetUtcNow();
        }
    }
}
