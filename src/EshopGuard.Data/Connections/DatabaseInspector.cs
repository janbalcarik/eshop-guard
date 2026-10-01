using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EshopGuard.Data.Connections;

/// <summary>Result of one database check: <see cref="Code"/> is <c>null</c> when the check passed.</summary>
public sealed record DatabaseCheck(string? Code, string? Key = null)
{
    /// <summary>The check passed.</summary>
    public bool Ok => Code is null;

    /// <summary>Migrations applied in the database but unknown to the code (only from the migration check).</summary>
    public IReadOnlyList<string> Ahead { get; init; } = [];

    /// <summary>Passed check.</summary>
    public static DatabaseCheck Passed { get; } = new((string?)null);
}

/// <summary>
/// Checks shared by the startup guard and <c>/health</c>: connectivity, role of the connection and migrations.
/// Every failure maps to a code; exception texts never leave this class.
/// </summary>
public sealed class DatabaseInspector(EshopGuardDataSource dataSource, IServiceScopeFactory scopes)
{
    /// <summary>Opens a connection and runs <c>SELECT 1</c>.</summary>
    public async Task<DatabaseCheck> CheckConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            await using var command = dataSource.Source.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return DatabaseCheck.Passed;
        }
        catch (Exception ex) when (Classify(ex) is { } failure)
        {
            return failure;
        }
    }

    /// <summary>Verifies the connection is the expected role, not a superuser and not BYPASSRLS.</summary>
    public async Task<DatabaseCheck> CheckRoleAsync(CancellationToken ct = default)
    {
        try
        {
            await using var command = dataSource.Source.CreateCommand(
                "SELECT current_user::text, r.rolsuper, r.rolbypassrls FROM pg_roles r WHERE r.rolname = current_user");
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return new DatabaseCheck(DataErrorCodes.UnexpectedRole);
            }

            var info = new RoleInfo(reader.GetString(0), reader.GetBoolean(1), reader.GetBoolean(2));
            return DatabaseRoleGuard.Evaluate(info, dataSource.Role) is { } code ? new DatabaseCheck(code) : DatabaseCheck.Passed;
        }
        catch (Exception ex) when (Classify(ex) is { } failure)
        {
            return failure;
        }
    }

    /// <summary>Verifies the database has every migration the code knows.</summary>
    public async Task<DatabaseCheck> CheckMigrationsAsync(CancellationToken ct = default)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<EshopGuardDb>();
            var applied = (await db.Database.GetAppliedMigrationsAsync(ct).ConfigureAwait(false)).ToList();
            var status = MigrationStatus.Evaluate(applied, db.Database.GetMigrations().ToList());
            return (status.HasPending ? new DatabaseCheck(DataErrorCodes.MigrationsPending) : DatabaseCheck.Passed) with { Ahead = status.Ahead };
        }
        catch (Exception ex) when (Classify(ex) is { } failure)
        {
            return failure;
        }
    }

    /// <summary>Maps an exception to a code; <c>null</c> for exceptions that must propagate (cancellation).</summary>
    public static DatabaseCheck? Classify(Exception ex) => ex switch
    {
        OperationCanceledException => null,
        EshopGuardConfigurationException config => new DatabaseCheck(config.Code, config.Key),
        PostgresException { SqlState: PostgresErrorCodes.InvalidPassword or PostgresErrorCodes.InvalidAuthorizationSpecification }
            => new DatabaseCheck(DataErrorCodes.AuthenticationFailed),
        PostgresException { SqlState: PostgresErrorCodes.InvalidCatalogName or PostgresErrorCodes.CannotConnectNow or PostgresErrorCodes.AdminShutdown }
            => new DatabaseCheck(DataErrorCodes.Unreachable),
        PostgresException => new DatabaseCheck(DataErrorCodes.QueryFailed),
        NpgsqlException or SocketException or TimeoutException => new DatabaseCheck(DataErrorCodes.Unreachable),
        InvalidOperationException { InnerException: { } inner } => Classify(inner) ?? new DatabaseCheck(DataErrorCodes.QueryFailed),
        _ => new DatabaseCheck(DataErrorCodes.QueryFailed),
    };
}
