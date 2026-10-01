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

    /// <summary>
    /// <c>findings.json</c> of change 6 carries the fields it always had plus codes and verdicts. For the comparison with an
    /// older reference, every object of the new file is cut down to the fields the reference has (recursively), so the texts,
    /// scores and pages must still be the same. Other files are compared as they are.
    /// </summary>
    public static SortedDictionary<string, string> ProjectOnto(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual)
    {
        var result = new SortedDictionary<string, string>(actual.ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal);
        if (expected.TryGetValue("findings.json", out var reference) && actual.TryGetValue("findings.json", out var current))
        {
            var expectedNode = System.Text.Json.Nodes.JsonNode.Parse(reference);
            var projected = Project(expectedNode, System.Text.Json.Nodes.JsonNode.Parse(current));
            if (System.Text.Json.Nodes.JsonNode.DeepEquals(expectedNode, projected))
            {
                result["findings.json"] = reference;
            }
        }

        return result;
    }

    private static System.Text.Json.Nodes.JsonNode? Project(System.Text.Json.Nodes.JsonNode? shape, System.Text.Json.Nodes.JsonNode? value) => (shape, value) switch
    {
        (System.Text.Json.Nodes.JsonObject s, System.Text.Json.Nodes.JsonObject v) => new System.Text.Json.Nodes.JsonObject(
            s.Select(p => KeyValuePair.Create(p.Key, v.TryGetPropertyValue(p.Key, out var inner) ? Project(p.Value, inner) : null))),
        (System.Text.Json.Nodes.JsonArray s, System.Text.Json.Nodes.JsonArray v) => new System.Text.Json.Nodes.JsonArray(
            v.Select((item, i) => Project(i < s.Count ? s[i] : item, item)).ToArray()),
        _ => value?.DeepClone(),
    };

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
