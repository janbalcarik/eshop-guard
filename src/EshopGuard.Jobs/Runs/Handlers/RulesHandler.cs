using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Core;
using EshopGuard.Core.Fix;
using Finding = EshopGuard.Core.Models.Finding;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
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
/// <c>run.rules</c>: the rules over the evaluated segments and the pages of the whole site (<c>RulesStep</c>, jurisdictions of
/// the run), the result of the run composed as the CLI composes it (<c>ScanResultAssembler</c>, the same warnings and
/// statistics), the findings into <c>checks.findings</c> with their occurrences, the summary of a free sample, and the
/// rewrites: batches of <c>Runs:RewriteBatchPages</c> pages for a full analysis (their price stored first), or one example
/// fix for a free sample.
/// </summary>
internal sealed class RulesHandler(
    RunHandlerContext context,
    IRuleSetProvider ruleSets,
    RulesStep rulesStep,
    SegmentEvaluator segmentEvaluator,
    ITextRewriter rewriter,
    IPageContentStore contents) : RunJobHandler(context)
{
    public const string ResultFolder = "result";
    public const string ResultFile = "scan";
    public const string FindingIdsFile = "finding-ids";

    public static string RewriteFile(int batch) => $"rewrite-{batch}";

    public override string Kind => RunJobKinds.Rules;

    public override JobResourceClass ResourceClass => JobResourceClass.Cpu;

    protected override IReadOnlyCollection<RunStatus> Statuses { get; } = [RunStatus.Ruling];

    protected override async Task<JobResult> RunAsync(RunJob job, CancellationToken ct)
    {
        var run = job.Run;
        var clock = Stopwatch.StartNew();
        var rules = await RunRules.LoadAsync(ruleSets, run, Ctx.Runs, ct).ConfigureAwait(false);
        var (urls, scopes) = await InTenantAsync(run.TenantId, async (c, t) =>
            (await RunPages.LoadUrlsAsync(c, t, run.Id, null, ct).ConfigureAwait(false), await RunScopeStore.LoadAllAsync(c, t, run.Id, ct).ConfigureAwait(false)), ct).ConfigureAwait(false);
        var pages = await RunPages.LoadPagesAsync(contents, urls, ct).ConfigureAwait(false);
        var records = pages.Select(p => p.Record).ToList();
        var scan = await AssembleAsync(job, rules, urls, scopes, records, ct).ConfigureAwait(false);
        await RunFiles.PutAsync(Ctx.Blobs, job.Scope, ResultFolder, ResultFile, scan, ct).ConfigureAwait(false);

        // Rewrites: their price is stored before the first call (and checked against the cap of a free sample).
        var pageInputs = records.Where(r => r.Info.IncludedInAnalysis).Select(RewritePage).ToList();
        var rewriteAll = new RewriteInput { Pages = pageInputs, Findings = scan.Findings, Country = rules.Jurisdictions[0], Jurisdictions = rules.Jurisdictions };
        var sorted = SampleFindingOrder.Sort(scan.Findings);
        var candidates = run.IsSample ? sorted.Take(5).Where(PageRewriter.IsRewritable).Take(Ctx.Runs.FreeSample.MaxExampleAttempts).ToList() : [];
        var batches = run.IsSample
            ? candidates.Count > 0 ? [new RewriteInput { Pages = pageInputs, Findings = candidates, Country = rewriteAll.Country, Jurisdictions = rewriteAll.Jurisdictions }] : []
            : RewriteStep.Batches(rewriteAll, Ctx.Runs.RewriteBatchPages);
        for (var i = 0; i < batches.Count; i++)
        {
            await RunFiles.PutAsync(Ctx.Blobs, job.Scope, RunFiles.Work, RewriteFile(i + 1), batches[i], ct).ConfigureAwait(false);
        }

        var rewriteUsd = 0m;
        foreach (var batch in batches)
        {
            rewriteUsd += run.IsSample
                ? (await rewriter.EstimateAsync(new RewriteInput { Pages = batch.Pages, Findings = [candidates[0]], Country = batch.Country, Jurisdictions = batch.Jurisdictions }, ct).ConfigureAwait(false)).EstimatedCostUsd * candidates.Count
                : (await rewriter.EstimateAsync(batch, ct).ConfigureAwait(false)).EstimatedCostUsd;
        }

        var pageIds = pages.GroupBy(p => p.Url, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().PageId, StringComparer.Ordinal);
        List<StoredFinding> stored = [];
        await CompleteAsync(job, async (tx, locked) =>
        {
            var (connection, transaction) = (tx.Connection, tx.Transaction);
            stored = await FindingWriter.WriteAsync(connection, transaction, job.Scope, scan.Findings, rules.RuleSets, pageIds, ct).ConfigureAwait(false);
            var ids = stored.ToDictionary(s => s.Key, s => s.Id);

            // Written before the commit that makes the rewrites visible: a rewrite never starts without the ids it needs.
            await RunFiles.PutAsync(Ctx.Blobs, job.Scope, RunFiles.Work, FindingIdsFile, stored, ct).ConfigureAwait(false);
            locked.Stats["scan"] = JsonNode.Parse(JsonSerializer.Serialize(scan.Stats, PipelineJson.Options));
            locked.Stats["findings"] = stored.Count;
            locked.Stats["findings_by_severity"] = SampleSummaryBuilder.BySeverity(sorted);
            if (run.IsSample)
            {
                locked.Stats["sample"] = SampleSummaryBuilder.Build(sorted, ids, records, rules.Jurisdictions);
            }

            await RunStore.SetJsonAsync(connection, transaction, run.Id, "stats", locked.Stats, ct).ConfigureAwait(false);
            InternalCostEstimator.OpenAi(RunStore.Section(locked.Estimate, "internal"), "rewrite_usd", rewriteUsd, Ctx.Time.GetUtcNow());
            await RunStore.SetJsonAsync(connection, transaction, run.Id, "estimate", locked.Estimate, ct).ConfigureAwait(false);
            RunStore.Section(locked.Progress, "steps")["rewrite"] = new JsonObject { ["done"] = 0, ["total"] = batches.Count };
            await RunStore.SetJsonAsync(connection, transaction, run.Id, "progress", locked.Progress, ct).ConfigureAwait(false);
            if (!await RunStateMachine.TryTransitionAsync(connection, transaction, run.TenantId, run.Id, RunStatus.Ruling, RunStatus.Rewriting, ct).ConfigureAwait(false))
            {
                return;
            }

            var affordable = !run.IsSample || InternalCostEstimator.WithinSampleBudget(RunStore.Section(locked.Estimate, "internal"), Ctx.Runs);
            if (batches.Count == 0 || !affordable)
            {
                if (run.IsSample)
                {
                    // Without a finding to rewrite, or over the cap of the sample, there is no example fix; the summary says why.
                    var sample = RunStore.Section(locked.Stats, "sample");
                    sample["example_fix_missing_reason"] = batches.Count == 0 ? RunCodes.NoRewritableFinding : RunCodes.RewriteFailed;
                    await RunStore.SetJsonAsync(connection, transaction, run.Id, "stats", locked.Stats, ct).ConfigureAwait(false);
                }

                await tx.EnqueueAsync(RunPlan.Finalize(locked), ct).ConfigureAwait(false);
                return;
            }

            for (var i = 0; i < batches.Count; i++)
            {
                var candidateIds = run.IsSample ? candidates.Select(c => ids[FindingWriter.Key(c)]).ToList() : null;
                await tx.EnqueueAsync(RunPlan.Rewrite(locked, i + 1, candidateIds), ct).ConfigureAwait(false);
            }
        }, ct).ConfigureAwait(false);
        Logger.LogInformation("run.rules {RunId} {TenantId} {JobId} {Findings} {Rewrites} {Seconds}", run.Id, run.TenantId, job.Job.Id, stored.Count, batches.Count, clock.Elapsed.TotalSeconds);
        return JobResult.Done;
    }

    /// <summary>The result of the run with the same warnings and statistics as the CLI's (<c>InMemoryPipelineRunner</c>).</summary>
    private async Task<ScanResult> AssembleAsync(
        RunJob job, RunRules rules, IReadOnlyList<RunUrlRow> urls, IReadOnlyList<RunScopeRow> scopes, List<ExtractedPageRecord> pages, CancellationToken ct)
    {
        var run = job.Run;
        var scope = job.Scope;
        var segmented = await RunFiles.RequireAsync<SegmentResult>(Ctx.Blobs, scope, RunFiles.Work, PlanEvaluateHandler.SievedSegmentsFile, ct).ConfigureAwait(false);
        SegmentStep.ApplyCounts(segmented, pages);
        var evaluateBatches = (int)RunStore.Long(RunStore.Section(run.Progress, "steps", "evaluate"), "total");
        var results = new List<EvaluateBatchResult>();
        for (var i = 1; i <= evaluateBatches; i++)
        {
            var result = await RunFiles.RequireAsync<EvaluateBatchResult>(Ctx.Blobs, scope, RunFiles.Work, EvaluateBatchHandler.ResultFile(i), ct).ConfigureAwait(false);
            EvaluateStep.Apply(result, segmented.Segments);
            results.Add(result);
        }

        var estimate = (await RunFiles.RequireAsync<RunEstimate>(Ctx.Blobs, scope, RunFiles.Work, SegmentHandler.EstimateFile, ct).ConfigureAwait(false)).Estimate;
        var evaluation = Summary(results, estimate);
        var sieved = rules.Sieve is null ? null : await RunFiles.GetAsync<SieveBatchResult>(Ctx.Blobs, scope, RunFiles.Work, PlanEvaluateHandler.SieveSummaryFile, ct).ConfigureAwait(false);
        var site = new SiteScope(new Uri(run.ShopBaseUrl));
        var plan = await RunFiles.GetAsync<ProfilePlan>(Ctx.Blobs, scope, RunFiles.Work, ProfileHandler.PlanFile, ct).ConfigureAwait(false) ?? ProfilePlan.Off(site.SiteKey);
        var created = await RunFiles.GetAsync<ProfileCreateResult>(Ctx.Blobs, scope, RunFiles.Work, ProfileHandler.CreatedFile, ct).ConfigureAwait(false) ?? ProfileCreateResult.None;
        var storedProfiles = await RunFiles.GetAsync<List<PageProfile>>(Ctx.Blobs, scope, RunFiles.Work, ProfileHandler.StoredFile, ct).ConfigureAwait(false) ?? [];

        // The crawl of all scopes: counters together, what was downloaded but not read.
        var counters = Merge(scopes.Select(s => s.Frontier.Counters));
        var notProcessed = urls.Where(u => u.State == RunUrlState.ExtractTimeout).Select(u => new NotProcessedPage { Url = u.Url, Reason = u.ErrorCode ?? ExtractStep.TimeoutReason }).ToList();
        var pace = scopes.Count > 0 ? scopes[0].Pace : new PaceState(0, 0, true, 0);
        var crawl = new CrawlSummary(counters, pace, TimeSpan.FromSeconds((double)RunStore.Decimal(run.Progress, "crawl_seconds")), notProcessed);
        var crawlWarnings = new List<ScanWarning>();
        for (var i = 0; ; i++)
        {
            if (await RunFiles.GetAsync<DiscoveryResult>(Ctx.Blobs, scope, RunFiles.Discovery, DiscoverHandler.Scope(i), ct).ConfigureAwait(false) is not { } discovery)
            {
                break;
            }

            crawlWarnings.AddRange(discovery.Warnings);
        }

        var warnings = rules.Warnings.ToList();
        warnings.InsertRange(0, crawlWarnings);
        if (notProcessed.Count > 0)
        {
            warnings.Insert(crawlWarnings.Count, new ScanWarning(EngineCodes.PagesNotProcessed, NoteParams.Of(("count", notProcessed.Count))));
        }

        if (counters.SsrfBlocked.Count > 0)
        {
            warnings.Insert(crawlWarnings.Count, new ScanWarning(EngineCodes.SsrfBlockedUrls, NoteParams.Of(("count", counters.SsrfBlocked.Count))));
        }

        var hasLegalPages = pages.Any(p => p.Info.Type == PageType.Legal);
        if (pages.Count > 0 && !hasLegalPages)
        {
            warnings.Add(new ScanWarning(EngineCodes.NoLegalPages, NoteParams.None));
        }

        var notLoaded = pages.Where(p => p.Info.TextNotLoaded).Select(p => p.Info).ToList();
        warnings.AddRange(RulesStep.NotLoadedWarnings(notLoaded.Count, pages.Count));
        warnings.AddRange(created.Warnings);
        if (plan.Planned.Count > 0 && plan.UnavailableReason is { } unavailable && !estimate.IsMock)
        {
            warnings.Add(new ScanWarning(EngineCodes.ProfilesNotCreated, NoteParams.Of(("count", plan.Planned.Count), ("reason", new FindingNote(unavailable, NoteParams.None)))));
        }

        if (sieved is { Errors: > 0 } || sieved is { TooLong: > 0 })
        {
            warnings.Add(new ScanWarning(EngineCodes.SieveUnevaluated, NoteParams.Of(("count", sieved.Errors + sieved.TooLong))));
        }

        IReadOnlyList<Finding> findings = [];
        IReadOnlyList<SiteObligation> obligations;
        var asOf = rulesStep.Today;
        if (pages.Count > 0)
        {
            var input = RulesStep.ForScan(rules.Jurisdictions, segmented.Segments, pages, counters.UncheckedDocuments, notLoaded,
                !hasLegalPages && rules.RuleSets.Any(s => s.Module == "legal")) with { AsOf = asOf };
            var engine = rulesStep.Evaluate(input, rules.Catalog, rules.RuleSets);
            findings = engine.Findings;
            obligations = engine.SiteObligations;
        }
        else
        {
            obligations = RuleEngine.NotChecked(rules.RuleSets, rules.Jurisdictions, EngineCodes.SiteNotCrawled);
        }

        if (evaluation.Errors > 0)
        {
            warnings.Add(new ScanWarning(EngineCodes.JevErrors, NoteParams.Of(("errors", evaluation.Errors), ("calls", evaluation.Calls))));
        }

        return ScanResultAssembler.Assemble(new ScanParts
        {
            SiteUrl = new Uri(run.ShopBaseUrl),
            Options = new ScanOptions { Modules = run.Modules, Country = rules.Jurisdictions[0], Jurisdictions = rules.Jurisdictions, QuestionLanguage = Ctx.Runs.QuestionLanguage },
            StartedAt = Ctx.Time.GetUtcNow(),
            Duration = crawl.Duration,
            Catalog = rules.Catalog,
            RuleSets = rules.RuleSets,
            Sieve = rules.Sieve,
            SieveModules = rules.SieveModules,
            Crawl = crawl,
            Pages = pages,
            TextNotLoaded = notLoaded,
            Segments = segmented,
            ProfilePlan = plan,
            ProfilesCreated = created,
            ProfileUses = ProfileFitting.Uses(pages, storedProfiles, created.Created),
            Evaluation = evaluation,
            Sieved = sieved,
            Findings = findings,
            Warnings = warnings,
            Jurisdictions = rules.Jurisdictions,
            AsOf = asOf,
            SiteObligations = obligations,
            Coverage = rules.Coverage,
        }, segmentEvaluator.Cost);
    }

    /// <summary>The evaluation of all batches as one (as <c>InMemoryPipelineRunner.Summary</c>).</summary>
    private static EvaluationSummary Summary(IReadOnlyList<EvaluateBatchResult> batches, JevCallEstimate estimate)
    {
        var calls = batches.Sum(b => b.Calls);
        var hits = batches.Sum(b => b.CacheHits);
        var errors = batches.Sum(b => b.Errors);
        var model = batches.Select(b => b.Model).FirstOrDefault(m => m is not null);
        return calls == 0 && hits == 0 && errors == 0 && model is null
            ? EvaluationSummary.None
            : new EvaluationSummary
            {
                Estimate = estimate,
                Calls = calls,
                CacheHits = hits,
                Errors = errors,
                InputTokens = batches.Sum(b => b.InputTokens),
                Model = model,
                NotEvaluated = batches.SelectMany(b => b.NotEvaluated).ToList(),
            };
    }

    private static CrawlCounters Merge(IEnumerable<CrawlCounters> all)
    {
        var merged = new CrawlCounters();
        foreach (var c in all)
        {
            merged.Requests += c.Requests;
            merged.Failed += c.Failed;
            merged.ExcludedByFilter += c.ExcludedByFilter;
            merged.OverLimit += c.OverLimit;
            merged.ProductOverLimit += c.ProductOverLimit;
            merged.RobotsBlocked.AddRange(c.RobotsBlocked);
            merged.SsrfBlocked.AddRange(c.SsrfBlocked);
            merged.UncheckedDocuments.AddRange(c.UncheckedDocuments);
            merged.CrawlDelaySeconds ??= c.CrawlDelaySeconds;
        }

        return merged;
    }

    private static RewritePageInput RewritePage(ExtractedPageRecord page) => new()
    {
        Url = page.Info.Url,
        Type = TextTools.Snake(page.Info.Type),
        Title = page.Info.Title,
        Category = page.Info.Category,
        MainText = page.Info.MainText,
        MetaDescription = page.Info.MetaDescription,
        JsonLdDescription = page.Info.JsonLdDescription,
    };
}

