using System.Text.RegularExpressions;
using EshopGuard.Core.Models;
using EshopGuard.Core.Report;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Outputs of a scan as the CLI writes them (findings.json, pages.jsonl, segments.csv, sieve.csv, profiles.json,
/// report.md and the CSV of findings), without what changes from run to run: date, durations, crawl pace and the time a
/// profile was written.
/// </summary>
internal static partial class OutputNormalizer
{
    /// <summary>Writes the outputs of <paramref name="result"/> with the report writers of <paramref name="provider"/> and returns them normalized by file name.</summary>
    public static async Task<SortedDictionary<string, string>> WriteAsync(IServiceProvider provider, ScanResult result, string? directory = null)
    {
        directory ??= Directory.CreateTempSubdirectory("eshopguard-out-").FullName;
        Directory.CreateDirectory(directory);
        foreach (var writer in provider.GetServices<IReportWriter>())
        {
            await writer.WriteAsync(result, directory, TestContext.Current.CancellationToken);
        }

        return Read(directory);
    }

    /// <summary>Normalized content of every output file in the folder.</summary>
    public static SortedDictionary<string, string> Read(string directory) =>
        new(Directory.GetFiles(directory)
                .Where(f => Path.GetFileName(f) != "run.log")
                .ToDictionary(f => Path.GetFileName(f), f => Normalize(Path.GetFileName(f), File.ReadAllText(f))),
            StringComparer.Ordinal);

    /// <summary>Content without the parts that differ between two runs of the same input.</summary>
    public static string Normalize(string fileName, string content)
    {
        content = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        return fileName switch
        {
            "report.md" => ReportLines(content),
            "profiles.json" => CreatedAt().Replace(content, "\"created_at\": \"-\""),
            _ => content,
        };
    }

    private static string ReportLines(string content) => string.Join('\n', content.Split('\n').Select(line =>
        line.StartsWith("| Datum |", StringComparison.Ordinal) ? "| Datum | - |"
        : line.StartsWith("- Doba běhu:", StringComparison.Ordinal) ? "- Doba běhu: -"
        : line.StartsWith("- Stahování:", StringComparison.Ordinal) ? CrawlRequests().Replace(line, "$1")
        : line));

    [GeneratedRegex("\"created_at\": \"[^\"]*\"")]
    private static partial Regex CreatedAt();

    /// <summary>Keeps the number of requests, drops time and pace.</summary>
    [GeneratedRegex(@"^(- Stahování: \d+ požadavků).*$")]
    private static partial Regex CrawlRequests();

    /// <summary>Lists the files that differ, with the first differing line of each.</summary>
    public static string Differences(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual)
    {
        var report = new List<string>();
        foreach (var name in expected.Keys.Union(actual.Keys).Order(StringComparer.Ordinal))
        {
            if (!expected.TryGetValue(name, out var left))
            {
                report.Add($"{name}: navíc");
                continue;
            }

            if (!actual.TryGetValue(name, out var right))
            {
                report.Add($"{name}: chybí");
                continue;
            }

            if (left == right)
            {
                continue;
            }

            var a = left.Split('\n');
            var b = right.Split('\n');
            var line = Enumerable.Range(0, Math.Max(a.Length, b.Length)).First(i => i >= a.Length || i >= b.Length || a[i] != b[i]);
            report.Add($"{name}, řádek {line + 1}:\n  čekáno: {(line < a.Length ? Cut(a[line]) : "(konec)")}\n  je:     {(line < b.Length ? Cut(b[line]) : "(konec)")}");
        }

        return string.Join('\n', report);
    }

    private static string Cut(string line) => line.Length <= 300 ? line : line[..300] + "…";
}
