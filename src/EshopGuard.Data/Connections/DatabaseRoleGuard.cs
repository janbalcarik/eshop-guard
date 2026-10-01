namespace EshopGuard.Data.Connections;

/// <summary>Attributes of the role a connection runs as (from <c>pg_roles</c>).</summary>
public sealed record RoleInfo(string CurrentUser, bool IsSuperuser, bool BypassesRls);

/// <summary>
/// Decides whether a connection may serve the host. A superuser bypasses RLS even with FORCE ROW LEVEL SECURITY,
/// so isolation tests would pass vacuously; such a connection is refused.
/// </summary>
public static class DatabaseRoleGuard
{
    /// <summary>Returns an error code, or <c>null</c> when the connection matches the expected role.</summary>
    public static string? Evaluate(RoleInfo info, DatabaseRole expected)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (info.IsSuperuser || (info.BypassesRls && expected != DatabaseRole.Admin))
        {
            return DataErrorCodes.RoleBypassesRls;
        }

        return string.Equals(info.CurrentUser, expected.RoleName(), StringComparison.Ordinal)
            ? null
            : DataErrorCodes.UnexpectedRole;
    }
}
