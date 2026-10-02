namespace EshopGuard.Application.Tenants;

/// <summary>
/// The matrix of permissions over members (design of change 9, „Matice oprávnění“, K rozhodnutí 2), with no database: who
/// may change a role, remove a member, invite with a role and transfer the ownership. That a tenant keeps an owner
/// (<c>membership.last_owner</c>) is checked by the services against the database.
/// </summary>
public static class MembershipRules
{
    /// <summary>
    /// Change of the role of <paramref name="target"/> to <paramref name="newRole"/>: an owner changes any role except to owner
    /// (that is the transfer); an admin only switches editor ↔ viewer; nobody else.
    /// </summary>
    public static bool CanChangeRole(TenantRole caller, TenantRole target, TenantRole newRole) => caller switch
    {
        _ when newRole == TenantRole.Owner => false,
        TenantRole.Owner => true,
        TenantRole.Admin => target is TenantRole.Editor or TenantRole.Viewer && newRole is TenantRole.Editor or TenantRole.Viewer,
        _ => false,
    };

    /// <summary>Removal of a member: anyone leaves by himself; an owner removes anybody; an admin an editor or a viewer.</summary>
    public static bool CanRemove(TenantRole caller, TenantRole target, bool self) => self || caller switch
    {
        TenantRole.Owner => true,
        TenantRole.Admin => target is TenantRole.Editor or TenantRole.Viewer,
        _ => false,
    };

    /// <summary>An invitation with <paramref name="role"/>: an admin invites an editor or a viewer, only an owner an admin; never an owner.</summary>
    public static bool CanInvite(TenantRole caller, TenantRole role) => role switch
    {
        TenantRole.Owner => false,
        TenantRole.Admin => caller == TenantRole.Owner,
        _ => caller >= TenantRole.Admin,
    };

    /// <summary>Only an owner hands the ownership over (he becomes an admin).</summary>
    public static bool CanTransferOwnership(TenantRole caller) => caller == TenantRole.Owner;
}
