using EshopGuard.Data;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Tenants;

/// <summary>The role of a user in a tenant and the tenant's state.</summary>
public sealed record TenantAccess(TenantRole Role, TenantStatus Status);

/// <summary>
/// Membership of the signed-in user in the tenant of the address (AD 6), read before any tenant data in a transaction of the
/// user without a tenant (policy <c>memberships_select_own</c>). No membership, or a deleted tenant, is <c>null</c>: a foreign
/// tenant looks like a missing one.
/// </summary>
public sealed class TenantAccessService(EshopGuardDb db)
{
    private static readonly string[] TenantFilter = [EshopGuardDb.TenantFilter];
    private static readonly string[] SoftDelete = [EshopGuardDb.SoftDeleteFilter];

    public Task<TenantAccess?> FindAsync(Guid userId, Guid tenantId, CancellationToken ct) => db.ExecuteInUserTransactionAsync(async () =>
    {
        await db.SwitchTransactionContextAsync(TenantSql.NoTenant, userId, ct).ConfigureAwait(false);
        var row = await db.Memberships.IgnoreQueryFilters(TenantFilter).AsNoTracking()
            .Where(m => m.UserId == userId && m.TenantId == tenantId)
            .Join(db.Tenants.IgnoreQueryFilters(SoftDelete), m => m.TenantId, t => t.Id, (m, t) => new { m.Role, t.Status, t.DeletedAt })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return row is null || row.DeletedAt is not null || row.Status == TenantStatus.Deleted
            ? null
            : new TenantAccess(TenantRoles.FromMembership(row.Role), row.Status);
    }, ct);
}
