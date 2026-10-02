using EshopGuard.Data.Entities.Iam;

namespace EshopGuard.Application.Tenants;

/// <summary>Role of a member, ordered: <see cref="Viewer"/> &lt; <see cref="Editor"/> &lt; <see cref="Admin"/> &lt; <see cref="Owner"/>.</summary>
public enum TenantRole
{
    Viewer = 1,
    Editor = 2,
    Admin = 3,
    Owner = 4,
}

/// <summary>Codes and conversions of roles (<c>iam.memberships.role</c> stores <see cref="MembershipRole"/>).</summary>
public static class TenantRoles
{
    public static TenantRole FromMembership(MembershipRole role) => role switch
    {
        MembershipRole.Owner => TenantRole.Owner,
        MembershipRole.Admin => TenantRole.Admin,
        MembershipRole.Editor => TenantRole.Editor,
        MembershipRole.Viewer => TenantRole.Viewer,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    public static MembershipRole ToMembership(this TenantRole role) => role switch
    {
        TenantRole.Owner => MembershipRole.Owner,
        TenantRole.Admin => MembershipRole.Admin,
        TenantRole.Editor => MembershipRole.Editor,
        TenantRole.Viewer => MembershipRole.Viewer,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    /// <summary>Code of the role in the API and in the database (<c>owner</c>, <c>admin</c>, <c>editor</c>, <c>viewer</c>).</summary>
    public static string Code(this TenantRole role) => role switch
    {
        TenantRole.Owner => "owner",
        TenantRole.Admin => "admin",
        TenantRole.Editor => "editor",
        TenantRole.Viewer => "viewer",
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    public static string Code(this MembershipRole role) => FromMembership(role).Code();

    public static bool TryParse(string? code, out MembershipRole role)
    {
        switch (code?.Trim())
        {
            case "owner":
                role = MembershipRole.Owner;
                return true;
            case "admin":
                role = MembershipRole.Admin;
                return true;
            case "editor":
                role = MembershipRole.Editor;
                return true;
            case "viewer":
                role = MembershipRole.Viewer;
                return true;
            default:
                role = default;
                return false;
        }
    }

    /// <summary>Whether <paramref name="role"/> is at least <paramref name="minimum"/>.</summary>
    public static bool AtLeast(this TenantRole role, TenantRole minimum) => role >= minimum;
}
