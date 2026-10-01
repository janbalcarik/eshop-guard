namespace EshopGuard.Data.Connections;

/// <summary>
/// Database role a host connects as. The expected role is fixed in the host's code, never in configuration.
/// </summary>
public enum DatabaseRole
{
    /// <summary><c>eshopguard_owner</c>: owner of the database and objects, runs migrations.</summary>
    Owner,

    /// <summary><c>eshopguard_app</c>: the API, subject to RLS.</summary>
    App,

    /// <summary><c>eshopguard_worker</c>: the worker, subject to RLS.</summary>
    Worker,

    /// <summary><c>eshopguard_admin</c>: support tooling, bypasses RLS.</summary>
    Admin,
}

/// <summary>Names belonging to a <see cref="DatabaseRole"/>.</summary>
public static class DatabaseRoleNames
{
    /// <summary>PostgreSQL role name.</summary>
    public static string RoleName(this DatabaseRole role) => role switch
    {
        DatabaseRole.Owner => "eshopguard_owner",
        DatabaseRole.App => "eshopguard_app",
        DatabaseRole.Worker => "eshopguard_worker",
        DatabaseRole.Admin => "eshopguard_admin",
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    /// <summary>Name under <c>ConnectionStrings</c> the host reads.</summary>
    public static string ConnectionStringName(this DatabaseRole role) => role switch
    {
        DatabaseRole.Owner => "Migrations",
        DatabaseRole.App => "App",
        DatabaseRole.Worker => "Worker",
        DatabaseRole.Admin => "Admin",
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    /// <summary>Full configuration key, e.g. <c>ConnectionStrings:App</c>.</summary>
    public static string ConnectionStringKey(this DatabaseRole role) => "ConnectionStrings:" + role.ConnectionStringName();

    /// <summary><c>Application Name</c> of the connection, visible in <c>pg_stat_activity</c>.</summary>
    public static string ApplicationName(this DatabaseRole role) => role switch
    {
        DatabaseRole.Owner => "eshopguard-migrations",
        DatabaseRole.App => "eshopguard-api",
        DatabaseRole.Worker => "eshopguard-worker",
        DatabaseRole.Admin => "eshopguard-admin",
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };
}
