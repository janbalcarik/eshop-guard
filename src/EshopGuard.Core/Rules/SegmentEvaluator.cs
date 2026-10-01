using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using EshopGuard.Core.Cache;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Rules;

/// <summary>
/// Sentence with its context as sent to Jev: <c>{"sentence", "context_before", "context_after"}</c>.
/// </summary>
internal sealed record SentenceState(string Sentence, string ContextBefore, string ContextAfter);

/// <summary>
/// Summary of the Jev evaluation of one run.
/// </summary>
internal sealed class EvaluationSummary
{
    public static EvaluationSummary None { get; } = new();

    public JevCallEstimate? Estimate { get; init; }

    public bool Skipped { get; init; }

    /// <summary>Requests sent to Jev, one per segment with the questions of all its rule sets (cache hits excluded).</summary>
    public int Calls { get; init; }

    /// <summary>Answers of one rule set for one segment taken from the cache.</summary>
    public int CacheHits { get; init; }

    public int Errors { get; init; }

    public long InputTokens { get; init; }

    public string? Model { get; init; }
}

/// <summary>
/// Sends every segment to Jev in one request with the questions of all applicable rule sets (TypeSafe recommends
/// batching questions: the answers do not change and the request is sent once instead of once per module) and
/// stores the probabilities of "yes" in <see cref="Segment.Probabilities"/>. The cache keeps the answers per rule set,
/// so a new version of one set asks again only its own questions.
/// </summary>
internal sealed class SegmentEvaluator(
    IOptions<EshopGuardOptions> options,
    IJevCache cache,
    ILogger<SegmentEvaluator> logger,
    IJevClient? client = null)
{
    private static readonly JsonSerializerOptions StateJson = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    /// <summary>Estimate of the requests for every segment and applicable rule set (without the sieve), cache hits excluded.</summary>
    public async Task<JevCallEstimate> EstimateAsync(
        IReadOnlyList<Segment> segments, IReadOnlyList<RuleSet> ruleSets, string questionLanguage, CancellationToken ct)
    {
        var work = BuildWork(segments, ruleSets, questionLanguage, include: null);
        var uncached = new List<WorkItem>();
        var cached = 0;
        foreach (var item in work)
        {
            if (await cache.GetAsync(item.CacheKey, ct) is null)
            {
                uncached.Add(item);
            }
            else
            {
                cached++;
            }
        }

        return Estimate(Group(uncached), cached);
    }

    /// <summary>
    /// Asks Jev for every segment and applicable rule set; with <c>include</c>, only the pairs it returns true for (the sieve).
    /// </summary>
    public async Task<EvaluationSummary> EvaluateAsync(
        IReadOnlyList<Segment> segments,
        IReadOnlyList<RuleSet> ruleSets,
        string questionLanguage,
        int? concurrency,
        Func<JevCallEstimate, CancellationToken, Task<bool>>? confirm,
        IProgress<ScanProgress>? progress,
        CancellationToken ct,
        Func<Segment, RuleSet, bool>? include = null)
    {
        var work = BuildWork(segments, ruleSets, questionLanguage, include);
        if (work.Count == 0)
        {
            return EvaluationSummary.None;
        }

        if (client is null)
        {
            throw new InvalidOperationException("Není zaregistrovaný klient Jevu (IJevClient). Zapněte Jev.UseMock nebo zaregistrujte vlastního klienta.");
        }

        var cached = new List<(Segment Segment, JevResult Result)>();
        var uncached = new List<WorkItem>();
        foreach (var item in work)
        {
            var hit = await cache.GetAsync(item.CacheKey, ct);
            if (hit is null)
            {
                uncached.Add(item);
            }
            else
            {
                cached.Add((item.Segment, hit));
            }
        }

        var pending = Group(uncached);
        var estimate = Estimate(pending, cached.Count);
        logger.LogInformation("Jev estimate: {Calls} calls ({Cached} more from cache), about {Tokens} input tokens, about {Cost} USD, mock {Mock}",
            estimate.Calls, estimate.CachedCalls, estimate.EstimatedInputTokens, estimate.EstimatedCostUsd, estimate.IsMock);
        if (confirm is not null && !await confirm(estimate, ct))
        {
            logger.LogWarning("Evaluation skipped: the estimate was not confirmed");
            return new EvaluationSummary { Estimate = estimate, Skipped = true };
        }

        var results = new ConcurrentBag<(Segment Segment, JevResult Result)>();
        var completed = 0;
        var errors = 0;
        long tokens = 0;
        JevApiException? fatal = null;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var semaphore = new SemaphoreSlim(Math.Max(1, concurrency ?? options.Value.Jev.Concurrency));

        var tasks = pending.Select(async request =>
        {
            try
            {
                await semaphore.WaitAsync(stop.Token);
                try
                {
                    var result = await client.EvaluateAsync(request.State, request.Questions, stop.Token);
                    Interlocked.Add(ref tokens, result.Usage.InputTokens);
                    results.Add((request.Segment, result));
                    foreach (var item in request.Items)
                    {
                        var own = new JevResult
                        {
                            Model = result.Model,
                            Answers = result.Answers.Where(a => item.Questions.ContainsKey(a.Key)).ToDictionary(a => a.Key, a => a.Value),
                            Usage = result.Usage,
                        };
                        await cache.SetAsync(item.CacheKey, own, ct);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }
            catch (JevApiException ex) when (ex.IsFatal)
            {
                // A rejected key or missing credit fails every request; stop instead of sending the rest.
                Interlocked.CompareExchange(ref fatal, ex, null);
                await stop.CancelAsync();
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Cancelled because another request hit a fatal error.
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Interlocked.Increment(ref errors);
                logger.LogWarning(ex, "Jev evaluation of segment {Hash} ({Modules}) failed", request.Segment.Hash, string.Join(", ", request.Items.Select(i => i.Module)));
            }
            finally
            {
                var done = Interlocked.Increment(ref completed);
                progress?.Report(new ScanProgress { Stage = ScanStage.Evaluation, Completed = done, Total = pending.Count });
            }
        });
        await Task.WhenAll(tasks);

        if (fatal is not null)
        {
            ExceptionDispatchInfo.Capture(fatal).Throw();
        }

        // Probabilities are merged after all requests finish, so no two requests write into one dictionary.
        foreach (var (segment, result) in cached.Concat(results))
        {
            foreach (var (question, answer) in result.Answers)
            {
                if (answer.Noul is { } probability)
                {
                    segment.Probabilities[question] = probability;
                }
            }
        }

        var modelName = results.Select(r => r.Result.Model).Concat(cached.Select(c => c.Result.Model)).FirstOrDefault(m => m is not null);
        logger.LogInformation("Jev evaluation finished: {Calls} calls, {Hits} from cache, {Errors} errors, {Tokens} input tokens, model {Model}",
            pending.Count, cached.Count, errors, tokens, modelName);
        return new EvaluationSummary
        {
            Estimate = estimate,
            Calls = pending.Count,
            CacheHits = cached.Count,
            Errors = errors,
            InputTokens = tokens,
            Model = modelName,
        };
    }

    public decimal Cost(long inputTokens) => inputTokens * options.Value.Cost.UsdPerMillionInputTokens / 1_000_000m;

    private List<WorkItem> BuildWork(IReadOnlyList<Segment> segments, IReadOnlyList<RuleSet> ruleSets, string questionLanguage, Func<Segment, RuleSet, bool>? include)
    {
        var questions = ruleSets.ToDictionary(s => s, s => BuildQuestions(s, questionLanguage));
        var model = options.Value.Jev.Model;
        return (from set in ruleSets
                from segment in segments
                where segment.Kind == (set.AppliesTo == RuleValidator.Sentence ? SegmentKind.Sentence : SegmentKind.LegalParagraph)
                where include is null || include(segment, set)
                let state = BuildState(segment)
                select new WorkItem(segment, questions[set], state, JevCacheKey.Create(model, set.Version, questionLanguage, questions[set], state), set.Module))
            .ToList();
    }

    // Question ids are unique across the rule sets of one country (RuleValidator), so they can share a request.
    private static List<Request> Group(List<WorkItem> items) =>
        items.GroupBy(item => item.Segment).Select(g => new Request(g.Key, g.First().State, g.ToList())).ToList();

    private JevCallEstimate Estimate(List<Request> pending, int cachedCalls)
    {
        // About 2.6 characters per token, measured on the fixture shop with jev-1.13.0 (Czech text, English questions);
        // only an estimate for the budget check, the report uses the tokens Jev bills.
        var characters = pending.Sum(request =>
            (request.State as string ?? JsonSerializer.Serialize(request.State, StateJson)).Length
            + request.Questions.Sum(q => (q.Value.Instructions.ToString()?.Length ?? 0) + q.Key.Length + 20));
        var tokens = (long)Math.Ceiling(characters / 2.6);
        var isMock = client is MockJevClient;
        return new JevCallEstimate
        {
            Calls = pending.Count,
            CachedCalls = cachedCalls,
            EstimatedInputTokens = tokens,
            EstimatedCostUsd = Math.Round(Cost(tokens), 6),
            IsMock = isMock,
            RequiresConfirmation = !isMock && pending.Count > options.Value.Budget.MaxCallsWithoutConfirm,
        };
    }

    private static object BuildState(Segment segment) =>
        segment.Kind == SegmentKind.Sentence
            ? new SentenceState(segment.Text, segment.ContextBefore, segment.ContextAfter)
            : segment.Text;

    private static Dictionary<string, JevQuestion> BuildQuestions(RuleSet set, string language) =>
        set.Questions.ToDictionary(
            q => q.Key,
            q => new JevQuestion
            {
                Type = q.Value.Type switch
                {
                    "choice" => "choice",
                    "score" => "score",
                    _ => "noul",
                },
                Instructions = language == "cs" ? q.Value.TextCs : q.Value.TextEn,
                Criteria = q.Value.Type switch
                {
                    "choice" => q.Value.Options,
                    "score" => q.Value.Levels,
                    _ => null,
                },
            });

    private sealed record WorkItem(Segment Segment, Dictionary<string, JevQuestion> Questions, object State, string CacheKey, string Module);

    /// <summary>One request to Jev: a segment with the questions of all its rule sets that are not in the cache.</summary>
    private sealed record Request(Segment Segment, object State, List<WorkItem> Items)
    {
        public Dictionary<string, JevQuestion> Questions { get; } = Items.SelectMany(i => i.Questions).ToDictionary(q => q.Key, q => q.Value);
    }
}
