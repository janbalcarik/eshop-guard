using System.Globalization;
using System.Text;
using CsvHelper;
using EshopGuard.Core.Models;

namespace EshopGuard.Core.Report;

/// <summary>
/// Writes every unique segment with its context, pages and the probability of every question,
/// as the basis for thresholds and the labeled sample. UTF-8 with BOM so that Excel shows Czech characters.
/// </summary>
internal sealed class SegmentsCsvWriter : IReportWriter
{
    private static readonly string[] Header =
    [
        "segment_hash", "kind", "text", "context_before", "context_after", "sources", "page_types",
        "boilerplate", "occurrences", "urls",
    ];

    public string FileName => "segments.csv";

    public async Task WriteAsync(ScanResult result, string outputDirectory, CancellationToken ct = default)
    {
        var questions = result.RuleSets.SelectMany(r => r.QuestionIds).Distinct().ToList();
        await using var writer = new StreamWriter(Path.Combine(outputDirectory, FileName), append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        foreach (var column in Header.Concat(questions))
        {
            csv.WriteField(column);
        }

        await csv.NextRecordAsync();
        foreach (var segment in result.Segments)
        {
            ct.ThrowIfCancellationRequested();
            csv.WriteField(segment.Hash);
            csv.WriteField(TextTools.Snake(segment.Kind));
            csv.WriteField(segment.Text);
            csv.WriteField(segment.ContextBefore);
            csv.WriteField(segment.ContextAfter);
            csv.WriteField(string.Join("|", segment.Sources.Select(TextTools.Snake)));
            csv.WriteField(string.Join("|", segment.PageTypes.Select(TextTools.Snake)));
            csv.WriteField(segment.Boilerplate ? "true" : "false");
            csv.WriteField(segment.Occurrences.ToString(CultureInfo.InvariantCulture));
            csv.WriteField(string.Join(" ", segment.Urls));
            foreach (var question in questions)
            {
                csv.WriteField(segment.Probabilities.TryGetValue(question, out var p) ? p.ToString("0.000", CultureInfo.InvariantCulture) : "");
            }

            await csv.NextRecordAsync();
        }
    }
}
