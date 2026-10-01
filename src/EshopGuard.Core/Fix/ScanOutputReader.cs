using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Core.Models;
using EshopGuard.Core.Report;

namespace EshopGuard.Core.Fix;

/// <summary>
/// Reads the output directory of a scan (<c>findings.json</c> and <c>pages.jsonl</c>) as input of a rewrite.
/// </summary>
public static class ScanOutputReader
{
    /// <summary>Reads the findings and pages of a scan.</summary>
    /// <exception cref="InvalidOperationException">A file is missing or not valid.</exception>
    public static RewriteInput Read(string outputDirectory, string country = "sk", int? maxPages = null)
    {
        var findingsFile = Path.Combine(outputDirectory, "findings.json");
        var pagesFile = Path.Combine(outputDirectory, "pages.jsonl");
        foreach (var file in new[] { findingsFile, pagesFile })
        {
            if (!File.Exists(file))
            {
                throw new InvalidOperationException($"Ve složce {outputDirectory} chybí {Path.GetFileName(file)}; zadejte složku s výsledky skenu.");
            }
        }

        List<Finding> findings;
        try
        {
            findings = JsonSerializer.Deserialize<List<Finding>>(File.ReadAllText(findingsFile), ReportFormat.Json) ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Soubor {findingsFile} není platný: {ex.Message}", ex);
        }

        var pages = new List<RewritePageInput>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(pagesFile))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonNode? node;
            try
            {
                node = JsonNode.Parse(line);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Soubor {pagesFile} není platný na řádku {lineNumber}: {ex.Message}", ex);
            }

            var url = node?["url"]?.GetValue<string>();
            if (url is null)
            {
                continue;
            }

            pages.Add(new RewritePageInput
            {
                Url = url,
                Type = node?["type"]?.GetValue<string>() ?? "",
                Title = node?["title"]?.GetValue<string>(),
                Category = node?["category"]?.GetValue<string>() ?? "",
                MainText = node?["main_text"]?.GetValue<string>() ?? "",
                MetaDescription = node?["meta_description"]?.GetValue<string>(),
                JsonLdDescription = node?["json_ld_description"]?.GetValue<string>(),
            });
        }

        return new RewriteInput { Findings = findings, Pages = pages, Country = country, MaxPages = maxPages };
    }
}
