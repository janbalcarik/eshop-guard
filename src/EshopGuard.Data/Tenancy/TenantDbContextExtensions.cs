using EshopGuard.Data.Entities.Common;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Data.Tenancy;

/// <summary>Running tenant work in a transaction with <c>app.tenant_id</c>.</summary>
public static class TenantDbContextExtensions
{
    /// <summary>
    /// Requires the tenant, opens a transaction through the EF execution strategy (the interceptor sets <c>app.tenant_id</c>),
    /// runs the work and commits. Reads of tenant data outside such a transaction fail in the database (42501), never return
    /// an empty result.
    /// </summary>
    public static async Task<T> ExecuteInTenantTransactionAsync<T>(this EshopGuardDb db, Func<Task<T>> work, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(work);
        db.TenantContext.RequireTenantId();
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            var result = await work().ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return result;
        }).ConfigureAwait(false);
    }

    /// <summary>Variant without a result.</summary>
    public static Task ExecuteInTenantTransactionAsync(this EshopGuardDb db, Func<Task> work, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        return db.ExecuteInTenantTransactionAsync(async () =>
        {
            await work().ConfigureAwait(false);
            return true;
        }, ct);
    }

    /// <summary>Marks a soft-deletable entity for a real delete (only the tenant deletion job).</summary>
    public static void HardDelete(this EshopGuardDb db, ISoftDeletable entity)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(entity);
        db.MarkHardDelete(entity);
        db.Remove(entity);
    }
}
