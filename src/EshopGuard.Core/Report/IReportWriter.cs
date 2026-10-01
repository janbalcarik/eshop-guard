using EshopGuard.Core.Models;

namespace EshopGuard.Core.Report;

/// <summary>
/// Writes one output file from a scan result. A product can skip writers and show the result in its own UI.
/// </summary>
public interface IReportWriter
{
    /// <summary>Name of the produced file, e.g. <c>segments.csv</c>.</summary>
    string FileName { get; }

    /// <summary>Writes the file into the output directory.</summary>
    Task WriteAsync(ScanResult result, string outputDirectory, CancellationToken ct = default);
}
