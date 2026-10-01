using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Segmentation;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Pipeline;

/// <summary>
/// Splits the analyzed pages into sentences with context (and legal pages also into paragraphs), cuts their main text into
/// chunks for the sieve and merges the same segments of all pages into unique ones; boilerplate is marked. For every page
/// it also returns the counts and the fingerprints of its segments, which the page version keeps.
/// </summary>
internal sealed class SegmentStep(SegmentBuilder builder, IOptions<EshopGuardOptions> options)
{
    public SegmentResult Segment(SegmentInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var occurrences = new List<SegmentOccurrence>();
        var sieveChunks = new List<SieveChunkInput>();
        var pages = new List<PageSegmentation>();
        var analyzed = input.Pages.Where(p => p.Info.IncludedInAnalysis).ToList();
        foreach (var page in analyzed)
        {
            var url = page.Info.Url;

            // Legal pages are never sieved: missing information there is a finding of its own.
            var chunkChars = input.SieveChunkChars is { } chars && page.Info.Type != PageType.Legal ? chars : (int?)null;
            var sentences = builder.BuildSentences(url, page.Info.Type, page.Content, chunkChars);
            var ofPage = new List<SegmentOccurrence>(sentences);
            occurrences.AddRange(sentences);
            if (chunkChars is { } size)
            {
                sieveChunks.AddRange(SieveChunker.Chunk(page.Content.MainBlocks, size).Select(c => new SieveChunkInput(url, c)));
            }

            int? paragraphCount = null;
            if (page.Info.Type == PageType.Legal)
            {
                var paragraphs = builder.BuildLegalParagraphs(url, page.Content);
                paragraphCount = paragraphs.Count;
                occurrences.AddRange(paragraphs);
                ofPage.AddRange(paragraphs);
            }

            var fingerprints = ofPage.Select(o => SentenceFingerprint.Of(o.Text)).Distinct().Order().ToList();
            pages.Add(new PageSegmentation(url, sentences.Count, paragraphCount, fingerprints));
        }

        var segments = SegmentAggregator.Aggregate(occurrences, analyzed.Count, options.Value.Segmentation);
        return new SegmentResult(occurrences.Count, segments, sieveChunks, pages);
    }

    /// <summary>Writes the counts into the pages, as the report shows them.</summary>
    public static void ApplyCounts(SegmentResult result, IEnumerable<ExtractedPageRecord> pages)
    {
        var byUrl = result.Pages.ToDictionary(p => p.Url);
        foreach (var page in pages)
        {
            if (byUrl.TryGetValue(page.Info.Url, out var counts))
            {
                page.Info.SentenceCount = counts.SentenceCount;
                if (counts.ParagraphCount is { } paragraphs)
                {
                    page.Info.ParagraphCount = paragraphs;
                }
            }
        }
    }
}
