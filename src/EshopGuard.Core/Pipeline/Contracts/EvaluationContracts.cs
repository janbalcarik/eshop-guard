using EshopGuard.Core.Models;

namespace EshopGuard.Core.Pipeline;

/// <summary>Input of <see cref="EstimateStep"/>: segments, sieved modules (empty without the sieve), language and planned profiles.</summary>
internal sealed record EstimateInput(SegmentResult Segments, IReadOnlyList<string> SieveModules, string QuestionLanguage, ProfilePlan Profiles) : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;
}

/// <summary>
/// Output of <see cref="EstimateStep"/>: the estimate of Jev calls and new profiles, and whether the host is asked before
/// anything is paid (the sieve is on, some segment is evaluated or a profile would be written).
/// </summary>
internal sealed record RunEstimate(JevCallEstimate Estimate, bool AsksConfirmation) : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;
}

/// <summary>Input of one batch of <see cref="SieveStep"/>.</summary>
internal sealed record SieveBatchInput(IReadOnlyList<SieveChunkInput> Chunks, IReadOnlyList<string> Modules, string QuestionLanguage, int? Concurrency) : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;
}

/// <summary>Output of one batch of <see cref="SieveStep"/>: the answer for every chunk and what was paid.</summary>
internal sealed record SieveBatchResult(IReadOnlyList<SieveChunkResult> Chunks, int Calls, int CacheHits, int Errors, int TooLong, long InputTokens) : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;

    /// <summary>Of <see cref="Errors"/>, failures that may pass on a later attempt (<see cref="ServiceErrors"/>).</summary>
    public int TransientErrors { get; init; }
}

/// <summary>Input of one batch of <see cref="EvaluateStep"/>.</summary>
internal sealed record EvaluateBatchInput(IReadOnlyList<SegmentState> Segments, string QuestionLanguage, int? Concurrency) : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;
}

/// <summary>
/// Output of one batch of <see cref="EvaluateStep"/>: counts, the probabilities of "yes" by segment hash and question id,
/// and the segments whose request failed (they stay without an answer and are reported).
/// </summary>
internal sealed record EvaluateBatchResult(
    int Calls,
    int CacheHits,
    int Errors,
    long InputTokens,
    string? Model,
    IReadOnlyDictionary<string, Dictionary<string, double>> Probabilities,
    IReadOnlyList<string> NotEvaluated) : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;

    /// <summary>Of <see cref="Errors"/>, failures that may pass on a later attempt (<see cref="ServiceErrors"/>).</summary>
    public int TransientErrors { get; init; }

    public static EvaluateBatchResult Empty { get; } = new(0, 0, 0, 0, null, new Dictionary<string, Dictionary<string, double>>(), []);
}
