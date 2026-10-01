using EshopGuard.Core.Models;
using EshopGuard.Core.Rules;

namespace EshopGuard.Core.Pipeline;

/// <summary>
/// Asks Jev the topic question of every sieved module for every chunk of a batch (one request per chunk, answers from the
/// cache first). A chunk that could not be asked keeps no probabilities and its sentences go to every module.
/// </summary>
internal sealed class SieveStep(PageSieve pageSieve)
{
    public async Task<SieveBatchResult> SieveAsync(SieveBatchInput input, SieveDefinition sieve, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var (_, work) = await pageSieve.PrepareAsync(input.Chunks.Select(c => (c.Url, c.Chunk)).ToList(), sieve, input.Modules, input.QuestionLanguage, ct);
        var summary = await pageSieve.EvaluateAsync(work, input.Concurrency, progress, ct);
        return new SieveBatchResult(summary.Chunks, summary.Calls, summary.CacheHits, summary.Errors, summary.TooLong, summary.InputTokens);
    }

    /// <summary>
    /// The modules whose questions each segment gets after the sieve: a sentence goes to a sieved module when any chunk with
    /// it reaches the threshold or was not asked; modules the sieve left out are added to <see cref="Segment.SkippedModules"/>.
    /// Without the sieve every segment gets every module of its kind.
    /// </summary>
    public static List<SegmentState> States(
        IReadOnlyList<Segment> segments, IReadOnlyList<RuleSet> ruleSets, SieveDefinition? sieve, IReadOnlyList<string> sieveModules,
        IReadOnlyList<SieveChunkResult>? chunks)
    {
        var byChunk = chunks?.ToDictionary(c => new SieveChunkRef(c.Url, c.Index), c => c.Probabilities) ?? [];
        bool Include(Segment segment, RuleSet set)
        {
            if (sieve is null || segment.Kind != SegmentKind.Sentence || segment.SieveExempt || !sieveModules.Contains(set.Module))
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

        // The same order as the evaluation: rule set by rule set, segment by segment.
        var modules = segments.ToDictionary(s => s, _ => new List<string>());
        foreach (var set in ruleSets)
        {
            var kind = set.AppliesTo == RuleValidator.Sentence ? SegmentKind.Sentence : SegmentKind.LegalParagraph;
            foreach (var segment in segments)
            {
                if (segment.Kind == kind && Include(segment, set) && !modules[segment].Contains(set.Module))
                {
                    modules[segment].Add(set.Module);
                }
            }
        }

        return segments.Select(s => new SegmentState(s.Hash, s.Kind, s.Text, s.ContextBefore, s.ContextAfter, modules[s])).ToList();
    }
}
