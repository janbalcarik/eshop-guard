using System.Collections.Concurrent;
using System.Text.Json;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Queue;

namespace EshopGuard.Jobs.Tests;

/// <summary>State shared by the test handlers of all workers in one test.</summary>
internal sealed class TestState
{
    /// <summary>Gate that <c>test.block</c> waits for (opened at the end of every test).</summary>
    public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Workers that started a blocked attempt (job, attempt, worker).</summary>
    public ConcurrentQueue<(long JobId, int Attempt, string WorkerId)> Blocked { get; } = new();

    /// <summary>Exceptions seen by handlers (e.g. <see cref="LeaseLostException"/>).</summary>
    public ConcurrentQueue<Exception> Errors { get; } = new();

    /// <summary>Executions per job (also those that did not count an attempt).</summary>
    public ConcurrentDictionary<long, int> Executions { get; } = new();

    /// <summary>Execution intervals by tag (for overlap and order checks).</summary>
    public ConcurrentQueue<Interval> Intervals { get; } = new();

    public int NextExecution(long jobId) => Executions.AddOrUpdate(jobId, 1, (_, n) => n + 1);
}

/// <summary>When a handler ran, for which tag and on which worker.</summary>
internal sealed record Interval(string? Tag, long JobId, string WorkerId, DateTimeOffset Start, DateTimeOffset End);

/// <summary>Requests of the test kinds.</summary>
internal static class TestJobs
{
    public static JobRequest Request(
        string kind,
        JobResourceClass resourceClass = JobResourceClass.Cpu,
        JobPriority priority = JobPriority.P2,
        object? payload = null,
        Guid? tenantId = null,
        Guid? runId = null,
        Guid? shopId = null,
        string? dedupeKey = null,
        string? concurrencyKey = null,
        int? maxAttempts = null,
        DateTimeOffset? notBefore = null) =>
        new(kind, resourceClass, priority, JsonDocument.Parse(JsonSerializer.Serialize(payload ?? new { })),
            tenantId, shopId, runId, dedupeKey, concurrencyKey, maxAttempts, notBefore);
}
