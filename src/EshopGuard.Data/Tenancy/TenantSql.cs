using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Data.Tenancy;

/// <summary>Tenant context for plain SQL and COPY outside EF.</summary>
public static class TenantSql
{
    /// <summary>Begins a transaction and sets <c>app.tenant_id</c> (and <c>app.user_id</c>) for it.</summary>
    public static async Task<NpgsqlTransaction> BeginAsync(NpgsqlConnection connection, Guid tenantId, Guid? userId = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Empty tenant id.", nameof(tenantId));
        }

        var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await using var command = CreateSetCommand(connection, transaction, tenantId, userId);
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>The tenant of a transaction of a user without a tenant: no tenant has it, so it sees no tenant row.</summary>
    public static readonly Guid NoTenant = Guid.Empty;

    /// <summary>
    /// Begins a transaction of a user without a tenant (<see cref="NoTenant"/>), or of an anonymous request (no user):
    /// tenant tables show only what the policies by user or by token allow.
    /// </summary>
    public static async Task<NpgsqlTransaction> BeginUserAsync(NpgsqlConnection connection, Guid? userId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await using var command = CreateSetCommand(connection, transaction, NoTenant, userId);
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// <c>SELECT set_config('app.tenant_id', $1, true), set_config('app.user_id', $2, true)</c>; the tenant is a <see cref="Guid"/>,
    /// so no text from outside can reach <c>set_config</c>.
    /// </summary>
    internal static NpgsqlCommand CreateSetCommand(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid? userId) =>
        new("SELECT set_config('app.tenant_id', $1, true), set_config('app.user_id', $2, true)", connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = tenantId.ToString("D") },
                new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = userId?.ToString("D") ?? string.Empty },
            },
        };
}
