using System.Text.Json.Nodes;
using EshopGuard.Core.Pipeline;
using EshopGuard.Core.Profiles;
using EshopGuard.Core.Rules;
using EshopGuard.Core.Storage;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Runs.Storage;
using Microsoft.Extensions.Logging;

namespace EshopGuard.Jobs.Runs.Handlers;

/// <summary>
/// <c>run.segment</c> (with the estimate of the design's <c>run.estimate</c>): sentences and legal paragraphs of all pages,
/// merged into unique segments, the chunks of the sieve; the versions of the pages with the fingerprints of their sentences
/// (<c>content.page_versions</c>); the internal estimate of Jev as the upper bound of <c>EstimateStep</c>, stored before the
/// first call and checked against the cap of a free sample; the batches of the sieve (or the plan of the evaluation when
/// there is no sieve).
/// </summary>
internal sealed class SegmentHandler(
    RunHandlerContext context, IRuleSetProvider ruleSets, SegmentStep segments, EstimateStep estimates, IPageContentStore contents)
    : RunJobHandler(context)
{
    public const string SegmentsFile = "segments";
    public const string EstimateFile = "estimate";

    public static string SieveFile(int batch) => $"sieve-{batch}";

    public override string Kind => RunJobKinds.Segment;

    public override JobResourceClass ResourceClass => JobResourceClass.Cpu;

    protected override IReadOnlyCollection<RunStatus> Statuses { get; } = [RunStatus.Segmenting];

    protected override async Task<JobResult> RunAsync(RunJob job, CancellationToken ct)
    {
        var run = job.Run;
        var rules = await RunRules.LoadAsync(ruleSets, run, Ctx.Runs, ct).ConfigureAwait(false);
        var urls = await InTenantAsync(run.TenantId, (c, t) => RunPages.LoadUrlsAsync(c, t, run.Id, RunPages.PageStates, ct), ct).ConfigureAwait(false);
        var pages = await RunPages.LoadPagesAsync(contents, urls, ct).ConfigureAwait(false);
        var records = pages.Select(p => p.Record).ToList();
        var segmented = segments.Segment(new SegmentInput(records, rules.Sieve?.MaxChunkChars));
        var plan = await RunFiles.GetAsync<ProfilePlan>(Ctx.Blobs, job.Scope, RunFiles.Work, ProfileHandler.PlanFile, ct).ConfigureAwait(false)
            ?? ProfilePlan.Off(new SiteScope(new Uri(run.ShopBaseUrl)).SiteKey);
        var estimate = await estimates.EstimateAsync(new EstimateInput(segmented, rules.SieveModules, Ctx.Runs.QuestionLanguage, plan with { Planned = [] }), rules.RuleSets, rules.Sieve, ct).ConfigureAwait(false);
        await RunFiles.PutAsync(Ctx.Blobs, job.Scope, RunFiles.Work, SegmentsFile, segmented, ct).ConfigureAwait(false);
        await RunFiles.PutAsync(Ctx.Blobs, job.Scope, RunFiles.Work, EstimateFile, estimate, ct).ConfigureAwait(false);

        var batches = rules.Sieve is null ? [] : segmented.SieveChunks.Chunk(Ctx.Runs.SieveBatchChunks).ToList();
        for (var i = 0; i < batches.Count; i++)
        {
            await RunFiles.PutAsync(Ctx.Blobs, job.Scope, RunFiles.Work, SieveFile(i + 1),
                new SieveBatchInput(batches[i], rules.SieveModules, Ctx.Runs.QuestionLanguage, null), ct).ConfigureAwait(false);
        }

        var jev = estimate.Estimate;
        var fingerprints = segmented.Pages.ToDictionary(p => p.Url, p => p.Fingerprints, StringComparer.Ordinal);
        var overBudget = false;
        await CompleteAsync(job, async (tx, locked) =>
        {
            var internalEstimate = InternalCostEstimator.Segmented(RunStore.Section(locked.Estimate, "internal"),
                jev.Calls, jev.SieveCalls, jev.EstimatedInputTokens, jev.EstimatedCostUsd, Ctx.Time.GetUtcNow());
            await RunStore.SetJsonAsync(tx.Connection, tx.Transaction, run.Id, "estimate", locked.Estimate, ct).ConfigureAwait(false);
            if (locked.IsSample && !jev.IsMock && !InternalCostEstimator.WithinSampleBudget(internalEstimate, Ctx.Runs))
            {
                // Over the cap of the free sample nothing more is paid: the run fails with the code, no smaller sample is passed off.
                overBudget = true;
                await RunTransitions.FailAsync(tx.Connection, tx.Transaction, Ctx.Queue, locked, RunCodes.SampleBudgetExceeded, ct).ConfigureAwait(false);
                return;
            }

            await RunPages.WriteVersionsAsync(tx.Connection, tx.Transaction, job.Scope, pages, fingerprints, Ctx.Time.GetUtcNow(), ct).ConfigureAwait(false);
            RunStore.Section(locked.Progress, "steps")["sieve"] = new JsonObject { ["done"] = 0, ["total"] = batches.Count };
            locked.Progress["segments"] = segmented.Segments.Count;
            await RunStore.SetJsonAsync(tx.Connection, tx.Transaction, run.Id, "progress", locked.Progress, ct).ConfigureAwait(false);
            if (!await RunStateMachine.TryTransitionAsync(tx.Connection, tx.Transaction, run.TenantId, run.Id, RunStatus.Segmenting, RunStatus.Evaluating, ct).ConfigureAwait(false))
            {
                return;
            }

            if (batches.Count == 0)
            {
                await tx.EnqueueAsync(RunPlan.PlanEvaluate(locked), ct).ConfigureAwait(false);
                return;
            }

            for (var i = 0; i < batches.Count; i++)
            {
                await tx.EnqueueAsync(RunPlan.Sieve(locked, i + 1), ct).ConfigureAwait(false);
            }
        }, ct).ConfigureAwait(false);
        Logger.LogInformation("run.segmented {RunId} {TenantId} {JobId} {Pages} {Segments} {SieveBatches} {OverBudget}",
            run.Id, run.TenantId, job.Job.Id, pages.Count, segmented.Segments.Count, batches.Count, overBudget);
        return JobResult.Done;
    }
}
