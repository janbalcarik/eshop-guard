using EshopGuard.Core.Pipeline;
using EshopGuard.Core.Profiles;
using EshopGuard.Core.Storage;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Entities.Usage;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Runs.Storage;
using Microsoft.Extensions.Logging;

namespace EshopGuard.Jobs.Runs.Handlers;

/// <summary>
/// <c>run.profile</c>: profiles of the page templates over all pages of the run (<c>ProfileStep</c>): the plan without a
/// model, its price stored before the first call (and checked against the cap of a free sample), the new profiles one by one
/// into <c>shop.page_profiles</c>, and the pages that fit a new profile written back with it (the refit of the design runs in
/// the same job: the step fits the pages as it creates each profile). Pages without a profile are checked whole.
/// </summary>
internal sealed class ProfileHandler(RunHandlerContext context, ProfileStep profiles, IPageContentStore contents) : RunJobHandler(context)
{
    /// <summary>Work files of the profiles.</summary>
    public const string PlanFile = "profile-plan";
    public const string CreatedFile = "profiles-created";
    public const string StoredFile = "profiles-stored";

    public override string Kind => RunJobKinds.Profile;

    public override JobResourceClass ResourceClass => JobResourceClass.Llm;

    protected override IReadOnlyCollection<RunStatus> Statuses { get; } = [RunStatus.Profiling];

    protected override async Task<JobResult> RunAsync(RunJob job, CancellationToken ct)
    {
        var run = job.Run;
        var urls = await InTenantAsync(run.TenantId, (c, t) => RunPages.LoadUrlsAsync(c, t, run.Id, RunPages.PageStates, ct), ct).ConfigureAwait(false);
        var pages = await RunPages.LoadPagesAsync(contents, urls, ct).ConfigureAwait(false);
        var records = pages.Select(p => p.Record).ToList();
        var site = new SiteScope(new Uri(run.ShopBaseUrl));
        var stored = await profiles.LoadStoredAsync(site, ct).ConfigureAwait(false);
        var plan = await profiles.PlanAsync(ProfileStep.PlanInput(site, records, stored), ct).ConfigureAwait(false);
        await RunFiles.PutAsync(Ctx.Blobs, job.Scope, RunFiles.Work, PlanFile, plan, ct).ConfigureAwait(false);
        await RunFiles.PutAsync(Ctx.Blobs, job.Scope, RunFiles.Work, StoredFile, stored, ct).ConfigureAwait(false);

        var created = ProfileCreateResult.None;
        if (plan.WillCreate)
        {
            var estimate = await InTenantAsync(run.TenantId, async (c, t) =>
            {
                var locked = (await RunStore.LoadAsync(c, t, run.Id, forUpdate: true, ct).ConfigureAwait(false))!;
                var internalEstimate = InternalCostEstimator.OpenAi(RunStore.Section(locked.Estimate, "internal"), "profiles_usd", plan.EstimatedUsd, Ctx.Time.GetUtcNow());
                await RunStore.SetJsonAsync(c, t, run.Id, "estimate", locked.Estimate, ct).ConfigureAwait(false);
                return internalEstimate;
            }, ct).ConfigureAwait(false);
            if (run.IsSample && !InternalCostEstimator.WithinSampleBudget(estimate, Ctx.Runs))
            {
                return await FailRunAsync(job, RunCodes.SampleBudgetExceeded, ct).ConfigureAwait(false);
            }

            var withoutProfile = records.Where(r => r.Fit is null).ToHashSet();
            created = await profiles.CreateAsync(plan, records, stored, ct).ConfigureAwait(false);
            foreach (var page in pages.Where(p => withoutProfile.Contains(p.Record) && p.Record.Fit is not null))
            {
                await contents.PutExtractAsync(new PageContentKey(site.SiteKey, page.Url), PipelineJson.Serialize(page.Record), ct).ConfigureAwait(false);
            }
        }

        await RunFiles.PutAsync(Ctx.Blobs, job.Scope, RunFiles.Work, CreatedFile, created, ct).ConfigureAwait(false);
        await CompleteAsync(job, async (tx, locked) =>
        {
            await Ctx.Usage.WriteAsync(tx.Connection, tx.Transaction, job.Scope,
                [new UsageEntry(UsageProvider.Openai, UsageOperation.Profile, created.Created.FirstOrDefault()?.Model, created.Calls, 0, created.InputTokens, 0, created.OutputTokens, 0, created.CostUsd)],
                ct).ConfigureAwait(false);
            if (await RunStateMachine.TryTransitionAsync(tx.Connection, tx.Transaction, run.TenantId, run.Id, RunStatus.Profiling, RunStatus.Segmenting, ct).ConfigureAwait(false))
            {
                await tx.EnqueueAsync(RunPlan.Segment(locked), ct).ConfigureAwait(false);
            }
        }, ct).ConfigureAwait(false);
        Logger.LogInformation("run.profiles {RunId} {TenantId} {JobId} {Pages} {Planned} {Created}", run.Id, run.TenantId, job.Job.Id, pages.Count, plan.Planned.Count, created.Created.Count);
        return JobResult.Done;
    }
}
