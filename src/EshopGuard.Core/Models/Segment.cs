using System.Text.Json.Serialization;

namespace EshopGuard.Core.Models;

/// <summary>
/// Kind of a segment. Rules declare which kind they apply to.
/// </summary>
public enum SegmentKind
{
    /// <summary>A sentence with its context, evaluated by the eco module.</summary>
    Sentence,

    /// <summary>A paragraph of a legal page prefixed with its nearest heading, evaluated by the legal module.</summary>
    LegalParagraph,
}

/// <summary>
/// Where on the page a segment comes from.
/// </summary>
public enum SegmentSource
{
    /// <summary>Main text of the page.</summary>
    Main,

    /// <summary>Page frame: header, footer or aside.</summary>
    Chrome,

    /// <summary>The <c>title</c> element.</summary>
    Title,

    /// <summary>The meta description.</summary>
    MetaDescription,

    /// <summary>Product description from JSON-LD.</summary>
    JsonLd,

    /// <summary>
    /// Other visible text of the page: outside the main text chosen by SmartReader, the frame and the navigation,
    /// for example a product box with badges, price and delivery.
    /// </summary>
    Rest,
}

/// <summary>
/// A unique segment: the same text with the same context is evaluated once and remembers every page it occurs on.
/// </summary>
public sealed class Segment
{
    /// <summary>SHA-256 of kind, normalized text and normalized context, prefixed with <c>sha256:</c>.</summary>
    public required string Hash { get; init; }

    /// <summary>Segment kind.</summary>
    public required SegmentKind Kind { get; init; }

    /// <summary>Text of the sentence or paragraph as found on the page.</summary>
    public required string Text { get; init; }

    /// <summary>Preceding sentences from the same part of the page (empty for legal paragraphs).</summary>
    public string ContextBefore { get; init; } = "";

    /// <summary>Following sentences from the same part of the page (empty for legal paragraphs).</summary>
    public string ContextAfter { get; init; } = "";

    /// <summary>Parts of pages where the segment occurs.</summary>
    public IReadOnlyList<SegmentSource> Sources { get; init; } = [];

    /// <summary>Types of pages where the segment occurs.</summary>
    public IReadOnlyList<PageType> PageTypes { get; init; } = [];

    /// <summary>Pages where the segment occurs, in the order they were found.</summary>
    public IReadOnlyList<string> Urls { get; init; } = [];

    /// <summary>Number of pages where the segment occurs.</summary>
    public int Occurrences => Urls.Count;

    /// <summary>True when the text is on so many pages that it is page frame or menu.</summary>
    public bool Boilerplate { get; init; }

    /// <summary>Probabilities of "yes" by <see cref="Rules.QuestionKey"/> (<c>{rule set}:{question id}</c>), filled by the evaluation.</summary>
    public Dictionary<string, double> Probabilities { get; init; } = [];

    /// <summary>Sieve chunks of the main text the segment occurs in.</summary>
    public IReadOnlyList<SieveChunkRef> SieveChunks { get; init; } = [];

    /// <summary>
    /// True when the segment also occurs outside a sieved main text (page frame, title, meta description, JSON-LD,
    /// legal page, or the sieve is off): the sieve never skips it.
    /// </summary>
    public bool SieveExempt { get; init; } = true;

    /// <summary>Modules whose questions the sieve left out for this segment (no chunk with it was flagged).</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public HashSet<string> SkippedModules { get; } = [];

    private long? _fingerprint;

    /// <summary>
    /// 64-bit fingerprint of the text alone (without context, case and spacing): the same sentence on other pages or in
    /// another language version of the shop has the same fingerprint.
    /// </summary>
    [JsonIgnore]
    public long Fingerprint => _fingerprint ??= Segmentation.SentenceFingerprint.Of(Text);
}

/// <summary>
/// A sieve chunk: page and position of the chunk on the page.
/// </summary>
public readonly record struct SieveChunkRef(string Url, int Chunk);

/// <summary>
/// Answer of the sieve for one chunk of the main text of a page.
/// </summary>
public sealed class SieveChunkResult
{
    /// <summary>Page of the chunk.</summary>
    public required string Url { get; init; }

    /// <summary>Position of the chunk on the page, from 0.</summary>
    public required int Index { get; init; }

    /// <summary>Text of the chunk as sent to Jev.</summary>
    public required string Text { get; init; }

    /// <summary>Probability of the topic by module; null when the chunk was not asked (error, too long).</summary>
    public IReadOnlyDictionary<string, double>? Probabilities { get; init; }

    /// <summary><c>ok</c>, <c>cache</c>, <c>error</c> or <c>too_long</c>.</summary>
    public required string Status { get; init; }
}
