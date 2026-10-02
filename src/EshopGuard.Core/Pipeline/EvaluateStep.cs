using EshopGuard.Core.Models;
using EshopGuard.Core.Rules;

namespace EshopGuard.Core.Pipeline;

/// <summary>
/// Asks Jev for every segment of a batch with the questions of its modules, one request per segment, answers from the cache
/// first. Every answer is written to the cache as soon as it arrives, so a batch started again after a crash only asks what
/// is missing; a fatal error (rejected key, no credit) stops the batch and the answers stay.
/// </summary>
internal sealed class EvaluateStep(SegmentEvaluator segmentEvaluator)
{
    public async Task<EvaluateBatchResult> EvaluateAsync(
        EvaluateBatchInput input, IReadOnlyList<RuleSet> ruleSets, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var segments = input.Segments.Select(s => new Segment { Hash = s.Hash, Kind = s.Kind, Text = s.Text, ContextBefore = s.ContextBefore, ContextAfter = s.ContextAfter }).ToList();
        var modules = input.Segments.ToDictionary(s => s.Hash, s => s.Modules);
        var summary = await segmentEvaluator.EvaluateAsync(
            segments, ruleSets, input.QuestionLanguage, input.Concurrency, confirm: null, progress, ct,
            (segment, set) => modules[segment.Hash].Contains(set.Module));
        var probabilities = segments.Where(s => s.Probabilities.Count > 0).ToDictionary(s => s.Hash, s => s.Probabilities);
        return new EvaluateBatchResult(summary.Calls, summary.CacheHits, summary.Errors, summary.InputTokens, summary.Model, probabilities, summary.NotEvaluated)
        {
            TransientErrors = summary.TransientErrors,
        };
    }

    /// <summary>Writes the probabilities of a batch into the segments of the run.</summary>
    public static void Apply(EvaluateBatchResult result, IEnumerable<Segment> segments)
    {
        foreach (var segment in segments)
        {
            if (result.Probabilities.TryGetValue(segment.Hash, out var probabilities))
            {
                foreach (var (question, probability) in probabilities)
                {
                    segment.Probabilities[question] = probability;
                }
            }
        }
    }
}
