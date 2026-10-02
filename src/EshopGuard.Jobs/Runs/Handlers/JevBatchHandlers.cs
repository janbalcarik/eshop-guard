using System.Text.Json.Nodes;
using EshopGuard.Core.Models;
using EshopGuard.Core.Pipeline;
using EshopGuard.Core.Rules;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Entities.Usage;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Runs.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EshopGuard.Jobs.Runs.Handlers;

/// <summary>
/// <c>run.sieve</c>: one batch of chunks of the sieve (<c>Runs:SieveBatchChunks</c>). Answers go to the cache as they arrive
/// (a repeated batch pays nothing twice) and the usage with them; chunks Jev did not answer for a while send the batch back
/// to the queue (<see cref="StepErrorPolicy"/>); the batch that finishes last plans the evaluation.
/// </summary>
internal sealed class SieveBatchHandler(RunHandlerContext context, IRuleSetProvider ruleSets, SieveStep sieve) : RunJobHandler(context)
{
    public static string ResultFile(int batch) => $"sieve-{batch}.result";

    public override string Kind => RunJobKinds.Sieve;

    public override JobResourceClass ResourceClass => JobResourceClass.Jev;

    protected override IReadOnlyCollection<RunStatus> Statuses { get; } = [RunStatus.Evaluating];

    protected override async Task<JobResult> RunAsync(RunJob job, CancellationToken ct)
    {
        var run = job.Run;
        var batch = job.Job.Payload.RootElement.GetProperty("batch").GetInt32();
        var rules = await RunRules.LoadAsync(ruleSets, run, Ctx.Runs, ct).ConfigureAwait(false);
        var input = await RunFiles.RequireAsync<SieveBatchInput>(Ctx.Blobs, job.Scope, RunFiles.Work, SegmentHandler.SieveFile(batch), ct).ConfigureAwait(false);
        job.Scope.JevOperation = UsageOperation.Sieve;
        var result = await sieve.SieveAsync(input, rules.Sieve ?? rules.Catalog.Sieve!, null, ct).ConfigureAwait(false);
        if (StepErrorPolicy.RepeatBatch(result.TransientErrors, job.Job))
        {
            // The answers received are in the cache: the repeated batch asks only for the chunks without one.
            throw new TransientStepException(StepErrorPolicy.JevUnavailable);
        }

        await RunFiles.PutAsync(Ctx.Blobs, job.Scope, RunFiles.Work, ResultFile(batch), result, ct).ConfigureAwait(false);
        await CompleteAsync(job, async (tx, locked) =>
        {
            await Ctx.Usage.WriteAsync(tx.Connection, tx.Transaction, job.Scope,
                [new UsageEntry(UsageProvider.Jev, UsageOperation.Sieve, null, 0, result.CacheHits, 0, 0, 0, 0, 0m)], ct).ConfigureAwait(false);
            if (await JevBarrier.AdvanceAsync(tx.Connection, tx.Transaction, locked, "sieve", ct).ConfigureAwait(false))
            {
                await tx.EnqueueAsync(RunPlan.PlanEvaluate(locked), ct).ConfigureAwait(false);
            }
        }, ct).ConfigureAwait(false);
        Logger.LogInformation("run.sieve_batch {RunId} {TenantId} {JobId} {Batch} {Calls} {CacheHits} {Errors}", run.Id, run.TenantId, job.Job.Id, batch, result.Calls, result.CacheHits, result.Errors);
        return JobResult.Done;
    }
}

/// <summary>
/// <c>run.plan_evaluate</c>: which modules each segment gets after the sieve (the same rule as the CLI's
/// <c>SieveStep.States</c>), the segments with the modules the sieve left out, and the batches of the detailed questions
/// (<c>Runs:EvaluateBatchSegments</c> segments each).
/// </summary>
internal sealed class PlanEvaluateHandler(RunHandlerContext context, IRuleSetProvider ruleSets) : RunJobHandler(context)
{
    public const string SievedSegmentsFile = "segments-sieved";
    public const string SieveSummaryFile = "sieve";

    public static string EvaluateFile(int batch) => $"evaluate-{batch}";

    public override string Kind => RunJobKinds.PlanEvaluate;

