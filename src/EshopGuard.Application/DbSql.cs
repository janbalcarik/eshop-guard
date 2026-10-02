using EshopGuard.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Application;

/// <summary>Plain SQL on the connection and in the open transaction of an <see cref="EshopGuardDb"/> (atomic updates of change 9).</summary>
internal static class DbSql
{
    public static NpgsqlTransaction Transaction(EshopGuardDb db) =>
        db.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction
        ?? throw new InvalidOperationException("Plain SQL of the application runs only in an open transaction.");

    public static NpgsqlCommand Command(EshopGuardDb db, string sql, params NpgsqlParameter[] parameters)
    {
        var transaction = Transaction(db);
        var command = new NpgsqlCommand(sql, transaction.Connection, transaction);
        command.Parameters.AddRange(parameters);
        return command;
    }

    public static async Task<int> ExecuteAsync(EshopGuardDb db, string sql, CancellationToken ct, params NpgsqlParameter[] parameters)
    {
        await using var command = Command(db, sql, parameters);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public static async Task<T?> ScalarAsync<T>(EshopGuardDb db, string sql, CancellationToken ct, params NpgsqlParameter[] parameters)
    {
        await using var command = Command(db, sql, parameters);
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is null or DBNull ? default : (T)value;
    }

    public static NpgsqlParameter P(string name, Guid? value) => new(name, NpgsqlDbType.Uuid) { Value = (object?)value ?? DBNull.Value };

    public static NpgsqlParameter P(string name, string? value) => new(name, NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };

    public static NpgsqlParameter P(string name, DateTimeOffset? value) =>
        new(name, NpgsqlDbType.TimestampTz) { Value = value is { } v ? v.UtcDateTime : DBNull.Value };

    public static NpgsqlParameter P(string name, byte[]? value) => new(name, NpgsqlDbType.Bytea) { Value = (object?)value ?? DBNull.Value };

    public static NpgsqlParameter P(string name, int value) => new(name, NpgsqlDbType.Integer) { Value = value };

    public static NpgsqlParameter Json(string name, string? value) => new(name, NpgsqlDbType.Jsonb) { Value = (object?)value ?? DBNull.Value };
}
