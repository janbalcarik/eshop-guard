using System.Globalization;
using System.Text;
using CsvHelper;
using EshopGuard.Core.Models;

namespace EshopGuard.Core.Report;

/// <summary>
/// Writes every chunk of the main text the sieve asked, with the topic probability of every sieved module, so that
/// it can be checked what the sieve let through and what it left out. Only the header when the sieve was off.
/// </summary>
internal sealed class SieveCsvWriter : IReportWriter
{
    public string FileName => "sieve.csv";

    public async Task WriteAsync(ScanResult result, string outputDirectory, CancellationToken ct = default)
    {
        var modules = result.SieveChunks.SelectMany(c => c.Probabilities?.Keys ?? []).Distinct().Order(StringComparer.Ordinal).ToList();
        await using var writer = new StreamWriter(Path.Combine(outputDirectory, FileName), append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        foreach (var column in new[] { "url", "chunk", "chars", "status" }.Concat(modules.Select(m => "sieve_" + m)).Append("text"))
        {
            csv.WriteField(column);
        }

        await csv.NextRecordAsync();
        foreach (var chunk in result.SieveChunks)
        {
            ct.ThrowIfCancellationRequested();
            csv.WriteField(chunk.Url);
            csv.WriteField(chunk.Index);
            csv.WriteField(chunk.Text.Length);
            csv.WriteField(chunk.Status);
            foreach (var module in modules)
            {
                csv.WriteField(chunk.Probabilities?.TryGetValue(module, out var p) == true ? p.ToString("0.000", CultureInfo.InvariantCulture) : "");
            }

            csv.WriteField(chunk.Text);
            await csv.NextRecordAsync();
        }
    }
}