    public override JobResourceClass ResourceClass => JobResourceClass.Cpu;

    protected override IReadOnlyCollection<RunStatus> Statuses { get; } = [RunStatus.Evaluating];

    protected override async Task<JobResult> RunAsync(RunJob job, CancellationToken ct)
    {
        var run = job.Run;
        var rules = await RunRules.LoadAsync(ruleSets, run, Ctx.Runs, ct).ConfigureAwait(false);
        var segmented = await RunFiles.RequireAsync<SegmentResult>(Ctx.Blobs, job.Scope, RunFiles.Work, SegmentHandler.SegmentsFile, ct).ConfigureAwait(false);
        var sieveBatches = (int)RunStore.Long(RunStore.Section(run.Progress, "steps", "sieve"), "total");
        var results = new List<SieveBatchResult>();
        for (var i = 1; i <= sieveBatches; i++)
        {
            results.Add(await RunFiles.RequireAsync<SieveBatchResult>(Ctx.Blobs, job.Scope, RunFiles.Work, SieveBatchHandler.ResultFile(i), ct).ConfigureAwait(false));
        }

        SieveBatchResult? sieved = rules.Sieve is null ? null : new SieveBatchResult(
            results.SelectMany(r => r.Chunks).ToList(), results.Sum(r => r.Calls), results.Sum(r => r.CacheHits), results.Sum(r => r.Errors),
            results.Sum(r => r.TooLong), results.Sum(r => r.InputTokens));
        var states = SieveStep.States(segmented.Segments, rules.RuleSets, rules.Sieve, rules.SieveModules, sieved?.Chunks);
        await RunFiles.PutAsync(Ctx.Blobs, job.Scope, RunFiles.Work, SievedSegmentsFile, segmented, ct).ConfigureAwait(false);
        if (sieved is not null)
        {
            await RunFiles.PutAsync(Ctx.Blobs, job.Scope, RunFiles.Work, SieveSummaryFile, sieved, ct).ConfigureAwait(false);
        }

        var batches = states.Where(s => s.Modules.Count > 0).Chunk(Ctx.Runs.EvaluateBatchSegments).ToList();
        for (var i = 0; i < batches.Count; i++)
        {
            await RunFiles.PutAsync(Ctx.Blobs, job.Scope, RunFiles.Work, EvaluateFile(i + 1), new EvaluateBatchInput(batches[i], Ctx.Runs.QuestionLanguage, null), ct).ConfigureAwait(false);
        }

        await CompleteAsync(job, async (tx, locked) =>
        {
            RunStore.Section(locked.Progress, "steps")["evaluate"] = new JsonObject { ["done"] = 0, ["total"] = batches.Count };
            await RunStore.SetJsonAsync(tx.Connection, tx.Transaction, run.Id, "progress", locked.Progress, ct).ConfigureAwait(false);
            if (batches.Count == 0)
            {
                if (await RunStateMachine.TryTransitionAsync(tx.Connection, tx.Transaction, run.TenantId, run.Id, RunStatus.Evaluating, RunStatus.Ruling, ct).ConfigureAwait(false))
                {
                    await tx.EnqueueAsync(RunPlan.Rules(locked), ct).ConfigureAwait(false);
                }

                return;
            }

            for (var i = 0; i < batches.Count; i++)
            {
                await tx.EnqueueAsync(RunPlan.Evaluate(locked, i + 1), ct).ConfigureAwait(false);
            }
        }, ct).ConfigureAwait(false);
        Logger.LogInformation("run.evaluation_planned {RunId} {TenantId} {JobId} {Segments} {Batches}", run.Id, run.TenantId, job.Job.Id, states.Count(s => s.Modules.Count > 0), batches.Count);
        return JobResult.Done;
    }
}

/// <summary>
/// <c>run.evaluate</c>: one batch of segments with the detailed questions of their modules (<c>EvaluateStep</c>, one Jev
/// request per segment through the limiter of the worker). Answers go to the cache as they arrive; segments Jev did not answer
/// for a while send the batch back to the queue, and after its last attempt they stay without an answer and are reported
/// (<see cref="StepErrorPolicy"/>). The batch that finishes last starts the rules.
/// </summary>
internal sealed class EvaluateBatchHandler(RunHandlerContext context, IRuleSetProvider ruleSets, EvaluateStep evaluate) : RunJobHandler(context)
{
    public static string ResultFile(int batch) => $"evaluate-{batch}.result";

