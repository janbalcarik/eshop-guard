using EshopGuard.Data.Entities.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

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

    /// <summary>
    /// A transaction of the user scope (<see cref="ITenantContext.UserScope"/>) or of the tenant when it is set: the
    /// interceptor sets <c>app.user_id</c> and the tenant (<see cref="TenantSql.NoTenant"/> without one).
    /// </summary>
    public static async Task<T> ExecuteInUserTransactionAsync<T>(this EshopGuardDb db, Func<Task<T>> work, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(work);
        if (!db.TenantContext.UserScope && db.TenantContext.TenantId is null)
        {
            throw new TenantNotSetException();
        }

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
    public static Task ExecuteInUserTransactionAsync(this EshopGuardDb db, Func<Task> work, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        return db.ExecuteInUserTransactionAsync(async () =>
        {
            await work().ConfigureAwait(false);
            return true;
        }, ct);
    }

    /// <summary>
    /// Sets <c>app.tenant_id</c> and <c>app.user_id</c> of the open transaction to other values (<c>SET LOCAL</c>): the tenant
    /// created in this transaction while its owner membership is written, or the user who has just proved his mailbox. The
    /// values are parameters; RLS checks every row against them. Without an open transaction
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    public static async Task SwitchTransactionContextAsync(this EshopGuardDb db, Guid tenantId, Guid? userId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var transaction = db.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction
            ?? throw new InvalidOperationException("Open a transaction before switching its tenant.");
        await using var command = TenantSql.CreateSetCommand(transaction.Connection!, transaction, tenantId, userId);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
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
