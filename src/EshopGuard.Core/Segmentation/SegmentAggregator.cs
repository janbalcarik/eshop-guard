using EshopGuard.Core.Models;
using EshopGuard.Core.Options;

namespace EshopGuard.Core.Segmentation;

/// <summary>
/// Deduplicates occurrences into unique segments and marks boilerplate.
/// A unique segment is the same kind, text and context; boilerplate is decided by text alone.
/// </summary>
internal static class SegmentAggregator
{
    private const char Separator = '\u001F';

    public static List<Segment> Aggregate(IReadOnlyList<SegmentOccurrence> occurrences, int pageCount, SegmentationOptions options)
    {
        var pagesByText = occurrences
            .GroupBy(o => (o.Kind, Text: TextTools.NormalizeForHash(o.Text)))
            .ToDictionary(g => g.Key, g => g.Select(o => o.Url).Distinct().Count());

        bool IsBoilerplate(SegmentKind kind, string text)
        {
            var pages = pagesByText[(kind, TextTools.NormalizeForHash(text))];
            return pageCount > 0
                && pages >= options.MinBoilerplatePages
                && (double)pages / pageCount > options.BoilerplatePageShare;
        }

        var groups = new Dictionary<string, List<SegmentOccurrence>>();
        var order = new List<string>();
        foreach (var occurrence in occurrences)
        {
            var hash = Hash(occurrence);
            if (!groups.TryGetValue(hash, out var list))
            {
                groups[hash] = list = [];
                order.Add(hash);
            }

            list.Add(occurrence);
        }

        return order.Select(hash =>
        {
            var list = groups[hash];
            var first = list[0];
            return new Segment
            {
                Hash = hash,
                Kind = first.Kind,
                Text = first.Text,
                ContextBefore = first.ContextBefore,
                ContextAfter = first.ContextAfter,
                Sources = list.Select(o => o.Source).Distinct().ToList(),
                SieveChunks = list.Where(o => o.Chunk is not null).Select(o => new SieveChunkRef(o.Url, o.Chunk!.Value)).Distinct().ToList(),
                SieveExempt = list.Any(o => o.Chunk is null),
                PageTypes = list.Select(o => o.PageType).Distinct().ToList(),
                Urls = list.Select(o => o.Url).Distinct().ToList(),
                Boilerplate = IsBoilerplate(first.Kind, first.Text),
            };
        }).ToList();
    }

    public static string Hash(SegmentOccurrence occurrence) =>
        TextTools.Sha256(string.Join(
            Separator,
            TextTools.Snake(occurrence.Kind),
            TextTools.NormalizeForHash(occurrence.Text),
            TextTools.NormalizeForHash(occurrence.ContextBefore),
            TextTools.NormalizeForHash(occurrence.ContextAfter)));
}
