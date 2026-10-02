using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Billing;

/// <summary>Plain SQL of billing with positional parameters (<c>$1</c>, <c>$2</c> …) in an open transaction.</summary>
internal static class BillingSql
{
    /// <summary>
    /// A transaction for global rows (price lists) and their audit without a tenant: <c>app.tenant_id</c> is the empty tenant, so no
    /// row of a tenant is visible and the policy of the audit of price lists applies.
    /// </summary>
    public static Task<NpgsqlTransaction> BeginGlobalAsync(NpgsqlConnection connection, Guid? actor, CancellationToken ct) =>
        EshopGuard.Data.Tenancy.TenantSql.BeginUserAsync(connection, actor, ct);

    public static NpgsqlCommand Command(NpgsqlTransaction transaction, string sql, params object?[] parameters)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var command = new NpgsqlCommand(sql, transaction.Connection, transaction);
        foreach (var value in parameters)
        {
            command.Parameters.Add(Parameter(value));
        }

        return command;
    }

    public static async Task<int> ExecuteAsync(NpgsqlTransaction transaction, string sql, CancellationToken ct, params object?[] parameters)
    {
        await using var command = Command(transaction, sql, parameters);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public static async Task<T?> ScalarAsync<T>(NpgsqlTransaction transaction, string sql, CancellationToken ct, params object?[] parameters)
    {
        await using var command = Command(transaction, sql, parameters);
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is null or DBNull ? default : (T)value;
    }

    /// <summary>Reads rows with <paramref name="map"/>.</summary>
    public static async Task<List<T>> ListAsync<T>(NpgsqlTransaction transaction, string sql, Func<NpgsqlDataReader, T> map, CancellationToken ct, params object?[] parameters)
    {
        await using var command = Command(transaction, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var rows = new List<T>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    /// <summary>A jsonb parameter.</summary>
    public static NpgsqlParameter Json(JsonNode? value) => new() { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = value?.ToJsonString() ?? (object)DBNull.Value };

    /// <summary>A text parameter that may be null.</summary>
    public static NpgsqlParameter Text(string? value) => new() { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)value ?? DBNull.Value };

    public static T? Get<T>(this NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? default : reader.GetFieldValue<T>(ordinal);

    /// <summary>
    /// A row of <c>ops.audit_log</c> in the transaction: codes and ids only. A tenant row in the transaction of the tenant, a global
    /// change of a price list (<c>price_list.*</c>) by the worker without a tenant (policy <c>audit_log_insert_price_list</c>).
    /// </summary>
    public static Task AuditAsync(
        NpgsqlTransaction transaction, Guid? tenantId, Guid? actorUserId, string actorKind, string action, string entityType, string entityId, JsonObject? data,
        DateTimeOffset at, CancellationToken ct) => ExecuteAsync(transaction,
        """
        INSERT INTO ops.audit_log (at, tenant_id, actor_user_id, actor_kind, action, entity_type, entity_id, data, created_at)
        VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $1)
        """, ct, at, tenantId, actorUserId, actorKind, action, entityType, entityId, Json(data));

    /// <summary>Values of <c>actor_kind</c>.</summary>
    public const string User = "user";
    public const string System = "system";
    public const string Admin = "admin";

    private static NpgsqlParameter Parameter(object? value) => value switch
    {
        NpgsqlParameter parameter => parameter,
        null => new NpgsqlParameter { Value = DBNull.Value },
        DateTimeOffset at => new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.TimestampTz, Value = at.UtcDateTime },
        Guid id => new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = id },
        _ => new NpgsqlParameter { Value = value },
    };
}
