using System.Text.Json;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Workers;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Jobs.Tests;

/// <summary>Handlers of the tests; none calls an external service.</summary>
internal static class TestHandlers
{
    public const string Record = "test.record";
    public const string Block = "test.block";
    public const string Fail = "test.fail";
    public const string FetchDomain = "test.fetch_domain";
    public const string Pause = "test.pause";
    public const string CancelAware = "test.cancel_aware";

    public static IServiceCollection AddTestHandlers(this IServiceCollection services) => services
        .AddJobHandler<RecordHandler>()
        .AddJobHandler<BlockHandler>()
        .AddJobHandler<FailHandler>()
        .AddJobHandler<FetchDomainHandler>()
        .AddJobHandler<PauseHandler>()
        .AddJobHandler<CancelAwareHandler>();

    /// <summary>Writes the effect row in the completion transaction (only an owner of the lease can commit it).</summary>
    public static async Task CompleteWithEffectAsync(JobExecutionContext context, TestState state, DateTimeOffset started, string? tag, CancellationToken ct, Func<JobTransaction, Task>? more = null)
    {
        var finished = DateTimeOffset.UtcNow;
        state.Intervals.Enqueue(new Interval(tag, context.Job.Id, context.WorkerId, started, finished));
        await context.CompleteAsync(async tx =>
        {
            await using var insert = new NpgsqlCommand(
                "INSERT INTO jobs_test.effects (job_id, attempt, worker_id, started_at, finished_at, tag) VALUES ($1, $2, $3, $4, $5, $6)",
                tx.Connection, tx.Transaction)
            {
                Parameters =
                {
                    new() { Value = context.Job.Id },
                    new() { Value = context.Job.Attempt },
                    new() { Value = context.WorkerId },
                    new() { Value = started },
                    new() { Value = finished },
                    new() { Value = (object?)tag ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text },
                },
            };
            await insert.ExecuteNonQueryAsync(ct);
            if (more is not null)
            {
                await more(tx);
            }
        }, ct);
    }

    public static string? Text(this JobExecutionContext context, string name) =>
        context.Job.Payload.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public static int Int(this JobExecutionContext context, string name, int fallback = 0) =>
        context.Job.Payload.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : fallback;

    public static int[] Ints(this JobExecutionContext context, string name) =>
        context.Job.Payload.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Select(v => v.GetInt32())]
            : [];
}

/// <summary><c>test.record</c>: optional sleep (<c>sleepMs</c>), then the effect row with <c>tag</c>.</summary>
internal sealed class RecordHandler(TestState state) : IJobHandler
{
    public string Kind => TestHandlers.Record;

    public JobResourceClass ResourceClass => JobResourceClass.Cpu;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow;
        state.NextExecution(context.Job.Id);
        if (context.Int("sleepMs") is > 0 and var sleep)
        {
            await Task.Delay(sleep, ct);
        }

        await TestHandlers.CompleteWithEffectAsync(context, state, started, context.Text("tag"), ct);
        return JobResult.Done;
    }
}

/// <summary>
/// <c>test.block</c>: in the attempts listed in <c>blockAttempts</c> (all when missing) it waits for <see cref="TestState.Gate"/>,
/// then writes the effect. A lost lease is recorded in <see cref="TestState.Errors"/>.
/// </summary>
internal sealed class BlockHandler(TestState state) : IJobHandler
{
    public string Kind => TestHandlers.Block;

    public JobResourceClass ResourceClass => JobResourceClass.Cpu;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow;
        state.NextExecution(context.Job.Id);
        var attempts = context.Ints("blockAttempts");
        if (attempts.Length == 0 || attempts.Contains(context.Job.Attempt))
        {
            state.Blocked.Enqueue((context.Job.Id, context.Job.Attempt, context.WorkerId));
            if (context.Int("ignoreCancellation") == 1)
            {
                await state.Gate.Task;
            }
            else
            {
                await state.Gate.Task.WaitAsync(ct);
            }
        }

        try
        {
            await TestHandlers.CompleteWithEffectAsync(context, state, started, context.Text("tag"), CancellationToken.None);
        }
        catch (LeaseLostException ex)
        {
            state.Errors.Enqueue(ex);
            throw;
        }

        return JobResult.Done;
    }
}