/// <summary>
/// The summary of a free sample (<c>runs.stats.sample</c>): findings by severity and by group of the strictest verdict, the
/// five most serious (<see cref="SampleFindingOrder"/>), the checked pages by language and the jurisdictions. Codes and
/// numbers only; the API composes the texts in the language of the reader.
/// </summary>
public static class SampleSummaryBuilder
{
    /// <summary>Findings (one per identity in the e-shop) by the severity of their strictest verdict.</summary>
    internal static JsonObject BySeverity(IReadOnlyList<Finding> sorted) => Counts(sorted.DistinctBy(FindingWriter.Key).ToList(), f => f.Strictest.Severity);

    internal static JsonObject Build(IReadOnlyList<Finding> sorted, IReadOnlyDictionary<FindingKey, Guid> ids, IReadOnlyList<ExtractedPageRecord> pages, IReadOnlyList<string> jurisdictions)
    {
        var merged = sorted.DistinctBy(FindingWriter.Key).ToList();
        return new JsonObject
        {
            ["findings_by_severity"] = Counts(merged, f => f.Strictest.Severity),
            ["findings_by_checkability"] = Counts(merged, f => FindingWriter.Checkability(f.Strictest.Checkability)),
            ["top_finding_ids"] = new JsonArray(merged.Take(5).Select(f => (JsonNode)ids[FindingWriter.Key(f)].ToString("D")).ToArray()),
            ["example_fix_proposal_id"] = null,
            ["example_fix_missing_reason"] = null,
            ["pages_checked_by_language"] = new JsonObject(pages.Where(p => p.Info.IncludedInAnalysis)
                .GroupBy(p => p.Info.Language ?? "und").OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => KeyValuePair.Create(g.Key, (JsonNode?)g.Count()))),
            ["jurisdictions"] = new JsonArray(jurisdictions.Select(j => (JsonNode)j).ToArray()),
        };
    }

    private static JsonObject Counts(IEnumerable<Finding> findings, Func<Finding, string> by) =>
        new(findings.GroupBy(by).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => KeyValuePair.Create(g.Key, (JsonNode?)g.Count())));
}