    public override string Kind => RunJobKinds.Evaluate;

    public override JobResourceClass ResourceClass => JobResourceClass.Jev;

    protected override IReadOnlyCollection<RunStatus> Statuses { get; } = [RunStatus.Evaluating];

    protected override async Task<JobResult> RunAsync(RunJob job, CancellationToken ct)
    {
        var run = job.Run;
        var batch = job.Job.Payload.RootElement.GetProperty("batch").GetInt32();
        var rules = await RunRules.LoadAsync(ruleSets, run, Ctx.Runs, ct).ConfigureAwait(false);
        var input = await RunFiles.RequireAsync<EvaluateBatchInput>(Ctx.Blobs, job.Scope, RunFiles.Work, PlanEvaluateHandler.EvaluateFile(batch), ct).ConfigureAwait(false);
        job.Scope.JevOperation = UsageOperation.SentenceEval;
        var result = await evaluate.EvaluateAsync(input, rules.RuleSets, null, ct).ConfigureAwait(false);
        if (StepErrorPolicy.RepeatBatch(result.TransientErrors, job.Job))
        {
            // The answers received are in the cache: the repeated batch asks only for the segments without one.
            throw new TransientStepException(StepErrorPolicy.JevUnavailable);
        }

        await RunFiles.PutAsync(Ctx.Blobs, job.Scope, RunFiles.Work, ResultFile(batch), result, ct).ConfigureAwait(false);
        await CompleteAsync(job, async (tx, locked) =>
        {
            await Ctx.Usage.WriteAsync(tx.Connection, tx.Transaction, job.Scope,
                [new UsageEntry(UsageProvider.Jev, UsageOperation.SentenceEval, result.Model, 0, result.CacheHits, 0, 0, 0, 0, 0m)], ct).ConfigureAwait(false);
            if (await JevBarrier.AdvanceAsync(tx.Connection, tx.Transaction, locked, "evaluate", ct).ConfigureAwait(false)
                && await RunStateMachine.TryTransitionAsync(tx.Connection, tx.Transaction, run.TenantId, run.Id, RunStatus.Evaluating, RunStatus.Ruling, ct).ConfigureAwait(false))
            {
                await tx.EnqueueAsync(RunPlan.Rules(locked), ct).ConfigureAwait(false);
            }
        }, ct).ConfigureAwait(false);
        Logger.LogInformation("run.evaluate_batch {RunId} {TenantId} {JobId} {Batch} {Calls} {CacheHits} {Errors}", run.Id, run.TenantId, job.Job.Id, batch, result.Calls, result.CacheHits, result.Errors);
        return JobResult.Done;
    }
}

/// <summary>
/// The barrier of the batch steps (<c>RunBarrier</c> of the design): in the completion transaction, with the run row locked,
/// the batch adds itself to <c>progress.steps.{step}.done</c>; the batch that reaches the total starts the next step. Batches
/// finishing at the same time wait for the lock, so the next step is started exactly once.
/// </summary>
internal static class JevBarrier
{
    /// <summary>Counts the batch in; true for the batch that completes the step. <paramref name="locked"/> must be locked FOR UPDATE in the transaction.</summary>
    public static async Task<bool> AdvanceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, RunRow locked, string step, CancellationToken ct)
    {
        var counters = RunStore.Section(locked.Progress, "steps", step);
        var done = RunStore.Long(counters, "done") + 1;
        var total = RunStore.Long(counters, "total");
        counters["done"] = done;
        await RunStore.SetJsonAsync(connection, transaction, locked.Id, "progress", locked.Progress, ct).ConfigureAwait(false);
        await RunEventWriter.WriteAsync(connection, transaction, locked.TenantId, locked.Id, "info", RunCodes.EventProgress,
            new JsonObject { ["step"] = step, ["done"] = done, ["total"] = total }, ct).ConfigureAwait(false);
        return done == total;
    }
}
