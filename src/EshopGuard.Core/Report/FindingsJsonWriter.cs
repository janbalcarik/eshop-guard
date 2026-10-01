using System.Text.Json;
using EshopGuard.Core.Models;

namespace EshopGuard.Core.Report;

/// <summary>
/// Writes all findings with all fields and question probabilities to <c>findings.json</c>.
/// </summary>
internal sealed class FindingsJsonWriter : IReportWriter
{
    public string FileName => "findings.json";

    public async Task WriteAsync(ScanResult result, string outputDirectory, CancellationToken ct = default)
    {
        await using var stream = File.Create(Path.Combine(outputDirectory, FileName));
        await JsonSerializer.SerializeAsync(stream, result.Findings, ReportFormat.Json, ct);
    }
}