/// <summary>
/// <c>test.fail</c>: the first <c>failures</c> executions end with <c>mode</c> (<c>retry</c>, <c>fail</c>, <c>defer</c>,
/// <c>throw</c>) and code <c>code</c>; later executions write the effect.
/// </summary>
internal sealed class FailHandler(TestState state) : IJobHandler
{
    public string Kind => TestHandlers.Fail;

    public JobResourceClass ResourceClass => JobResourceClass.Cpu;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow;
        var execution = state.NextExecution(context.Job.Id);
        if (execution <= context.Int("failures", int.MaxValue))
        {
            var code = context.Text("code") ?? "test.failure";
            return context.Text("mode") switch
            {
                "fail" => new JobResult.Fail(code),
                "defer" => new JobResult.Defer(TimeSpan.Zero, code),
                "throw" => throw new InvalidOperationException("test failure with a text that must not reach last_error"),
                _ => new JobResult.Retry(code),
            };
        }

        await TestHandlers.CompleteWithEffectAsync(context, state, started, context.Text("tag"), ct);
        return JobResult.Done;
    }
}

/// <summary>
/// <c>test.fetch_domain</c>: takes the domain lease (busy → defer), "downloads" for 100 ms, releases the lease with a new
/// politeness state and, until <c>batches</c> is reached, enqueues the next batch with the domain as concurrency key.
/// </summary>
internal sealed class FetchDomainHandler(TestState state, IWorkerStore store) : IJobHandler
{
    public string Kind => TestHandlers.FetchDomain;

    public JobResourceClass ResourceClass => JobResourceClass.Fetch;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        var domain = context.Text("domain")!;
        var lease = await store.TryAcquireDomainAsync(domain, context.Job.Id, TimeSpan.FromSeconds(30), ct);
        if (lease is null)
        {
            return new JobResult.Defer(TimeSpan.FromMilliseconds(100), "domain.busy");
        }

        var started = DateTimeOffset.UtcNow;
        await Task.Delay(100, ct);
        var batch = context.Int("batch", 1);
        var batches = context.Int("batches", 1);
        await store.ReleaseDomainAsync(domain, context.Job.Id, lease.State with
        {
            LastRequestAt = DateTimeOffset.UtcNow,
            Rate = 2.5,
            CrawlDelayMs = 250,
            RobotsTxt = "User-agent: *\nAllow: /",
        }, ct);

        await TestHandlers.CompleteWithEffectAsync(context, state, started, context.Text("tag"), ct, async tx =>
        {
            if (batch < batches)
            {
                await tx.EnqueueAsync(TestJobs.Request(
                    TestHandlers.FetchDomain, JobResourceClass.Fetch, context.Job.Priority,
                    new { domain, batch = batch + 1, batches, tag = context.Text("tag") },
                    tenantId: context.Job.TenantId, runId: context.Job.RunId, shopId: context.Job.ShopId,
                    concurrencyKey: JobKeys.Domain(domain)), ct);
            }
        });
        return JobResult.Done;
    }
}

/// <summary><c>test.pause</c>: the first execution pauses the class (<c>jev.credit_exhausted</c>), later ones write the effect.</summary>
internal sealed class PauseHandler(TestState state) : IJobHandler
{
    public string Kind => TestHandlers.Pause;

    public JobResourceClass ResourceClass => JobResourceClass.Jev;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow;
        if (state.NextExecution(context.Job.Id) == 1 && context.Int("pause", 1) == 1)
        {
            return new JobResult.PauseClass("jev.credit_exhausted");
        }

        await TestHandlers.CompleteWithEffectAsync(context, state, started, context.Text("tag"), ct);
        return JobResult.Done;
    }
}

/// <summary><c>test.cancel_aware</c>: works in partial batches of 50 ms and stops when its run is canceled.</summary>
internal sealed class CancelAwareHandler(TestState state) : IJobHandler
{
    public string Kind => TestHandlers.CancelAware;

    public JobResourceClass ResourceClass => JobResourceClass.Cpu;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        state.NextExecution(context.Job.Id);
        state.Blocked.Enqueue((context.Job.Id, context.Job.Attempt, context.WorkerId));
        while (!await context.IsRunCancellationRequestedAsync(ct))
        {
            await Task.Delay(50, ct);
        }

        return new JobResult.Canceled();
    }
}
