using EshopGuard.Data.Connections;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Application.RateLimits;

/// <summary>Outcome of taking from a bucket: granted, or the time until enough tokens come back.</summary>
public readonly record struct BucketResult(bool Granted, TimeSpan RetryAfter);

/// <summary>
/// Token buckets in <c>ops.rate_limit_buckets</c> shared by all instances of the API (AD 5). A bucket is created full on
/// first use; taking is one conditional <c>UPDATE</c>, so two instances never take the last token twice. The time comes from
/// <see cref="TimeProvider"/> (tests move it).
/// </summary>
public interface IRateLimitBuckets
{
    Task<BucketResult> TryTakeAsync(string key, double cost, double capacity, double refillPerSecond, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class PgRateLimitBuckets(EshopGuardDataSource dataSource, TimeProvider time) : IRateLimitBuckets
{
    private const string Available = "least(b.capacity, b.tokens + greatest(0, extract(epoch FROM @now - b.updated_at)) * b.refill_per_sec)";

    private const string Ensure = """
        INSERT INTO ops.rate_limit_buckets AS b (key, capacity, tokens, refill_per_sec, created_at, updated_at)
        VALUES (@key, @capacity, @capacity, @refill, @now, @now)
        ON CONFLICT (key) DO UPDATE SET capacity = excluded.capacity, refill_per_sec = excluded.refill_per_sec
        WHERE b.capacity <> excluded.capacity OR b.refill_per_sec <> excluded.refill_per_sec
        """;

    private const string Take = $"""
        UPDATE ops.rate_limit_buckets b SET tokens = {Available} - @cost, updated_at = greatest(b.updated_at, @now)
        WHERE b.key = @key AND {Available} >= @cost
        RETURNING b.tokens
        """;

    private const string State = $"SELECT {Available}, b.refill_per_sec FROM ops.rate_limit_buckets b WHERE b.key = @key";

    public async Task<BucketResult> TryTakeAsync(string key, double cost, double capacity, double refillPerSecond, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var now = time.GetUtcNow();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (var ensure = Command(connection, Ensure, key, now, ("capacity", capacity), ("refill", refillPerSecond)))
        {
            await ensure.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await using (var take = Command(connection, Take, key, now, ("cost", cost)))
        {
            if (await take.ExecuteScalarAsync(ct).ConfigureAwait(false) is not null)
            {
                return new BucketResult(true, TimeSpan.Zero);
            }
        }

        await using var state = Command(connection, State, key, now);
        await using var reader = await state.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return new BucketResult(false, TimeSpan.FromSeconds(1));
        }

        var available = reader.GetDouble(0);
        var refill = reader.GetDouble(1);
        var seconds = refill <= 0 ? double.MaxValue : Math.Max(1, Math.Ceiling((cost - available) / refill));
        return new BucketResult(false, seconds >= TimeSpan.MaxValue.TotalSeconds ? TimeSpan.MaxValue : TimeSpan.FromSeconds(seconds));
    }

    private static NpgsqlCommand Command(NpgsqlConnection connection, string sql, string key, DateTimeOffset now, params (string Name, double Value)[] numbers)
    {
        var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter("key", NpgsqlDbType.Text) { Value = key });
        command.Parameters.Add(new NpgsqlParameter("now", NpgsqlDbType.TimestampTz) { Value = now.UtcDateTime });
        foreach (var (name, value) in numbers)
        {
            command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Double) { Value = value });
        }

        return command;
    }
}
