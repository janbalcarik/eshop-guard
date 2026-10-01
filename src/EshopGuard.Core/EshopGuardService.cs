using System.Diagnostics;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Extract;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Profiles;
using EshopGuard.Core.Rules;
using EshopGuard.Core.Segmentation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core;

/// <summary>
/// Default <see cref="IEshopGuard"/>: load rules, crawl, extract, match profiles of page templates, segment, deduplicate,
/// evaluate with Jev, apply rules.
/// </summary>
internal sealed class EshopGuardService(
    Crawler crawler,
    PageProfiler pageProfiler,
    SegmentBuilder segmentBuilder,
    IRuleSetProvider ruleSetProvider,
    SegmentEvaluator segmentEvaluator,
    PageSieve pageSieve,
    IOptions<EshopGuardOptions> guardOptions,
    ILogger<EshopGuardService> logger) : IEshopGuard
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
        var ruleSets = SelectRuleSets(catalog, options.Modules, options.Country, warnings);

        // The sieve asks the topic of the sentence modules that have a sieve question; legal paragraphs always go in full.
        var sieve = options.UseSieve ? catalog.Sieve : null;
        var sieveModules = sieve is null
            ? []
            : ruleSets.Where(s => s.AppliesTo == RuleValidator.Sentence && sieve.Questions.ContainsKey(s.Module)).Select(s => s.Module).Distinct().ToList();
        if (sieveModules.Count == 0)
        {
            sieve = null;
        }

        var crawl = await crawler.CrawlAsync(siteUrl, options, progress, ct);
        warnings.InsertRange(0, crawl.Warnings);

        // Stored profiles are applied now; new ones are only planned and priced until the host confirms the estimate.
        var profiling = await pageProfiler.PrepareAsync(siteUrl, crawl.Pages, ct);
        var pages = profiling.Pages;
        progress?.Report(new ScanProgress { Stage = ScanStage.Segmentation, Completed = 0, Total = pages.Count });
        var segmented = BuildSegments(pages, sieve);
        var hasLegalPages = pages.Any(p => p.Info.Type == PageType.Legal);
        if (pages.Count > 0 && !hasLegalPages)
        {
            warnings.Add("Nenalezeny právní stránky (obchodní podmínky, reklamační řád, odstoupení).");
        }

        var notLoaded = pages.Where(p => p.Info.TextNotLoaded).Select(p => p.Info).ToList();
        warnings.AddRange(NotLoadedWarnings(notLoaded.Count, pages.Count));

        // One estimate for new profiles and Jev, confirmed before any paid call. New profiles only leave text out,
        // so the Jev part counted before them is an upper bound.
        var (estimate, prepared) = await EstimateAsync(segmented, ruleSets, sieve, sieveModules, options.QuestionLanguage, ct);
        estimate = WithProfiles(estimate, profiling);
        logger.LogInformation("Estimate: {Calls} Jev calls, {Cost} USD; {Profiles} new profiles, {ProfileCost} USD, created: {WillCreate}",
            estimate.Calls, estimate.EstimatedCostUsd, estimate.ProfileTemplates, estimate.ProfileCostUsd, estimate.ProfilesWillRun);
        var asksConfirmation = sieve is not null || estimate.Calls + estimate.CachedCalls > 0 || estimate.ProfilesWillRun;
        var confirmed = !asksConfirmation || options.ConfirmJevCalls is not { } confirm || await confirm(estimate, ct);
        EvaluationSummary evaluation;
        SieveSummary? sieveSummary = null;
        if (!confirmed)
        {
            logger.LogWarning("Evaluation skipped: the estimate was not confirmed");
            evaluation = new EvaluationSummary { Estimate = estimate, Skipped = true };
        }
        else
        {
            if (profiling.WillCreate)
            {
                await pageProfiler.CreateAsync(profiling, ct);
                pages = profiling.Pages;
                segmented = BuildSegments(pages, sieve);
                prepared = null;
            }

            (evaluation, sieveSummary) = sieve is null
                ? (await segmentEvaluator.EvaluateAsync(segmented.Segments, ruleSets, options.QuestionLanguage, options.Concurrency, confirm: null, progress, ct), null)
                : await EvaluateWithSieveAsync(segmented, ruleSets, sieve, sieveModules, prepared, options, progress, ct);
        }

        warnings.AddRange(profiling.Warnings);
        if (profiling.Planned.Count > 0 && profiling.UnavailableReason is { } unavailable && !estimate.IsMock)
        {
            warnings.Add($"Profily {profiling.Planned.Count} šablon se nevytvořily ({unavailable}); jejich stránky se kontrolovaly celé.");
        }

        var segments = segmented.Segments;
        var occurrences = segmented.Occurrences;
        var analyzed = pages.Where(p => p.Info.IncludedInAnalysis).ToList();
        var sievedSets = sieve is null ? 0 : ruleSets.Count(s => s.AppliesTo == RuleValidator.Sentence && sieveModules.Contains(s.Module));
        if (sieveSummary is { Errors: > 0 } || sieveSummary is { TooLong: > 0 })
        {
            warnings.Add($"Síto nevyhodnotilo {sieveSummary.Errors + sieveSummary.TooLong} úseků (chyba nebo příliš dlouhý text); jejich věty prošly celou podrobnou kontrolou.");
        }

        var findings = new List<Finding>();
        if (evaluation.Skipped)
        {
            warnings.Add("Vyhodnocení Jevem nebylo potvrzeno, zpráva obsahuje jen stažené stránky a segmenty.");
        }
        else if (pages.Count > 0)
        {
            var engine = RuleEngine.Evaluate(new RuleEngineInput
            {
                RuleSets = ruleSets,
                Labels = catalog.Labels,
                Segments = segments,
                PageTexts = analyzed.ToDictionary(p => p.Info.Url, p => PageText(p.Info, p.Content)),
                PageSignals = pages.ToDictionary(p => p.Info.Url, p => Signals(p.Info, p.Content)),
                EvaluateSiteSignals = true,
                LegalRequirements = catalog.LegalRequirements,
                PageCategories = analyzed.ToDictionary(p => p.Info.Url, p => p.Info.Category),
                Country = options.Country,
                UncheckedDocuments = crawl.UncheckedDocuments,
                TextNotLoadedPages = notLoaded,
                AddMissingLegalPagesFinding = !hasLegalPages && ruleSets.Any(s => s.Module == "legal"),
            });
            findings = engine.Findings;
        }

        if (evaluation.Errors > 0)
        {
            warnings.Add($"{evaluation.Errors} z {evaluation.Calls} volání Jevu selhalo; tyto segmenty nejsou vyhodnocené (podrobnosti v run.log).");
        }

        var stats = new ScanStats
        {
            PagesFetched = pages.Count,
            PagesByType = pages.GroupBy(p => p.Info.Type).ToDictionary(g => g.Key, g => g.Count()),
            PagesFailed = crawl.Failed,
            PagesBlockedByRobots = crawl.RobotsBlocked.Count,
            PagesExcludedByFilter = crawl.ExcludedByFilter,
            PagesOverLimit = crawl.OverLimit,
            ProductPagesOverLimit = crawl.ProductOverLimit,
            PagesWithReadability = pages.Count(p => p.Info.Extraction == ExtractionMethod.Readability),
            PagesWithFallback = pages.Count(p => p.Info.Extraction == ExtractionMethod.Fallback),
            PagesTextNotLoaded = notLoaded.Count,
            VisibleTextChars = pages.Sum(p => (long)p.Info.VisibleTextChars),
            CheckedTextChars = pages.Sum(p => (long)Math.Min(p.Info.CheckedTextChars, p.Info.VisibleTextChars)),
            NavigationTextChars = pages.Sum(p => (long)p.Info.NavigationTextChars),
            ListingTextChars = pages.Sum(p => (long)p.Info.ListingTextChars),
            PagesWithUncheckedText = pages.Count(p => p.Info.HasUncheckedText),
            CrawlSeconds = Math.Round(crawl.Duration.TotalSeconds, 1),
            CrawlRequests = crawl.Requests,
            CrawlFinalRate = Math.Round(crawl.FinalRate, 2),
            CrawlThrottled = crawl.Throttled,
            CrawlDelaySeconds = crawl.CrawlDelay?.TotalSeconds ?? 0,
            SegmentOccurrences = occurrences.Count,
            UniqueSegments = segments.Count,
            SentenceSegments = segments.Count(s => s.Kind == SegmentKind.Sentence),
            LegalParagraphSegments = segments.Count(s => s.Kind == SegmentKind.LegalParagraph),
            BoilerplateSegments = segments.Count(s => s.Boilerplate),
            JevCalls = evaluation.Calls,
            JevCacheHits = evaluation.CacheHits,
            JevErrors = evaluation.Errors,
            InputTokens = evaluation.InputTokens + (sieveSummary?.InputTokens ?? 0),
            EstimatedCostUsd = Math.Round(segmentEvaluator.Cost(evaluation.InputTokens + (sieveSummary?.InputTokens ?? 0)), 6),
            SieveEnabled = sieve is not null,
            SieveThreshold = sieve?.Threshold ?? 0,
            SieveChunkChars = sieve?.MaxChunkChars ?? 0,
            SieveChunks = sieveSummary?.Chunks.Count ?? 0,
            SieveCalls = sieveSummary?.Calls ?? 0,
            SieveCacheHits = sieveSummary?.CacheHits ?? 0,
            SieveErrors = sieveSummary?.Errors ?? 0,
            SieveTooLong = sieveSummary?.TooLong ?? 0,
            SievePairs = sieveSummary is null ? 0 : segments.Count(s => s.Kind == SegmentKind.Sentence) * sievedSets,
            SieveSkippedPairs = segments.Sum(s => s.SkippedModules.Count),
            ProfilesEnabled = !profiling.Disabled,
            ProfilesUsed = profiling.Uses.Count,
            ProfilesCreated = profiling.Created.Count,
            ProfilesPlanned = profiling.Planned.Count,
            PagesWithProfile = pages.Count(p => p.Info.ProfileId is not null),
            PagesWithoutProfile = profiling.Entries.Count(e => e.Eligible && e.Profile is null),
            ProfileSkippedTextChars = pages.Sum(p => (long)p.Info.ProfileSkippedTextChars),
            ProfileCalls = profiling.Calls,
            ProfileInputTokens = profiling.InputTokens,
            ProfileOutputTokens = profiling.OutputTokens,
            ProfileCostUsd = Math.Round(profiling.CostUsd, 6),
            DurationSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 1),
        };

        logger.LogInformation(
            "Scan of {Url} finished: {Pages} pages, {Unique} unique segments, {Findings} findings, {Calls} Jev calls",
            siteUrl, stats.PagesFetched, stats.UniqueSegments, findings.Count, stats.JevCalls);

        return new ScanResult
        {
            SiteUrl = siteUrl.AbsoluteUri,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.UtcNow,
            Modules = options.Modules,
            Country = options.Country,
            QuestionLanguage = options.QuestionLanguage,
            Pages = pages.Select(p => p.Info).ToList(),
            Segments = segments,
            SieveChunks = sieveSummary?.Chunks ?? [],
            Findings = findings,
            ImagesForReview = ImagesForReview(analyzed.Select(p => p.Info), catalog.Labels),
            RuleSets = ruleSets.Select(Describe).ToList(),
            JevModel = evaluation.Model,
            EvaluationSkipped = evaluation.Skipped,
            UncheckedDocuments = crawl.UncheckedDocuments,
            RobotsBlockedUrls = crawl.RobotsBlocked,
            Profiles = profiling.Uses.Values.OrderBy(u => u.Profile.CreatedAt).ToList(),
            Warnings = warnings,
            Stats = stats,
        };
    }

    public async Task<AnalysisResult> AnalyzeTextsAsync(IReadOnlyList<TextInput> texts, AnalyzeOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(options);

        var stopwatch = Stopwatch.StartNew();
        var catalog = await ruleSetProvider.LoadAsync(ct);
        var warnings = new List<string>();
        var ruleSets = SelectRuleSets(catalog, options.Modules, options.Country, warnings);

        var occurrences = new List<SegmentOccurrence>();
        var pageTexts = new Dictionary<string, string>();
        var pageCategories = new Dictionary<string, string>();
        for (var i = 0; i < texts.Count; i++)
        {
            var input = texts[i];
            var url = input.Url ?? input.Id ?? $"text:{i + 1}";
            var page = new ExtractedPage
            {
                MainBlocks = input.Text
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(line => new TextBlock(TextTools.Clean(line)))
                    .ToList(),
            };
            var pageType = input.Kind == TextKind.Legal ? PageType.Legal : PageType.Content;
            occurrences.AddRange(segmentBuilder.BuildSentences(url, pageType, page));
            if (input.Kind == TextKind.Legal)
            {
                occurrences.AddRange(segmentBuilder.BuildLegalParagraphs(url, page));
            }

            pageTexts[url] = pageTexts.GetValueOrDefault(url, "") + "\n" + input.Text;
            if (!string.IsNullOrWhiteSpace(input.Category))
            {
                pageCategories[url] = TextTools.Clean(input.Category);
            }
        }

        var segments = SegmentAggregator.Aggregate(occurrences, texts.Count, guardOptions.Value.Segmentation);
        var evaluation = await segmentEvaluator.EvaluateAsync(
            segments, ruleSets, options.QuestionLanguage, options.Concurrency, options.ConfirmJevCalls, progress: null, ct);

        var engine = evaluation.Skipped
            ? new RuleEngineOutput()
            : RuleEngine.Evaluate(new RuleEngineInput
            {
                RuleSets = ruleSets,
                Labels = catalog.Labels,
                Segments = segments,
                PageTexts = pageTexts,
                LegalRequirements = catalog.LegalRequirements,
                PageCategories = pageCategories,
                Country = options.Country,
                EvaluateSitePresence = texts.Any(t => t.Kind == TextKind.Legal),
            });

        if (evaluation.Skipped)
        {
            warnings.Add("Vyhodnocení Jevem nebylo potvrzeno.");
        }

        return new AnalysisResult
        {
            Segments = segments,
            Findings = engine.Findings,
            RuleResults = engine.RuleResults,
            RuleSets = ruleSets.Select(Describe).ToList(),
            JevModel = evaluation.Model,
            Warnings = warnings,
            Stats = new ScanStats
            {
                SegmentOccurrences = occurrences.Count,
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

    /// <param name="Occurrences">Segments before deduplication.</param>
    /// <param name="SieveChunks">Chunks of the main text for the sieve, with their page.</param>
    /// <param name="Segments">Unique segments.</param>
    private sealed record Segmented(List<SegmentOccurrence> Occurrences, List<(string Url, SieveChunk Chunk)> SieveChunks, IReadOnlyList<Segment> Segments);

    private Segmented BuildSegments(IReadOnlyList<CrawledPage> pages, SieveDefinition? sieve)
    {
        var occurrences = new List<SegmentOccurrence>();
        var sieveChunks = new List<(string Url, SieveChunk Chunk)>();
        var analyzed = pages.Where(p => p.Info.IncludedInAnalysis).ToList();
        foreach (var page in analyzed)
        {
            // Legal pages are never sieved: missing information there is a finding of its own.
            var chunkChars = sieve is not null && page.Info.Type != PageType.Legal ? sieve.MaxChunkChars : (int?)null;
            var sentences = segmentBuilder.BuildSentences(page.Info.Url, page.Info.Type, page.Content, chunkChars);
            page.Info.SentenceCount = sentences.Count;
            occurrences.AddRange(sentences);
            if (chunkChars is { } chars)
            {
                sieveChunks.AddRange(SieveChunker.Chunk(page.Content.MainBlocks, chars).Select(c => (page.Info.Url, c)));
            }

            if (page.Info.Type == PageType.Legal)
            {
                var paragraphs = segmentBuilder.BuildLegalParagraphs(page.Info.Url, page.Content);
                page.Info.ParagraphCount = paragraphs.Count;
                occurrences.AddRange(paragraphs);
            }
        }

        return new Segmented(occurrences, sieveChunks, SegmentAggregator.Aggregate(occurrences, analyzed.Count, guardOptions.Value.Segmentation));
    }

    /// <summary>
    /// Estimate of the Jev calls: with the sieve, the sieve and all detailed questions (an upper bound: the sieve then leaves
    /// some out). The prepared sieve work is returned so that it is not prepared twice.
    /// </summary>
    private async Task<(JevCallEstimate, List<PageSieve.Prepared>?)> EstimateAsync(
        Segmented segmented, IReadOnlyList<RuleSet> ruleSets, SieveDefinition? sieve, List<string> sieveModules, string questionLanguage, CancellationToken ct)
    {
        var detail = await segmentEvaluator.EstimateAsync(segmented.Segments, ruleSets, questionLanguage, ct);
        if (sieve is null)
        {
            logger.LogInformation("Jev estimate: {Calls} calls ({Cached} more from cache), about {Tokens} input tokens, about {Cost} USD, mock {Mock}",
                detail.Calls, detail.CachedCalls, detail.EstimatedInputTokens, detail.EstimatedCostUsd, detail.IsMock);
            return (detail, null);
        }

        var (sieveEstimate, prepared) = await pageSieve.PrepareAsync(segmented.SieveChunks, sieve, sieveModules, questionLanguage, ct);
        var calls = sieveEstimate.Calls + detail.Calls;
        var tokens = sieveEstimate.EstimatedInputTokens + detail.EstimatedInputTokens;
        var estimate = new JevCallEstimate
        {
            Calls = calls,
            SieveCalls = sieveEstimate.Calls,
            CachedCalls = sieveEstimate.CachedCalls + detail.CachedCalls,
            EstimatedInputTokens = tokens,
            EstimatedCostUsd = Math.Round(segmentEvaluator.Cost(tokens), 6),
            IsMock = detail.IsMock,
            RequiresConfirmation = !detail.IsMock && calls > guardOptions.Value.Budget.MaxCallsWithoutConfirm,
            UpperBound = true,
        };
        logger.LogInformation("Jev estimate with sieve: at most {Calls} calls ({Sieve} of the sieve), about {Tokens} input tokens, at most {Cost} USD",
            estimate.Calls, estimate.SieveCalls, estimate.EstimatedInputTokens, estimate.EstimatedCostUsd);
        return (estimate, prepared);
    }

    /// <summary>The estimate with the new profiles the scan would write; their price above the limit needs confirmation too.</summary>
    private JevCallEstimate WithProfiles(JevCallEstimate estimate, ProfilingRun profiling) => new()
    {
        Calls = estimate.Calls,
        SieveCalls = estimate.SieveCalls,
        CachedCalls = estimate.CachedCalls,
        EstimatedInputTokens = estimate.EstimatedInputTokens,
        EstimatedCostUsd = estimate.EstimatedCostUsd,
        IsMock = estimate.IsMock,
        UpperBound = estimate.UpperBound || profiling.WillCreate,
        ProfileTemplates = profiling.Planned.Count,
        ProfileCostUsd = Math.Round(profiling.EstimatedUsd, 4),
        ProfilesWillRun = profiling.WillCreate,
        ProfilesUnavailableReason = profiling.Planned.Count > 0 ? profiling.UnavailableReason : null,
        RequiresConfirmation = estimate.RequiresConfirmation
            || (profiling.WillCreate && profiling.EstimatedUsd > guardOptions.Value.Profiles.MaxUsdWithoutConfirm),
    };

    /// <summary>
    /// The sieve runs and only the pairs it lets through are asked in detail. A sentence goes to a module when any chunk with
    /// it reaches the threshold or was not asked. The estimate was confirmed before.
    /// </summary>
    private async Task<(EvaluationSummary, SieveSummary?)> EvaluateWithSieveAsync(
        Segmented segmented, IReadOnlyList<RuleSet> ruleSets, SieveDefinition sieve, List<string> sieveModules,
        List<PageSieve.Prepared>? prepared, ScanOptions options, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        prepared ??= (await pageSieve.PrepareAsync(segmented.SieveChunks, sieve, sieveModules, options.QuestionLanguage, ct)).Work;
        var sieveSummary = await pageSieve.EvaluateAsync(prepared, options.Concurrency, progress, ct);
        var byChunk = sieveSummary.Chunks.ToDictionary(c => new SieveChunkRef(c.Url, c.Index), c => c.Probabilities);

        bool Include(Segment segment, RuleSet set)
        {
            if (segment.Kind != SegmentKind.Sentence || segment.SieveExempt || !sieveModules.Contains(set.Module))
            {
                return true;
            }

            var pass = segment.SieveChunks.Any(c =>
                byChunk.GetValueOrDefault(c) is not { } probabilities
                || !probabilities.TryGetValue(set.Module, out var p)
                || p >= sieve.Threshold);
            if (!pass)
            {
                segment.SkippedModules.Add(set.Module);
            }

            return pass;
        }

        var evaluation = await segmentEvaluator.EvaluateAsync(
            segmented.Segments, ruleSets, options.QuestionLanguage, options.Concurrency, confirm: null, progress, ct, Include);
        return (evaluation, sieveSummary);
    }

    /// <summary>Order of modules in reports; modules not listed here follow alphabetically.</summary>
    private static readonly string[] ModuleOrder = ["eco", "dur", "lr", "ucp", "legal"];

    private static List<RuleSet> SelectRuleSets(RuleCatalog catalog, IReadOnlyList<string> modules, string country, List<string> warnings)
    {
        if (modules.Count == 0)
        {
            // No explicit choice: every module with enabled rules for the country, without warnings for the others.
            modules = catalog.RuleSets
                .Where(s => s.Enabled && s.Jurisdictions.Contains(country))
                .Select(s => s.Module)
                .Distinct()
                .OrderBy(m => Array.IndexOf(ModuleOrder, m) is var i && i >= 0 ? i : ModuleOrder.Length)
                .ThenBy(m => m, StringComparer.Ordinal)
                .ToList();
        }

        var selected = new List<RuleSet>();
        foreach (var module in modules)
        {
            var sets = catalog.RuleSets.Where(s => s.Module == module && s.Jurisdictions.Contains(country)).ToList();
            if (sets.Count == 0)
            {
                var elsewhere = catalog.RuleSets.Where(s => s.Module == module).SelectMany(s => s.Jurisdictions).Distinct().Order().ToList();
                warnings.Add(elsewhere.Count > 0
                    ? $"Modul {module} má pravidla jen pro {string.Join(", ", elsewhere)}, pro zemi {country} se nespustil."
                    : $"Pro modul {module} neexistuje žádná sada pravidel, modul se nespustil.");
            }
            else if (!sets.Any(s => s.Enabled))
            {
                warnings.Add($"Sada pravidel modulu {module} pro zemi {country} je vypnutá ({string.Join(", ", sets.Select(s => s.SourceFile))}), modul se nespustil.");
            }

            selected.AddRange(sets.Where(s => s.Enabled));
        }

        return selected;
    }

    private static RuleSetInfo Describe(RuleSet set) => new()
    {
        Module = set.Module,
        Version = set.Version,
        File = set.SourceFile,
        QuestionIds = set.Questions.Keys.ToList(),
    };

    /// <summary>
    /// A page whose text was not in the HTML must never pass for a page without findings: the report says so,
    /// and when most of the site is like that, it says the whole result is incomplete.
    /// </summary>
    internal static IEnumerable<string> NotLoadedWarnings(int notLoaded, int pages)
    {
        if (notLoaded == 0)
        {
            return [];
        }

        var warnings = new List<string>
        {
            $"{notLoaded} z {pages} stažených stránek nemělo v HTML skoro žádný čitelný text; web je nejspíš vykresluje až JavaScriptem, "
            + "který nástroj nespouští. Na těchto stránkách je zkontrolovaný jen titulek, meta popis a popis z dat pro vyhledávače (JSON-LD); "
            + "to, že u nich nejsou nálezy, neznamená, že jsou v pořádku. Seznam je v části „Co nebylo zkontrolováno“.",
        };
        if (notLoaded * 2 >= pages)
        {
            warnings.Add("Web je z velké části vykreslovaný JavaScriptem, kontrola je proto neúplná. "
                + "Spolehlivý výsledek dá připojení e-shopu přes konektor nebo produktový feed.");
        }

        return warnings;
    }

    /// <summary>Everything a customer sees on the page, for page-level allowlists: text, image alt texts and file names.</summary>
    private static string PageText(PageInfo info, ExtractedPage content) =>
        string.Join("\n",
            new[] { content.Title, content.MetaDescription, content.JsonLdDescription }.OfType<string>()
                .Concat(content.MainBlocks.Select(b => b.Text))
                .Concat(content.ChromeRegions.SelectMany(r => r).Select(b => b.Text))
                .Concat(content.RestBlocks.Select(b => b.Text))
                .Concat(content.ProfileSkippedBlocks.Select(b => b.Text))
                .Concat(info.Images.Select(i => $"{i.Alt} {i.FileName}")));

    /// <summary>What a customer can see on the page, split into visible text, links and images, for site_signal rules.</summary>
    private static PageSignals Signals(PageInfo info, ExtractedPage content) =>
        new(
            PageText(info, content),
            string.Join("\n", content.Links.Select(l => $"{l.Text} {l.Url.AbsoluteUri}")),
            string.Join("\n", info.Images.Select(i => $"{i.Alt} {i.Src}")));

    private static List<ImageForReview> ImagesForReview(IEnumerable<PageInfo> pages, LabelConfiguration labels)
    {
        var keywords = labels.EcoImageKeywords
            .Select(k => TextTools.RemoveDiacritics(k).ToLowerInvariant())
            .Where(k => k.Length > 0)
            .ToList();
        var images = new List<ImageForReview>();
        var seen = new HashSet<string>();
        foreach (var page in pages)
        {
            foreach (var image in page.Images)
            {
                var haystack = TextTools.RemoveDiacritics($"{image.Alt} {image.FileName}").ToLowerInvariant();
                var keyword = keywords.FirstOrDefault(k => haystack.Contains(k, StringComparison.Ordinal));
                if (keyword is not null && seen.Add(page.Url + "\u001F" + image.Src))
                {
                    images.Add(new ImageForReview { PageUrl = page.Url, Src = image.Src, FileName = image.FileName, Alt = image.Alt, Keyword = keyword });
                }
            }
        }

        return images;
    }
}
