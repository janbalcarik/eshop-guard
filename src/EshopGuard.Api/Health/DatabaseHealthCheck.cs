using EshopGuard.Data.Connections;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EshopGuard.Api.Health;

/// <summary>Database checks of <c>/health</c>; the result carries only a code in <c>data["code"]</c>.</summary>
public static class DatabaseHealthCheck
{
    /// <summary>Key of the error code in <see cref="HealthCheckResult.Data"/>.</summary>
    public const string CodeKey = "code";

    /// <summary><c>database</c>: connection and <c>SELECT 1</c>.</summary>
    public sealed class Connection(DatabaseInspector inspector) : IHealthCheck
    {
        /// <inheritdoc />
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            ToResult(await inspector.CheckConnectionAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary><c>database_role</c>: expected role, not a superuser, no BYPASSRLS.</summary>
    public sealed class Role(DatabaseInspector inspector) : IHealthCheck
    {
        /// <inheritdoc />
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            ToResult(await inspector.CheckRoleAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary><c>migrations</c>: every migration the code knows is applied.</summary>
    public sealed class Migrations(DatabaseInspector inspector) : IHealthCheck
    {
        /// <inheritdoc />
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            ToResult(await inspector.CheckMigrationsAsync(cancellationToken).ConfigureAwait(false));
    }

    private static HealthCheckResult ToResult(DatabaseCheck check) => check.Ok
        ? HealthCheckResult.Healthy()
        : HealthCheckResult.Unhealthy(data: new Dictionary<string, object> { [CodeKey] = check.Code! });
}
