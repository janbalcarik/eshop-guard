using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EshopGuard.Data.Connections;

/// <summary>Startup refused by <see cref="DatabaseStartupGuard"/>; the message is only the code.</summary>
public sealed class DatabaseStartupException(string code, string? key = null)
    : Exception(key is null ? code : $"{code}: {key}")
{
    /// <summary>Error code, e.g. <c>db.role_bypasses_rls</c>.</summary>
    public string Code { get; } = code;
}

/// <summary>
/// First hosted service of the API and the worker. Stops the host before anything else starts (before Kestrel listens)
/// when the connection string is missing, the database is unreachable, the role is a superuser, has BYPASSRLS or is not
/// the expected one, or a migration is pending. Logs only codes.
/// </summary>
public sealed class DatabaseStartupGuard(DatabaseInspector inspector, EshopGuardDataSource dataSource, ILogger<DatabaseStartupGuard> logger)
    : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Ensure(await inspector.CheckRoleAsync(cancellationToken).ConfigureAwait(false));

        var migrations = await inspector.CheckMigrationsAsync(cancellationToken).ConfigureAwait(false);
        if (migrations.Ahead.Count > 0)
        {
            logger.LogWarning("Database check: {Code} ({Count} unknown migrations)", DataErrorCodes.MigrationsAhead, migrations.Ahead.Count);
        }

        Ensure(migrations);
        logger.LogInformation("Database check passed: role {Role}", dataSource.Role.RoleName());
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void Ensure(DatabaseCheck check)
    {
        if (check.Ok)
        {
            return;
        }

        if (check.Key is null)
        {
            logger.LogCritical("Database startup check failed: {Code}", check.Code);
        }
        else
        {
            logger.LogCritical("Database startup check failed: {Code} {Key}", check.Code, check.Key);
        }

        throw new DatabaseStartupException(check.Code!, check.Key);
    }
}
