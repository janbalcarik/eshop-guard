using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Identity.Validators;
using EshopGuard.Application.Problems;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Tenants;

/// <summary>
/// Members of the tenant of the address (tenant set by the access filter): the list, a change of role, removal or leaving and
/// the transfer of ownership, by <see cref="MembershipRules"/>. A tenant always keeps an owner: the owner rows are locked
/// (<c>FOR UPDATE</c>) before the last one could go (<c>409 membership.last_owner</c>).
/// </summary>
public sealed class MembershipService(EshopGuardDb db, SecurityAuditWriter audit)
{
    public Task<IReadOnlyList<MemberDto>> ListAsync(CancellationToken ct) => db.ExecuteInTenantTransactionAsync(async () =>
        (IReadOnlyList<MemberDto>)(await db.Memberships.AsNoTracking()
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { m, u })
            .OrderBy(x => x.m.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false))
            .Select(x => ToDto(x.m, x.u)).ToList(), ct);

    public async Task<MemberDto> ChangeRoleAsync(Guid callerId, TenantRole callerRole, Guid userId, string? role, CancellationToken ct)
    {
        var validation = new ValidationResult();
        var newRole = FieldValidators.Role(validation, "role", role);
        validation.ThrowIfInvalid();
        var tenantId = db.TenantContext.RequireTenantId();
        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var owners = await LockOwnersAsync(ct).ConfigureAwait(false);
            var membership = await FindAsync(userId, ct).ConfigureAwait(false);
            var target = TenantRoles.FromMembership(membership.Role);
            var wanted = TenantRoles.FromMembership(newRole!.Value);
            if (!MembershipRules.CanChangeRole(callerRole, target, wanted))
            {
                throw RoleNotAllowed(wanted);
            }

            if (target == TenantRole.Owner && wanted != TenantRole.Owner && owners <= 1)
            {
                throw new DomainException(ProblemCodes.MembershipLastOwner, 409);
            }

            if (target != wanted)
            {
                membership.Role = newRole.Value;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await audit.WriteAsync(new AuditEvent(AuditActions.MembershipRoleChanged, tenantId, callerId, "membership", userId.ToString("D"),
                    new JsonObject { ["from"] = target.Code(), ["to"] = wanted.Code() }), ct).ConfigureAwait(false);
            }

            var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct).ConfigureAwait(false);
            return ToDto(membership, user);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Removes a member, or the caller leaves (anyone may leave, except the last owner).</summary>
    public async Task RemoveAsync(Guid callerId, TenantRole callerRole, Guid userId, CancellationToken ct)
    {
        var tenantId = db.TenantContext.RequireTenantId();
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var owners = await LockOwnersAsync(ct).ConfigureAwait(false);
            var membership = await FindAsync(userId, ct).ConfigureAwait(false);
            var target = TenantRoles.FromMembership(membership.Role);
            var self = callerId == userId;
            if (!MembershipRules.CanRemove(callerRole, target, self))
            {
                throw RoleNotAllowed(target);
            }

            if (target == TenantRole.Owner && owners <= 1)
            {
                throw new DomainException(ProblemCodes.MembershipLastOwner, 409);
            }

            db.Memberships.Remove(membership);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await audit.WriteAsync(new AuditEvent(self ? AuditActions.MembershipLeft : AuditActions.MembershipRemoved, tenantId, callerId,
                "membership", userId.ToString("D"), new JsonObject { ["role"] = target.Code() }), ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>The owner hands the ownership to a member, who becomes an owner; the caller becomes an admin.</summary>
    public async Task TransferOwnershipAsync(Guid callerId, TenantRole callerRole, Guid? userId, CancellationToken ct)
    {
        if (userId is null)
        {
            new ValidationResult().Add("userId", ProblemCodes.Fields.Required).ThrowIfInvalid();
        }

        if (!MembershipRules.CanTransferOwnership(callerRole))
        {
            throw RoleNotAllowed(TenantRole.Owner);
        }

        var tenantId = db.TenantContext.RequireTenantId();
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            await LockOwnersAsync(ct).ConfigureAwait(false);
            var target = await FindAsync(userId!.Value, ct).ConfigureAwait(false);
            if (userId == callerId)
            {
                return;
            }

            var caller = await FindAsync(callerId, ct).ConfigureAwait(false);
            if (caller.Role != MembershipRole.Owner)
            {
                throw RoleNotAllowed(TenantRole.Owner);
            }

            var previous = TenantRoles.FromMembership(target.Role);
            target.Role = MembershipRole.Owner;
            caller.Role = MembershipRole.Admin;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await audit.WriteAsync(new AuditEvent(AuditActions.OwnershipTransferred, tenantId, callerId, "membership", userId.Value.ToString("D"),
                new JsonObject { ["from"] = previous.Code(), ["previousOwnerRole"] = TenantRole.Admin.Code() }), ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    private static MemberDto ToDto(Membership membership, User user) =>
        new(user.Id, user.Email, user.DisplayName, membership.Role.Code(), membership.InvitedBy, membership.CreatedAt, user.LastLoginAt);

    private static DomainException RoleNotAllowed(TenantRole role) =>
        new(ProblemCodes.MembershipRoleNotAllowed, 403, new Dictionary<string, object?> { ["role"] = role.Code() });

    private async Task<Membership> FindAsync(Guid userId, CancellationToken ct) =>
        await db.Memberships.FirstOrDefaultAsync(m => m.UserId == userId, ct).ConfigureAwait(false)
        ?? throw new DomainException(ProblemCodes.MembershipNotFound, 404);

    /// <summary>Locks the owner rows of the tenant (two owners demoting each other wait for one another) and counts them.</summary>
    private async Task<int> LockOwnersAsync(CancellationToken ct)
    {
        await using var command = DbSql.Command(db, "SELECT user_id FROM iam.memberships WHERE tenant_id = @tenant AND role = 'owner' FOR UPDATE",
            DbSql.P("tenant", db.TenantContext.RequireTenantId()));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var count = 0;
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            count++;
        }

        return count;
    }
}
