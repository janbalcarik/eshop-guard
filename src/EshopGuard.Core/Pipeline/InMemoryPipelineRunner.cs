using System.Diagnostics;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Extract;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Profiles;
using EshopGuard.Core.Rules;
using EshopGuard.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Pipeline;

/// <summary>
/// Default <see cref="IEshopGuard"/>: runs the steps of the analysis one after another in memory (the CLI and tests); the
/// web application runs the same steps as jobs. Order of a scan: rules, discovery, batches of downloads with extraction,
/// profile plan, segments, estimate and the host's confirmation, new profiles, sieve, Jev, rules, result.
/// </summary>
internal sealed class InMemoryPipelineRunner(
    IRuleSetProvider ruleSetProvider,
    DiscoveryStep discoveryStep,
    FetchStep fetchStep,
    ProfileStep profileStep,
    SegmentStep segmentStep,
    EstimateStep estimateStep,
    SieveStep sieveStep,
    EvaluateStep evaluateStep,
    RulesStep rulesStep,
    SegmentEvaluator segmentEvaluator,
    IPageStore pageStore,
    IOptions<EshopGuardOptions> guardOptions,
    ILogger<InMemoryPipelineRunner> logger) : IEshopGuard
{
    public async Task<ScanResult> ScanSiteAsync(Uri siteUrl, ScanOptions options,
        IProgress<ScanProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(siteUrl);
        ArgumentNullException.ThrowIfNull(options);
        if (!siteUrl.IsAbsoluteUri || siteUrl.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("The site URL must be an absolute http or https URL.", nameof(siteUrl));
        }

        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Scanning {Url}", siteUrl);

        // Rules are loaded first, so an invalid YAML file stops the run before anything is downloaded.
        var catalog = await ruleSetProvider.LoadAsync(ct);
        var warnings = new List<string>();
        var ruleSets = RulesStep.SelectRuleSets(catalog, options.Modules, options.Country, warnings);

        // The sieve asks the topic of the sentence modules that have a sieve question; legal paragraphs always go in full.
        var sieve = options.UseSieve ? catalog.Sieve : null;
        var sieveModules = RulesStep.SieveModules(sieve, ruleSets);
        if (sieveModules.Count == 0)
        {
            sieve = null;
        }

        var site = new SiteScope(siteUrl);
        var stored = await profileStep.LoadStoredAsync(site, ct);
        var (crawl, crawlWarnings, pages) = await CrawlAsync(site, options, stored, progress, ct);
        warnings.InsertRange(0, crawlWarnings);
        if (crawl.NotProcessed.Count > 0)
        {
            warnings.Insert(crawlWarnings.Count, $"{crawl.NotProcessed.Count} stažených stránek se nepodařilo přečíst v časovém limitu; nejsou zkontrolované (seznam je v části „Co nebylo zkontrolováno“).");
        }

        if (crawl.Counters.SsrfBlocked.Count > 0)
        {
            warnings.Insert(crawlWarnings.Count, $"{crawl.Counters.SsrfBlocked.Count} adres se nestáhlo, protože vedou do vnitřní nebo místní sítě (ochrana proti SSRF); seznam je v části „Co nebylo zkontrolováno“.");
        }

        // Stored profiles were applied during the extraction; new ones are only planned and priced until the host confirms.
        var plan = await profileStep.PlanAsync(ProfileStep.PlanInput(site, pages, stored), ct);
        progress?.Report(new ScanProgress { Stage = ScanStage.Segmentation, Completed = 0, Total = pages.Count });
        var segmentInput = new SegmentInput(pages, sieve?.MaxChunkChars);
        var segmented = segmentStep.Segment(segmentInput);
        SegmentStep.ApplyCounts(segmented, pages);
        var hasLegalPages = pages.Any(p => p.Info.Type == PageType.Legal);
        if (pages.Count > 0 && !hasLegalPages)
        {
            warnings.Add("Nenalezeny právní stránky (obchodní podmínky, reklamační řád, odstoupení).");
        }

        var notLoaded = pages.Where(p => p.Info.TextNotLoaded).Select(p => p.Info).ToList();
        warnings.AddRange(RulesStep.NotLoadedWarnings(notLoaded.Count, pages.Count));

        // One estimate for new profiles and Jev, confirmed before any paid call.
        var runEstimate = await estimateStep.EstimateAsync(new EstimateInput(segmented, sieveModules, options.QuestionLanguage, plan), ruleSets, sieve, ct);
        var estimate = runEstimate.Estimate;
        var confirmed = !runEstimate.AsksConfirmation || options.ConfirmJevCalls is not { } confirm || await confirm(estimate, ct);
        var created = ProfileCreateResult.None;
        EvaluationSummary evaluation;
        SieveBatchResult? sieved = null;
        if (!confirmed)
        {
            logger.LogWarning("Evaluation skipped: the estimate was not confirmed");
            evaluation = new EvaluationSummary { Estimate = estimate, Skipped = true };
        }
        else
        {
            if (plan.WillCreate)
            {
                created = await profileStep.CreateAsync(plan, pages, stored, ct);
                segmented = segmentStep.Segment(segmentInput);
                SegmentStep.ApplyCounts(segmented, pages);
            }

            if (sieve is not null)
            {
                sieved = await sieveStep.SieveAsync(new SieveBatchInput(segmented.SieveChunks, sieveModules, options.QuestionLanguage, options.Concurrency), sieve, progress, ct);
            }

            var states = SieveStep.States(segmented.Segments, ruleSets, sieve, sieveModules, sieved?.Chunks);
            var batch = await evaluateStep.EvaluateAsync(new EvaluateBatchInput(states, options.QuestionLanguage, options.Concurrency), ruleSets, progress, ct);
            EvaluateStep.Apply(batch, segmented.Segments);
            evaluation = Summary(batch, estimate);
        }

        await RecordVersionsAsync(site, pages, segmented, ct);

        warnings.AddRange(created.Warnings);
        if (plan.Planned.Count > 0 && plan.UnavailableReason is { } unavailable && !estimate.IsMock)
        {
            warnings.Add($"Profily {plan.Planned.Count} šablon se nevytvořily ({unavailable}); jejich stránky se kontrolovaly celé.");
        }

        if (sieved is { Errors: > 0 } || sieved is { TooLong: > 0 })
        {
            warnings.Add($"Síto nevyhodnotilo {sieved.Errors + sieved.TooLong} úseků (chyba nebo příliš dlouhý text); jejich věty prošly celou podrobnou kontrolou.");
        }

        IReadOnlyList<Finding> findings = [];
        if (evaluation.Skipped)
        {
            warnings.Add("Vyhodnocení Jevem nebylo potvrzeno, zpráva obsahuje jen stažené stránky a segmenty.");
        }
        else if (pages.Count > 0)
        {
            var input = RulesStep.ForScan(options.Country, segmented.Segments, pages, crawl.Counters.UncheckedDocuments, notLoaded,
                !hasLegalPages && ruleSets.Any(s => s.Module == "legal"));
            findings = rulesStep.Evaluate(input, catalog, ruleSets).Findings;
        }

        if (evaluation.Errors > 0)
        {
            warnings.Add($"{evaluation.Errors} z {evaluation.Calls} volání Jevu selhalo; tyto segmenty nejsou vyhodnocené (podrobnosti v run.log).");
        }

        var result = ScanResultAssembler.Assemble(new ScanParts
        {
            SiteUrl = siteUrl,
            Options = options,
            StartedAt = startedAt,
            Duration = stopwatch.Elapsed,
            Catalog = catalog,
            RuleSets = ruleSets,
            Sieve = sieve,
            SieveModules = sieveModules,
            Crawl = crawl,
            Pages = pages,
            TextNotLoaded = notLoaded,
            Segments = segmented,
            ProfilePlan = plan,
            ProfilesCreated = created,
            ProfileUses = ProfileFitting.Uses(pages, stored, created.Created),
            Evaluation = evaluation,
            Sieved = sieved,
            Findings = findings,
            Warnings = warnings,
        }, segmentEvaluator.Cost);

        logger.LogInformation(
            "Scan of {Url} finished: {Pages} pages, {Unique} unique segments, {Findings} findings, {Calls} Jev calls",
            siteUrl, result.Stats.PagesFetched, result.Stats.UniqueSegments, result.Findings.Count, result.Stats.JevCalls);
        return result;
    }

    public async Task<AnalysisResult> AnalyzeTextsAsync(IReadOnlyList<TextInput> texts, AnalyzeOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(options);

        var stopwatch = Stopwatch.StartNew();
        var catalog = await ruleSetProvider.LoadAsync(ct);
        var warnings = new List<string>();
        var ruleSets = RulesStep.SelectRuleSets(catalog, options.Modules, options.Country, warnings);

        // Every text is a page of its own: lines are blocks of the main text, a legal text is also split into paragraphs.
        var pages = new List<ExtractedPageRecord>();
        var pageTexts = new Dictionary<string, string>();
        var pageCategories = new Dictionary<string, string>();
        for (var i = 0; i < texts.Count; i++)
        {
            var input = texts[i];
            var url = input.Url ?? input.Id ?? $"text:{i + 1}";
            pages.Add(new ExtractedPageRecord
            {
                Info = new PageInfo { Url = url, Type = input.Kind == TextKind.Legal ? PageType.Legal : PageType.Content },
                Content = new ExtractedPage
                {
                    MainBlocks = input.Text
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(line => new TextBlock(TextTools.Clean(line)))
                        .ToList(),
                },
            });

            pageTexts[url] = pageTexts.GetValueOrDefault(url, "") + "\n" + input.Text;
            if (!string.IsNullOrWhiteSpace(input.Category))
            {
                pageCategories[url] = TextTools.Clean(input.Category);
            }
        }

        var segmented = segmentStep.Segment(new SegmentInput(pages, SieveChunkChars: null));
        var segments = segmented.Segments;
        var states = SieveStep.States(segments, ruleSets, sieve: null, [], chunks: null);
        var evaluation = EvaluationSummary.None;
        if (states.Any(s => s.Modules.Count > 0))
        {
            var estimate = await segmentEvaluator.EstimateAsync(segments, ruleSets, options.QuestionLanguage, ct);
            if (options.ConfirmJevCalls is { } confirm && !await confirm(estimate, ct))
            {
                logger.LogWarning("Evaluation skipped: the estimate was not confirmed");
                evaluation = new EvaluationSummary { Estimate = estimate, Skipped = true };
            }
            else
            {
                var batch = await evaluateStep.EvaluateAsync(new EvaluateBatchInput(states, options.QuestionLanguage, options.Concurrency), ruleSets, progress: null, ct);
                EvaluateStep.Apply(batch, segments);
                evaluation = Summary(batch, estimate);
            }
        }

        var engine = evaluation.Skipped
            ? new RuleEngineOutput()
            : rulesStep.Evaluate(
                new RulesInput(options.Country, segments, pageTexts, new Dictionary<string, PageSignals>(), pageCategories, [], [], false)
                {
                    EvaluateSiteSignals = false,
                    EvaluateSitePresence = texts.Any(t => t.Kind == TextKind.Legal),
                },
                catalog,
                ruleSets);

        if (evaluation.Skipped)
        {
            warnings.Add("Vyhodnocení Jevem nebylo potvrzeno.");
        }

        return new AnalysisResult
        {
            Segments = segments,
            Findings = engine.Findings,
            RuleResults = engine.RuleResults,
            RuleSets = ruleSets.Select(RulesStep.Describe).ToList(),
            JevModel = evaluation.Model,
            Warnings = warnings,
            Stats = new ScanStats
            {
                SegmentOccurrences = segmented.OccurrenceCount,
                UniqueSegments = segments.Count,
                SentenceSegments = segments.Count(s => s.Kind == SegmentKind.Sentence),
                LegalParagraphSegments = segments.Count(s => s.Kind == SegmentKind.LegalParagraph),
                JevCalls = evaluation.Calls,
                JevCacheHits = evaluation.CacheHits,
                JevErrors = evaluation.Errors,
                InputTokens = evaluation.InputTokens,
                EstimatedCostUsd = Math.Round(segmentEvaluator.Cost(evaluation.InputTokens), 6),
                DurationSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 1),
            },
        };
    }

    /// <summary>
    /// Discovery and the batches of downloads, each extracted right away as the CLI always did; the frontier carries the
    /// state from batch to batch.
    /// </summary>
    private async Task<(CrawlSummary Crawl, List<string> Warnings, List<ExtractedPageRecord> Pages)> CrawlAsync(
        SiteScope site, ScanOptions options, IReadOnlyList<PageProfile> stored, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        var crawl = guardOptions.Value.Crawl;
        var limits = new CrawlLimits(options.MaxPages ?? crawl.MaxPages, options.SampleProducts ?? crawl.SampleProducts, options.Include, options.Exclude, options.RequestsPerSecond);
        var clock = Stopwatch.StartNew();
        var discovery = await discoveryStep.DiscoverAsync(new DiscoveryInput(site, limits), progress, ct);
        if (discovery.HomeBlocked)
        {
            throw new SsrfBlockedException(site.Home.AbsoluteUri);
        }

        var frontier = discovery.Frontier;
        var pace = discovery.Pace;
        var pages = new List<ExtractedPageRecord>();
        var notProcessed = new List<NotProcessedPage>();
        var maxDuration = TimeSpan.FromSeconds(Math.Max(1, crawl.FetchBatchMaxSeconds));
        try
        {
            while (!frontier.Stopped)
            {
                var batch = await fetchStep.FetchBatchAsync(
                    new FetchBatchInput(site, discovery.Robots, frontier, pace, Math.Max(1, crawl.FetchBatchMaxPages), maxDuration, ExtractInline: true) { StoredProfiles = stored },
                    progress, ct);
                pace = batch.Pace;
                foreach (var page in batch.Pages)
                {
                    if (page.Extract is not { } extract)
                    {
                        continue;
                    }

                    if (extract.Status == ExtractionStatus.Ok)
                    {
                        pages.Add(extract);
                    }
                    else
                    {
                        notProcessed.Add(new NotProcessedPage { Url = extract.Info.Url, Reason = extract.NotProcessedReason ?? ExtractStep.TimeoutReason });
                    }
                }
            }
        }
        finally
        {
            logger.LogInformation("Crawl finished: {Requests} requests in {Seconds:0.0} s, final pace {Rate:0.00}/s, throttled {Throttled}×",
                frontier.Counters.Requests, clock.Elapsed.TotalSeconds, pace.Rate, pace.Throttled);
        }

        return (new CrawlSummary(frontier.Counters, pace, clock.Elapsed, notProcessed), [.. discovery.Warnings], pages);
    }

    /// <summary>The pages and their versions: a new version only when the text changed, with the fingerprints of its segments.</summary>
    private async Task RecordVersionsAsync(SiteScope site, IReadOnlyList<ExtractedPageRecord> pages, SegmentResult segmented, CancellationToken ct)
    {
        var fingerprints = segmented.Pages.ToDictionary(p => p.Url, p => p.Fingerprints);
        var now = DateTimeOffset.UtcNow;
        foreach (var page in pages)
        {
            var info = page.Info;
            await pageStore.UpsertPageAsync(new PageRecord(site.SiteKey, info.Url, info.Url, TextTools.Snake(info.Type), null, null, page.TextHash, now), ct);
            await pageStore.AddVersionAsync(new PageVersionRecord(
                site.SiteKey, info.Url, page.TextHash, fingerprints.GetValueOrDefault(info.Url) ?? [], info.VisibleTextChars, info.CheckedTextChars,
                TextTools.Snake(info.Extraction), info.ScriptApp, info.TextNotLoaded, now), ct);
        }
    }

    private static EvaluationSummary Summary(EvaluateBatchResult batch, JevCallEstimate estimate) =>
        batch.Calls == 0 && batch.CacheHits == 0 && batch.Errors == 0 && batch.Model is null
            ? EvaluationSummary.None
            : new EvaluationSummary
            {
                Estimate = estimate,
                Calls = batch.Calls,
                CacheHits = batch.CacheHits,
                Errors = batch.Errors,
                InputTokens = batch.InputTokens,
                Model = batch.Model,
                NotEvaluated = batch.NotEvaluated,
            };
}
