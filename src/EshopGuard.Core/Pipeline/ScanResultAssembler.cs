using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Profiles;
using EshopGuard.Core.Rules;

namespace EshopGuard.Core.Pipeline;

/// <summary>What the steps of one scan produced, for <see cref="ScanResultAssembler"/>.</summary>
internal sealed class ScanParts
{
    public required Uri SiteUrl { get; init; }

    public required ScanOptions Options { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public TimeSpan Duration { get; init; }

    public required RuleCatalog Catalog { get; init; }

    public required IReadOnlyList<RuleSet> RuleSets { get; init; }

    public SieveDefinition? Sieve { get; init; }

    public IReadOnlyList<string> SieveModules { get; init; } = [];

    public required CrawlSummary Crawl { get; init; }

    public required IReadOnlyList<ExtractedPageRecord> Pages { get; init; }

    public required IReadOnlyList<PageInfo> TextNotLoaded { get; init; }

    public required SegmentResult Segments { get; init; }

    public required ProfilePlan ProfilePlan { get; init; }

    public required ProfileCreateResult ProfilesCreated { get; init; }

    public required IReadOnlyDictionary<string, ProfileUse> ProfileUses { get; init; }

    public required EvaluationSummary Evaluation { get; init; }

    public SieveBatchResult? Sieved { get; init; }

    public IReadOnlyList<Finding> Findings { get; init; } = [];

    public required IReadOnlyList<string> Warnings { get; init; }
}

/// <summary>The crawl of a scan: the counters of all batches, the pace at the end and how long it took.</summary>
internal sealed record CrawlSummary(CrawlCounters Counters, PaceState Pace, TimeSpan Duration, IReadOnlyList<NotProcessedPage> NotProcessed);

/// <summary>
/// Composes the <see cref="ScanResult"/> and its <see cref="ScanStats"/> from the outputs of the steps, with the same
/// numbers as before the steps existed.
/// </summary>
internal static class ScanResultAssembler
{
    public static ScanResult Assemble(ScanParts parts, Func<long, decimal> jevCost)
    {
        var pages = parts.Pages;
        var crawl = parts.Crawl;
        var counters = crawl.Counters;
        var segments = parts.Segments.Segments;
        var evaluation = parts.Evaluation;
        var sieve = parts.Sieve;
        var sieved = parts.Sieved;
        var plan = parts.ProfilePlan;
        var created = parts.ProfilesCreated;
        var analyzed = pages.Where(p => p.Info.IncludedInAnalysis).ToList();
        var sievedSets = sieve is null ? 0 : parts.RuleSets.Count(s => s.AppliesTo == RuleValidator.Sentence && parts.SieveModules.Contains(s.Module));
        var inputTokens = evaluation.InputTokens + (sieved?.InputTokens ?? 0);

        var stats = new ScanStats
        {
            PagesFetched = pages.Count,
            PagesByType = pages.GroupBy(p => p.Info.Type).ToDictionary(g => g.Key, g => g.Count()),
            PagesFailed = counters.Failed,
            PagesBlockedByRobots = counters.RobotsBlocked.Count,
            PagesExcludedByFilter = counters.ExcludedByFilter,
            PagesOverLimit = counters.OverLimit,
            ProductPagesOverLimit = counters.ProductOverLimit,
            PagesWithReadability = pages.Count(p => p.Info.Extraction == ExtractionMethod.Readability),
            PagesWithFallback = pages.Count(p => p.Info.Extraction == ExtractionMethod.Fallback),
            PagesTextNotLoaded = parts.TextNotLoaded.Count,
            VisibleTextChars = pages.Sum(p => (long)p.Info.VisibleTextChars),
            CheckedTextChars = pages.Sum(p => (long)Math.Min(p.Info.CheckedTextChars, p.Info.VisibleTextChars)),
            NavigationTextChars = pages.Sum(p => (long)p.Info.NavigationTextChars),
            ListingTextChars = pages.Sum(p => (long)p.Info.ListingTextChars),
            PagesWithUncheckedText = pages.Count(p => p.Info.HasUncheckedText),
            CrawlSeconds = Math.Round(crawl.Duration.TotalSeconds, 1),
            CrawlRequests = counters.Requests,
            CrawlFinalRate = Math.Round(crawl.Pace.Rate, 2),
            CrawlThrottled = crawl.Pace.Throttled,
            CrawlDelaySeconds = counters.CrawlDelaySeconds ?? 0,
            SegmentOccurrences = parts.Segments.OccurrenceCount,
            UniqueSegments = segments.Count,
            SentenceSegments = segments.Count(s => s.Kind == SegmentKind.Sentence),
            LegalParagraphSegments = segments.Count(s => s.Kind == SegmentKind.LegalParagraph),
            BoilerplateSegments = segments.Count(s => s.Boilerplate),
            JevCalls = evaluation.Calls,
            JevCacheHits = evaluation.CacheHits,
            JevErrors = evaluation.Errors,
            InputTokens = inputTokens,
            EstimatedCostUsd = Math.Round(jevCost(inputTokens), 6),
            SieveEnabled = sieve is not null,
            SieveThreshold = sieve?.Threshold ?? 0,
            SieveChunkChars = sieve?.MaxChunkChars ?? 0,
            SieveChunks = sieved?.Chunks.Count ?? 0,
            SieveCalls = sieved?.Calls ?? 0,
            SieveCacheHits = sieved?.CacheHits ?? 0,
            SieveErrors = sieved?.Errors ?? 0,
            SieveTooLong = sieved?.TooLong ?? 0,
            SievePairs = sieved is null ? 0 : segments.Count(s => s.Kind == SegmentKind.Sentence) * sievedSets,
            SieveSkippedPairs = segments.Sum(s => s.SkippedModules.Count),
            ProfilesEnabled = !plan.Disabled,
            ProfilesUsed = parts.ProfileUses.Count,
            ProfilesCreated = created.Created.Count,
            ProfilesPlanned = plan.Planned.Count,
            PagesWithProfile = pages.Count(p => p.Info.ProfileId is not null),
            PagesWithoutProfile = pages.Count(p => p.ProfileEligible && p.Fit is null),
            ProfileSkippedTextChars = pages.Sum(p => (long)p.Info.ProfileSkippedTextChars),
            ProfileCalls = created.Calls,
            ProfileInputTokens = created.InputTokens,
            ProfileOutputTokens = created.OutputTokens,
            ProfileCostUsd = Math.Round(created.CostUsd, 6),
            DurationSeconds = Math.Round(parts.Duration.TotalSeconds, 1),
        };

        return new ScanResult
        {
            SiteUrl = parts.SiteUrl.AbsoluteUri,
            StartedAt = parts.StartedAt,
            FinishedAt = DateTimeOffset.UtcNow,
            Modules = parts.Options.Modules,
            Country = parts.Options.Country,
            QuestionLanguage = parts.Options.QuestionLanguage,
            Pages = pages.Select(p => p.Info).ToList(),
            Segments = segments,
            SieveChunks = sieved?.Chunks ?? [],
            Findings = parts.Findings,
            ImagesForReview = ImagesForReview(analyzed.Select(p => p.Info), parts.Catalog.Labels),
            RuleSets = parts.RuleSets.Select(RulesStep.Describe).ToList(),
            JevModel = evaluation.Model,
            EvaluationSkipped = evaluation.Skipped,
            UncheckedDocuments = counters.UncheckedDocuments,
            RobotsBlockedUrls = counters.RobotsBlocked,
            BlockedUrls = counters.SsrfBlocked,
            NotProcessedPages = crawl.NotProcessed,
            Profiles = parts.ProfileUses.Values.OrderBy(u => u.Profile.CreatedAt).ToList(),
            Warnings = parts.Warnings,
            Stats = stats,
        };
    }

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
