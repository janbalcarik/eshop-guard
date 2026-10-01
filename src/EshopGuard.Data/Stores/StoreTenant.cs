using EshopGuard.Data.Connections;
using Npgsql;

namespace EshopGuard.Data.Stores;

/// <summary>The tenant (and, in a job of the web application, the e-shop) whose cache the stores read and write.</summary>
public interface IStoreTenant
{
    Guid TenantId { get; }

    /// <summary>The e-shop of the job; null in the CLI, whose profiles find (or create) the e-shop by the site key.</summary>
    Guid? ShopId => null;
}

/// <summary>A tenant fixed for the whole process (the CLI).</summary>
public sealed record FixedStoreTenant(Guid TenantId) : IStoreTenant;

/// <summary>
/// The reserved tenant of the CLI. <c>eshopguard cache init</c> creates it (<c>iam.ensure_cli_tenant()</c>, migration
/// F3); a scan only checks that it exists, so nothing is written into a database without that command.
/// </summary>
public static class CliTenant
{
    /// <summary>The fixed id (the same in the SQL of migration F3).</summary>
    public static readonly Guid Id = new("00000000-0000-0000-0000-0000000000c1");

    /// <summary>Creates the tenant when it is missing; returns its id.</summary>
    public static async Task<Guid> EnsureAsync(EshopGuardDataSource dataSource, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        await using var command = dataSource.Source.CreateCommand("SELECT iam.ensure_cli_tenant()");
        return (Guid)(await command.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
    }

    /// <summary>True when the tenant exists (and is not deleted).</summary>
    public static async Task<bool> ExistsAsync(EshopGuardDataSource dataSource, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        await using var command = dataSource.Source.CreateCommand("SELECT EXISTS (SELECT 1 FROM iam.tenants WHERE id = $1 AND deleted_at IS NULL)");
        command.Parameters.Add(new NpgsqlParameter { Value = Id });
        return (bool)(await command.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
    }
}
