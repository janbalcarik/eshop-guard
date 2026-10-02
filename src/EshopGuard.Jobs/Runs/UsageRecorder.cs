using System.Collections.Concurrent;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Options;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Entities.Usage;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Jobs.Runs;

/// <summary>One line of usage: what was paid (or taken from the cache) for one provider, operation and model.</summary>
internal sealed record UsageEntry(
    UsageProvider Provider,
    UsageOperation Operation,
    string? Model,
    int Calls,
    int CacheHits,
    long InputTokens,
    long CachedTokens,
    long OutputTokens,
    long BytesIn,
    decimal CostUsd);

/// <summary>Usage of paid calls collected in a job and not written yet (thread-safe: Jev answers arrive in parallel).</summary>
internal sealed class UsageBuffer
{
    private readonly ConcurrentQueue<UsageEntry> _pending = new();
    private long _firstAtTicks;
    private int _calls;

    public int PendingCalls => Volatile.Read(ref _calls);

    public DateTimeOffset? FirstPendingAt
    {
        get
        {
            var ticks = Interlocked.Read(ref _firstAtTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    public void Add(UsageEntry entry, DateTimeOffset now)
    {
        _pending.Enqueue(entry);
        Interlocked.Add(ref _calls, entry.Calls);
        Interlocked.CompareExchange(ref _firstAtTicks, now.UtcTicks, 0);
    }

    /// <summary>Everything pending, summed by provider, operation and model; the buffer is empty afterwards.</summary>
    public List<UsageEntry> TakeAll()
    {
        var taken = new List<UsageEntry>();
        while (_pending.TryDequeue(out var entry))
        {
            taken.Add(entry);
        }

        Interlocked.Exchange(ref _calls, 0);
        Interlocked.Exchange(ref _firstAtTicks, 0);
        return Sum(taken);
    }

    public static List<UsageEntry> Sum(IEnumerable<UsageEntry> entries) =>
        entries.GroupBy(e => (e.Provider, e.Operation, e.Model))
            .Select(g => new UsageEntry(g.Key.Provider, g.Key.Operation, g.Key.Model,
                g.Sum(e => e.Calls), g.Sum(e => e.CacheHits), g.Sum(e => e.InputTokens), g.Sum(e => e.CachedTokens),
                g.Sum(e => e.OutputTokens), g.Sum(e => e.BytesIn), g.Sum(e => e.CostUsd)))
            .ToList();
}

/// <summary>
/// Writes the internal usage of runs into <c>usage.usage_records</c> (customers never see it; the role <c>eshopguard_app</c>
/// cannot read it). Jev calls are collected as they are answered (<see cref="UsageRecordingJevClient"/>) and written every
/// <c>Runs:UsageFlushEvery</c> calls or <c>Runs:UsageFlushSeconds</c> and at the end of the job, each time in its own
/// transaction: a paid call is recorded even when its batch later fails, and a crash loses at most the calls since the last
/// write (their answers are in the cache, so the repeated batch does not pay them again). Usage of OpenAI and of the crawl is
/// written with the result of its step (<see cref="WriteAsync(NpgsqlConnection, NpgsqlTransaction?, RunAmbientScope, IReadOnlyList{UsageEntry}, CancellationToken)"/>).
/// </summary>
public sealed class UsageRecorder(EshopGuardDataSource dataSource, IOptions<RunsOptions> runs, IOptions<EshopGuardOptions> guard, TimeProvider time)
{
    private RunsOptions Runs => runs.Value;

    /// <summary>Price of Jev input tokens (as <c>SegmentEvaluator.Cost</c>).</summary>
    public decimal JevCost(long inputTokens) => inputTokens * guard.Value.Cost.UsdPerMillionInputTokens / 1_000_000m;

    /// <summary>One answered Jev call of the current job; written when enough have gathered.</summary>
    internal async Task RecordJevCallAsync(JevResult result, CancellationToken ct)
    {
        if (RunAmbient.Current is not { } scope)
        {
            return;
        }

        var tokens = result.Usage.InputTokens;
        scope.Usage.Add(new UsageEntry(UsageProvider.Jev, scope.JevOperation, result.Model, 1, 0, tokens, 0, result.Usage.OutputTokens, 0, JevCost(tokens)), time.GetUtcNow());
        if (scope.Usage.PendingCalls >= Runs.UsageFlushEvery
            || (scope.Usage.FirstPendingAt is { } first && time.GetUtcNow() - first >= TimeSpan.FromSeconds(Runs.UsageFlushSeconds)))
        {
            await FlushAsync(scope, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Writes what the job collected (also at its end and after a failure).</summary>
    public async Task FlushAsync(RunAmbientScope scope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var entries = scope.Usage.TakeAll();
        if (entries.Count == 0)
        {
            return;
        }

        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await WriteAsync(connection, null, scope, entries, ct).ConfigureAwait(false);
    }

    /// <summary>Writes usage lines in the given transaction (the completion of a batch) or on their own.</summary>
    internal async Task WriteAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, RunAmbientScope scope, IReadOnlyList<UsageEntry> entries, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        foreach (var entry in UsageBuffer.Sum(entries).Where(e => e.Calls > 0 || e.CacheHits > 0 || e.BytesIn > 0))
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO usage.usage_records (tenant_id, shop_id, run_id, job_id, provider, model, operation, calls, cache_hits,
                    input_tokens, cached_tokens, output_tokens, bytes_in, cost_usd, occurred_at, created_at)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $15)
                """, connection, transaction)
            {
                Parameters =
                {
                    new NpgsqlParameter { Value = scope.TenantId },
                    new NpgsqlParameter { Value = scope.ShopId },
                    // A job outside of a run (the recheck of a fix, change 11) has no run.
                    new NpgsqlParameter { Value = scope.RunId == Guid.Empty ? DBNull.Value : scope.RunId, NpgsqlDbType = NpgsqlDbType.Uuid },
                    new NpgsqlParameter { Value = scope.JobId },
                    new NpgsqlParameter { Value = SnakeCaseEnumConverter<UsageProvider>.ToText(entry.Provider) },
                    new NpgsqlParameter { Value = (object?)entry.Model ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text },
                    new NpgsqlParameter { Value = SnakeCaseEnumConverter<UsageOperation>.ToText(entry.Operation) },
                    new NpgsqlParameter { Value = entry.Calls },
                    new NpgsqlParameter { Value = entry.CacheHits },
                    new NpgsqlParameter { Value = entry.InputTokens },
                    new NpgsqlParameter { Value = entry.CachedTokens },
                    new NpgsqlParameter { Value = entry.OutputTokens },
                    new NpgsqlParameter { Value = entry.BytesIn },
                    new NpgsqlParameter { Value = Math.Round(entry.CostUsd, 6) },
                    new NpgsqlParameter { Value = now },
                },
            };
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// The Jev client of the worker: the client of the library, with every answered call counted for the usage of the run
/// (design of change 8, "Spotřeba"). The calls themselves, their retries and limits are those of the inner client.
/// </summary>
internal sealed class UsageRecordingJevClient(IJevClient inner, UsageRecorder recorder) : IJevClient
{
    public async Task<JevResult> EvaluateAsync(object state, IReadOnlyDictionary<string, JevQuestion> questions, CancellationToken ct)
    {
        var result = await inner.EvaluateAsync(state, questions, ct).ConfigureAwait(false);
        await recorder.RecordJevCallAsync(result, ct).ConfigureAwait(false);
        return result;
    }
}
