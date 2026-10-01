using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using EshopGuard.Core.Cache;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Segmentation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Rules;

/// <summary>
/// Result of the sieve for the whole scan.
/// </summary>
internal sealed class SieveSummary
{
    public IReadOnlyList<SieveChunkResult> Chunks { get; init; } = [];

    public int Calls { get; init; }

    public int CacheHits { get; init; }

    public int Errors { get; init; }

    public int TooLong { get; init; }

    public long InputTokens { get; init; }
}

/// <summary>
/// Asks Jev the topic question of every sieved module for every chunk of the main text, one request per chunk.
/// A chunk that could not be asked (error, longer than <see cref="SieveDefinition.MaxRequestChars"/>) has no
/// probabilities, and its sentences then go to every module: the sieve never hides a sentence because of a failure.
/// </summary>
internal sealed class PageSieve(
    IOptions<EshopGuardOptions> options,
    IJevCache cache,
    ILogger<PageSieve> logger,
    IJevClient? client = null)
{
    /// <summary>Chunks to send and their cache keys; answers already in the cache are read here.</summary>
    public async Task<(JevCallEstimate Estimate, List<Prepared> Work)> PrepareAsync(
        IReadOnlyList<(string Url, SieveChunk Chunk)> chunks, SieveDefinition sieve, IReadOnlyList<string> modules,
        string language, CancellationToken ct)
    {
        var questions = modules.ToDictionary(
            SieveDefinition.QuestionId,
            m => new JevQuestion { Type = "noul", Instructions = language == "cs" ? sieve.Questions[m].TextCs : sieve.Questions[m].TextEn });
        var work = new List<Prepared>();
        long characters = 0;
        var calls = 0;
        var cached = 0;
        foreach (var (url, chunk) in chunks)
        {
            if (chunk.Text.Length > sieve.MaxRequestChars)
            {
                work.Add(new Prepared(url, chunk, questions, null, null));
                continue;
            }

            var key = JevCacheKey.Create(options.Value.Jev.Model, sieve.Version, language, questions, chunk.Text);
            var hit = await cache.GetAsync(key, ct);
            work.Add(new Prepared(url, chunk, questions, key, hit));
            if (hit is null)
            {
                calls++;
                characters += chunk.Text.Length + questions.Sum(q => (q.Value.Instructions.ToString()?.Length ?? 0) + q.Key.Length + 20);
            }
            else
            {
                cached++;
            }
        }

        // The same 2.6 characters per token as the estimate of the detailed questions.
        var tokens = (long)Math.Ceiling(characters / 2.6);
        var estimate = new JevCallEstimate
        {
            Calls = calls,
            CachedCalls = cached,
            EstimatedInputTokens = tokens,
            EstimatedCostUsd = Math.Round(tokens * options.Value.Cost.UsdPerMillionInputTokens / 1_000_000m, 6),
            IsMock = client is MockJevClient,
        };
        return (estimate, work);
    }

    public async Task<SieveSummary> EvaluateAsync(List<Prepared> work, int? concurrency, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        if (client is null)
        {
            throw new InvalidOperationException("Není zaregistrovaný klient Jevu (IJevClient). Zapněte Jev.UseMock nebo zaregistrujte vlastního klienta.");
        }

        var pending = work.Where(w => w.CacheKey is not null && w.Cached is null).ToList();
        var answers = new ConcurrentDictionary<Prepared, JevResult>();
        var errors = 0;
        var completed = 0;
        long tokens = 0;
        JevApiException? fatal = null;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var semaphore = new SemaphoreSlim(Math.Max(1, concurrency ?? options.Value.Jev.Concurrency));
        var tasks = pending.Select(async item =>
        {
            try
            {
                await semaphore.WaitAsync(stop.Token);
                try
                {
                    var result = await client.EvaluateAsync(item.Chunk.Text, item.Questions, stop.Token);
                    Interlocked.Add(ref tokens, result.Usage.InputTokens);
                    answers[item] = result;
                    await cache.SetAsync(item.CacheKey!, result, ct);
                }
                finally
                {
                    semaphore.Release();
                }
            }
            catch (JevApiException ex) when (ex.IsFatal)
            {
                Interlocked.CompareExchange(ref fatal, ex, null);
                await stop.CancelAsync();
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Cancelled because another request hit a fatal error.
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Fail safe: the chunk keeps no probabilities and all its sentences are evaluated in detail.
                Interlocked.Increment(ref errors);
                logger.LogWarning(ex, "Sieve request for chunk {Chunk} of {Url} failed; its sentences are evaluated in detail", item.Chunk.Index, item.Url);
            }
            finally
            {
                var done = Interlocked.Increment(ref completed);
                progress?.Report(new ScanProgress { Stage = ScanStage.Sieve, Completed = done, Total = pending.Count });
            }
        });
        await Task.WhenAll(tasks);
        if (fatal is not null)
        {
            ExceptionDispatchInfo.Capture(fatal).Throw();
        }

        var chunks = work.Select(item =>
        {
            var result = item.Cached ?? answers.GetValueOrDefault(item);
            var probabilities = result?.Answers
                .Where(a => a.Value.Noul is not null)
                .ToDictionary(a => a.Key["sieve_".Length..], a => a.Value.Noul!.Value);
            var status = item.CacheKey is null ? "too_long" : item.Cached is not null ? "cache" : result is null ? "error" : "ok";
            return new SieveChunkResult { Url = item.Url, Index = item.Chunk.Index, Text = item.Chunk.Text, Probabilities = probabilities, Status = status };
        }).ToList();

        logger.LogInformation("Sieve finished: {Chunks} chunks, {Calls} calls, {Cached} from cache, {Errors} errors, {TooLong} too long, {Tokens} input tokens",
            chunks.Count, pending.Count, work.Count(w => w.Cached is not null), errors, work.Count(w => w.CacheKey is null), tokens);
        return new SieveSummary
        {
            Chunks = chunks,
            Calls = pending.Count,
            CacheHits = work.Count(w => w.Cached is not null),
            Errors = errors,
            TooLong = work.Count(w => w.CacheKey is null),
            InputTokens = tokens,
        };
    }

    /// <summary>A chunk ready for the sieve; <see cref="CacheKey"/> is null for a chunk that is too long to send.</summary>
    internal sealed record Prepared(
        string Url, SieveChunk Chunk, Dictionary<string, JevQuestion> Questions, string? CacheKey, JevResult? Cached);
}
