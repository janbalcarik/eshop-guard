using System.Globalization;
using System.Text;
using CsvHelper;
using EshopGuard.Core.Models;

namespace EshopGuard.Core.Report;

/// <summary>
/// Writes one row per finding with empty <c>human_label</c> and <c>note</c> columns for manual labeling.
/// </summary>
internal sealed class FindingsCsvWriter : IReportWriter
{
    private static readonly string[] Header =
    [
        "rule_id", "module", "title", "severity", "checkability", "scope", "band", "score", "text", "context_before",
        "context_after", "sources", "occurrences", "urls", "boilerplate", "question_probs", "legal_refs", "notes",
        "segment_hash", "human_label", "note",
    ];

    public string FileName => "findings.csv";

    public async Task WriteAsync(ScanResult result, string outputDirectory, CancellationToken ct = default)
    {
        await using var writer = new StreamWriter(Path.Combine(outputDirectory, FileName), append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        foreach (var column in Header)
        {
            csv.WriteField(column);
        }

        await csv.NextRecordAsync();
        foreach (var finding in result.Findings)
        {
            ct.ThrowIfCancellationRequested();
            csv.WriteField(finding.RuleId);
            csv.WriteField(finding.Module);
            csv.WriteField(finding.Title);
            csv.WriteField(finding.Severity);
            csv.WriteField(finding.Checkability);
            csv.WriteField(finding.Scope);
            csv.WriteField(TextTools.Snake(finding.Band));
            csv.WriteField(finding.Score.ToString("0.000", CultureInfo.InvariantCulture));
            csv.WriteField(finding.Text ?? "");
            csv.WriteField(finding.ContextBefore);
            csv.WriteField(finding.ContextAfter);
            csv.WriteField(string.Join("|", finding.Sources.Select(TextTools.Snake)));
            csv.WriteField(finding.Occurrences.ToString(CultureInfo.InvariantCulture));
            csv.WriteField(string.Join(" ", finding.Urls));
            csv.WriteField(finding.Boilerplate ? "true" : "false");
            csv.WriteField(ReportFormat.Questions(finding.QuestionProbs));
            csv.WriteField(string.Join(" | ", finding.LegalRefs.Select(r => $"{r.Jurisdiction}: {r.Ref} [{r.Status}]")));
            csv.WriteField(string.Join(" ", finding.Notes));
            csv.WriteField(finding.SegmentHash ?? "");
            csv.WriteField("");
            csv.WriteField("");
            await csv.NextRecordAsync();
        }
    }
}
