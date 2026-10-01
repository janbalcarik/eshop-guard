using System.Text;
using EshopGuard.Core.Extract;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Segmentation;

/// <summary>
/// One occurrence of a segment on one page, before deduplication. <c>Chunk</c> is the sieve chunk of the main text
/// the sentence belongs to; null outside the main text or without the sieve.
/// </summary>
internal sealed record SegmentOccurrence(
    SegmentKind Kind,
    string Text,
    string ContextBefore,
    string ContextAfter,
    string Url,
    PageType PageType,
    SegmentSource Source,
    int? Chunk = null);

/// <summary>
/// A part of a page whose sentences are context for each other.
/// </summary>
internal sealed record TextRegion(SegmentSource Source, IReadOnlyList<TextBlock> Blocks);

/// <summary>
/// Builds sentence segments with context for every page and paragraph segments for legal pages.
/// </summary>
internal sealed class SegmentBuilder(SentenceSplitter splitter, IOptions<EshopGuardOptions> options)
{
    private SegmentationOptions Settings => options.Value.Segmentation;

    /// <summary>
    /// Sentences with context from every region of the page. With <c>sieveChunkChars</c>, sentences of the main text
    /// get the index of their sieve chunk (<see cref="SieveChunker"/> with this chunk size), so the sieve can skip them.
    /// </summary>
    public List<SegmentOccurrence> BuildSentences(string url, PageType pageType, ExtractedPage page, int? sieveChunkChars = null)
    {
        var settings = Settings;
        var occurrences = new List<SegmentOccurrence>();
        foreach (var region in GetRegions(page))
        {
            var chunkOfBlock = region.Source == SegmentSource.Main && sieveChunkChars is { } chars
                ? SieveChunker.ChunkOfBlock(SieveChunker.Chunk(region.Blocks, chars), region.Blocks.Count)
                : null;
            var parts = region.Blocks
                .SelectMany((b, block) => splitter.Split(b.Text).Select(s => (Text: s, Block: block)))
                .SelectMany(s => SentenceSplitter.SplitLong(s.Text, settings.MaxSentenceLength).Select(t => (Text: t, s.Block)))
                .ToList();
            var sentences = parts.Select(p => p.Text).ToList();

            for (var i = 0; i < sentences.Count; i++)
            {
                // Short fragments are dropped as segments but stay in the context of their neighbours.
                if (!IsWorthEvaluating(sentences[i], settings))
                {
                    continue;
                }

                var before = sentences.Skip(Math.Max(0, i - settings.ContextSentences)).Take(Math.Min(i, settings.ContextSentences));
                var after = sentences.Skip(i + 1).Take(settings.ContextSentences);
                occurrences.Add(new SegmentOccurrence(
                    SegmentKind.Sentence,
                    sentences[i],
                    TrimContext(string.Join(" ", before), settings.MaxContextChars, keepEnd: true),
                    TrimContext(string.Join(" ", after), settings.MaxContextChars, keepEnd: false),
                    url,
                    pageType,
                    region.Source,
                    chunkOfBlock?[parts[i].Block]));
            }
        }

        return occurrences;
    }

    /// <summary>
    /// Groups the main text of a legal page, followed by its other visible text, into paragraphs of at most
    /// <see cref="SegmentationOptions.MaxParagraphLength"/> characters, each prefixed with its nearest heading.
    /// </summary>
    public List<SegmentOccurrence> BuildLegalParagraphs(string url, ExtractedPage page)
    {
        var settings = Settings;
        var occurrences = new List<SegmentOccurrence>();
        string? heading = null;
        var parts = new List<string>();
        var length = 0;

        void Flush()
        {
            if (parts.Count == 0)
            {
                return;
            }

            var body = string.Join("\n", parts);
            var text = heading is null ? body : heading + "\n" + body;
            parts.Clear();
            length = 0;
            if (IsWorthEvaluating(text, settings))
            {
                occurrences.Add(new SegmentOccurrence(SegmentKind.LegalParagraph, text, "", "", url, PageType.Legal, SegmentSource.Main));
            }
        }

        foreach (var block in page.MainBlocks.Concat(page.RestBlocks))
        {
            if (block.IsHeading)
            {
                Flush();
                heading = block.Text;
                continue;
            }

            var headingLength = heading is null ? 0 : heading.Length + 1;
            var room = Math.Max(settings.MinSegmentLength, settings.MaxParagraphLength - headingLength);
            foreach (var piece in SplitToPieces(block.Text, room))
            {
                if (length > 0 && length + 1 + piece.Length > room)
                {
                    Flush();
                }

                parts.Add(piece);
                length += (length > 0 ? 1 : 0) + piece.Length;
            }
        }

        Flush();
        return occurrences;
    }

    /// <summary>
    /// At least <see cref="SegmentationOptions.MinSegmentLength"/> characters and at least one word of
    /// <see cref="SegmentationOptions.MinWordLetters"/> letters.
    /// </summary>
    public static bool IsWorthEvaluating(string text, SegmentationOptions settings)
    {
        if (text.Length < settings.MinSegmentLength)
        {
            return false;
        }

        if (settings.MinWordLetters <= 0)
        {
            return true;
        }

        var run = 0;
        foreach (var c in text)
        {
            run = char.IsLetter(c) ? run + 1 : 0;
            if (run >= settings.MinWordLetters)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<string> SplitToPieces(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            yield return text;
            yield break;
        }

        var piece = new StringBuilder();
        foreach (var sentence in splitter.Split(text).SelectMany(s => SentenceSplitter.SplitLong(s, maxLength)))
        {
            if (piece.Length > 0 && piece.Length + 1 + sentence.Length > maxLength)
            {
                yield return piece.ToString();
                piece.Clear();
            }

            piece.Append(piece.Length > 0 ? " " : "").Append(sentence);
        }

        if (piece.Length > 0)
        {
            yield return piece.ToString();
        }
    }

    private static IEnumerable<TextRegion> GetRegions(ExtractedPage page)
    {
        yield return new TextRegion(SegmentSource.Main, page.MainBlocks);
        foreach (var chrome in page.ChromeRegions)
        {
            yield return new TextRegion(SegmentSource.Chrome, chrome);
        }

        if (page.RestBlocks.Count > 0)
        {
            yield return new TextRegion(SegmentSource.Rest, page.RestBlocks);
        }

        if (page.Title is { Length: > 0 } title)
        {
            yield return new TextRegion(SegmentSource.Title, [new TextBlock(title)]);
        }

        if (page.MetaDescription is { Length: > 0 } meta)
        {
            yield return new TextRegion(SegmentSource.MetaDescription, [new TextBlock(meta)]);
        }

        if (page.JsonLdDescription is { Length: > 0 } description)
        {
            yield return new TextRegion(SegmentSource.JsonLd, [new TextBlock(description)]);
        }
    }

    private static string TrimContext(string context, int maxChars, bool keepEnd)
    {
        if (context.Length <= maxChars)
        {
            return context;
        }

        if (keepEnd)
        {
            var tail = context[^maxChars..];
            var space = tail.IndexOf(' ');
            return "…" + (space >= 0 ? tail[(space + 1)..] : tail);
        }

        var head = context[..maxChars];
        var lastSpace = head.LastIndexOf(' ');
        return (lastSpace > 0 ? head[..lastSpace] : head) + "…";
    }
}
