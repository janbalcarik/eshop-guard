using System.Text.Json;
using EshopGuard.Core.Models;

namespace EshopGuard.Core.Report;

/// <summary>
/// Writes all findings to <c>findings.json</c>: codes, parameters and verdicts with the texts in the language of the run
/// (<see cref="FindingDocument"/>).
/// </summary>
internal sealed class FindingsJsonWriter(ReportTexts texts) : IReportWriter
{
    public string FileName => "findings.json";

    public async Task WriteAsync(ScanResult result, string outputDirectory, CancellationToken ct = default)
    {
        var renderer = await texts.RendererAsync();
        var several = result.Jurisdictions.Count > 1;
        var documents = result.Findings.Select(f => FindingDocument.From(f, renderer, texts.Locale, several)).ToList();
        await using var stream = File.Create(Path.Combine(outputDirectory, FileName));
        await JsonSerializer.SerializeAsync(stream, documents, ReportFormat.Json, ct);
    }
}
