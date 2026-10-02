using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace EshopGuard.Data.Tenancy;

/// <summary>
/// Sets <c>app.tenant_id</c> and <c>app.user_id</c> for the transaction (<c>set_config(…, true)</c> = <c>SET LOCAL</c>) whenever
/// EF starts or adopts a transaction. The values are parameters, never SQL text; they vanish with the transaction,
/// so they never leak to the next user of a pooled connection.
/// </summary>
public sealed class TenantTransactionInterceptor : DbTransactionInterceptor
{
    /// <inheritdoc />
    public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        Apply(connection, eventData, result);
        return result;
    }

    /// <inheritdoc />
    public override async ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
    {
        await ApplyAsync(connection, eventData, result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public override DbTransaction TransactionUsed(DbConnection connection, TransactionEventData eventData, DbTransaction result)
    {
        Apply(connection, eventData, result);
        return result;
    }

    /// <inheritdoc />
    public override async ValueTask<DbTransaction> TransactionUsedAsync(DbConnection connection, TransactionEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
    {
        await ApplyAsync(connection, eventData, result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static void Apply(DbConnection connection, DbContextEventData eventData, DbTransaction transaction)
    {
        if (CreateCommand(connection, eventData, transaction) is { } command)
        {
            using (command)
            {
                command.ExecuteNonQuery();
            }
        }
    }

    private static async Task ApplyAsync(DbConnection connection, DbContextEventData eventData, DbTransaction transaction, CancellationToken ct)
    {
        if (CreateCommand(connection, eventData, transaction) is { } command)
        {
            await using (command.ConfigureAwait(false))
            {
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }
    }

    private static DbCommand? CreateCommand(DbConnection connection, DbContextEventData eventData, DbTransaction transaction)
    {
        if (eventData.Context is not EshopGuardDb db)
        {
            return null;
        }

        var tenant = db.TenantContext;
        if (tenant.TenantId is { } tenantId)
        {
            return TenantSql.CreateSetCommand((NpgsqlConnection)connection, (NpgsqlTransaction)transaction, tenantId, tenant.UserId);
        }

        return tenant.UserScope
            ? TenantSql.CreateSetCommand((NpgsqlConnection)connection, (NpgsqlTransaction)transaction, TenantSql.NoTenant, tenant.UserId)
            : null;
    }
}
