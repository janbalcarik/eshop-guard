namespace EshopGuard.Data.Connections;

/// <summary>Codes reported by the startup guard, the health checks and the logs (never exception texts).</summary>
public static class DataErrorCodes
{
    /// <summary>The connection string for the role is not configured.</summary>
    public const string ConnectionStringMissing = "config.connection_string_missing";

    /// <summary>The database server cannot be reached.</summary>
    public const string Unreachable = "db.unreachable";

    /// <summary>The server rejected the login (wrong password or role without LOGIN).</summary>
    public const string AuthenticationFailed = "db.authentication_failed";

    /// <summary>A check query failed for another reason than connectivity or login.</summary>
    public const string QueryFailed = "db.query_failed";

    /// <summary>The connection is a superuser or has BYPASSRLS, so Row-Level Security would not apply.</summary>
    public const string RoleBypassesRls = "db.role_bypasses_rls";

    /// <summary>The connection uses another role than the host expects.</summary>
    public const string UnexpectedRole = "db.unexpected_role";

    /// <summary>The database lacks a migration the code knows.</summary>
    public const string MigrationsPending = "db.migrations_pending";

    /// <summary>The database has a migration the code does not know (older code after a rollback); logged only.</summary>
    public const string MigrationsAhead = "db.migrations_ahead";
}
