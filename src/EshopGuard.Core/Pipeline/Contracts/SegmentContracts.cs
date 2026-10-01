using EshopGuard.Core.Models;
using EshopGuard.Core.Segmentation;

namespace EshopGuard.Core.Pipeline;

/// <summary>Input of <see cref="SegmentStep"/>: the pages of the scan and the size of the sieve chunks (null without the sieve).</summary>
internal sealed record SegmentInput(IReadOnlyList<ExtractedPageRecord> Pages, int? SieveChunkChars) : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;
}

/// <summary>A chunk of the main text of a page for the sieve.</summary>
internal sealed record SieveChunkInput(string Url, SieveChunk Chunk);

/// <summary>Counts of one page after segmentation and the sorted unique fingerprints of its sentences and paragraphs.</summary>
/// <param name="Url">Final URL of the page.</param>
/// <param name="SentenceCount">Sentence segments of the page before deduplication.</param>
/// <param name="ParagraphCount">Legal paragraphs, only for legal pages.</param>
/// <param name="Fingerprints">Sorted unique <see cref="Segmentation.SentenceFingerprint"/> of its segments.</param>
internal sealed record PageSegmentation(string Url, int SentenceCount, int? ParagraphCount, IReadOnlyList<long> Fingerprints);

/// <summary>Output of <see cref="SegmentStep"/>.</summary>
/// <param name="OccurrenceCount">Segments before deduplication.</param>
/// <param name="Segments">Unique segments in the order they were found.</param>
/// <param name="SieveChunks">Chunks of the main text for the sieve, with their page.</param>
/// <param name="Pages">Counts by page, for the analyzed pages in their order.</param>
internal sealed record SegmentResult(
    int OccurrenceCount,
    IReadOnlyList<Segment> Segments,
    IReadOnlyList<SieveChunkInput> SieveChunks,
    IReadOnlyList<PageSegmentation> Pages) : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;
}

/// <summary>
/// A unique segment as a batch of <see cref="EvaluateStep"/> needs it: what is sent to Jev and the modules whose questions
/// are asked (after the sieve).
/// </summary>
internal sealed record SegmentState(string Hash, SegmentKind Kind, string Text, string ContextBefore, string ContextAfter, IReadOnlyList<string> Modules);
