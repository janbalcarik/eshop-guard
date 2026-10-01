using System.Globalization;
using System.Text;
using System.Text.Json;
using EshopGuard.Core.Models;
using EshopGuard.Core.Report;
using EshopGuard.Core.Rules.Texts;

namespace EshopGuard.Core.Fix;

/// <summary>
/// Writes <c>rewrite.md</c> (page by page: what to change, what to fill in, why, result of the check) and
/// <c>rewrite.json</c> (everything, for further processing) into the output directory of the scan.
/// </summary>
public static class RewriteReportWriter
{
    /// <summary>File name of the Markdown report.</summary>
    public const string MarkdownFile = "rewrite.md";

    /// <summary>File name of the JSON output.</summary>
    public const string JsonFile = "rewrite.json";

    /// <summary>Writes both files; titles of the findings of the check are in <paramref name="locale"/>.</summary>
    public static async Task WriteAsync(RewriteResult result, string outputDirectory, RuleTextRenderer texts, string locale, CancellationToken ct = default)
    {
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, MarkdownFile), Markdown(result, texts, locale), new UTF8Encoding(false), ct);
        await using var stream = File.Create(Path.Combine(outputDirectory, JsonFile));
        await JsonSerializer.SerializeAsync(stream, result, ReportFormat.Json, ct);
    }

    /// <summary>Czech label of a status.</summary>
    public static string Status(RewriteStatus status) => status switch
    {
        RewriteStatus.Resolved => "vyřešeno",
        RewriteStatus.WaitingForFacts => "čeká na doplnění",
        RewriteStatus.StillFinding => "stále nález",
        RewriteStatus.Kept => "ponecháno",
        _ => "bez návrhu",
    };

    /// <summary>The Markdown report.</summary>
    public static string Markdown(RewriteResult result, RuleTextRenderer texts, string locale)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(texts);
        string Title(Finding finding) => texts.Render(finding, locale).Title;
        var cz = ReportFormat.Czech;
        var findings = result.Pages.SelectMany(p => p.Findings).ToList();
        int Count(RewriteStatus status) => findings.Count(f => f.Status == status);
        var text = new StringBuilder();
        text.Append("# Návrh přepisu problematických pasáží\n\n");
        text.Append(CultureInfo.InvariantCulture, $"Model {result.Model}, zadání {result.PromptVersion}.\n\n");
        text.Append("Návrhy napsal jazykový model a nástroj každý změněný blok znovu zkontroloval svými pravidly. ");
        text.Append("Před zveřejněním je zkontrolujte, doplňte údaje v hranatých závorkách „[doplňte: …]“ (bez nich text zveřejnit nelze; ");
        text.Append("pokud údaj nemáte doložený, tvrzení odstraňte) a konečné znění nechte posoudit právníkem.\n\n");

        text.Append("## Souhrn\n\n| Ukazatel | Hodnota |\n|---|---|\n");
        text.Append(cz, $"| Stránek | {result.Stats.Pages} (z mezipaměti {result.Stats.FromCache}, chyb {result.Stats.Errors}) |\n");
        text.Append(cz, $"| Nálezů | {findings.Count}: vyřešeno {Count(RewriteStatus.Resolved)}, čeká na doplnění {Count(RewriteStatus.WaitingForFacts)}, stále nález {Count(RewriteStatus.StillFinding)}, ponecháno {Count(RewriteStatus.Kept)}, bez návrhu {Count(RewriteStatus.NotAddressed)} |\n");
        text.Append(cz, $"| Tokeny | vstup {result.Stats.InputTokens:N0} (z mezipaměti {result.Stats.CachedTokens:N0}), výstup {result.Stats.OutputTokens:N0} (přemýšlení {result.Stats.ReasoningTokens:N0}) |\n");
        text.Append(cz, $"| Cena | model {result.Stats.CostUsd:0.0000} USD, kontrola Jevem {result.Stats.CheckCostUsd:0.0000} USD ({result.Stats.CheckCalls} volání) |\n");
        text.Append(cz, $"| Doba | {result.Stats.Duration.TotalSeconds:0} s |\n\n");
        foreach (var warning in result.Warnings)
        {
            text.Append("Upozornění: ").Append(warning).Append("\n\n");
        }

        foreach (var page in result.Pages)
        {
            text.Append("## ").Append(page.Url).Append("\n\n");
            foreach (var finding in page.Findings)
            {
                var f = finding.Finding;
                text.Append("- **").Append(finding.Id).Append("** (").Append(f.Checkability == "text" ? "porušení" : "k posouzení")
                    .Append(", ").Append(finding.Texts.Count > 0 ? finding.Texts[0].Title : Title(f)).Append("): „").Append(f.Text).Append("“ → **").Append(Status(finding.Status)).Append("**");
                if (finding.AlsoOn.Count > 0)
                {
                    text.Append(cz, $" (stejný text i na dalších {finding.AlsoOn.Count} stránkách)");
                }

                text.Append('\n');
            }

            text.Append('\n');
            if (page.Error is not null)
            {
                text.Append("Chyba: ").Append(page.Error).Append("\n\n");
                continue;
            }

            if (page.Changes.Count > 0)
            {
                text.Append("### Změny\n\n");
                for (var i = 0; i < page.Changes.Count; i++)
                {
                    var change = page.Changes[i];
                    text.Append(cz, $"{i + 1}. {string.Join(", ", change.BlockIds)} ({string.Join(", ", change.FindingIds)}): **{Status(change.Status)}**\n");
                    text.Append("   - Původně: ").Append(OneLine(change.Original)).Append('\n');
                    text.Append("   - Nově: ").Append(change.Rewritten.Length == 0 ? "*(blok smazat)*" : OneLine(change.Rewritten)).Append('\n');
                    if (change.Placeholders.Count > 0)
                    {
                        text.Append("   - Doplňte: ").Append(string.Join("; ", change.Placeholders)).Append('\n');
                    }

                    text.Append("   - Proč: ").Append(change.Reason).Append('\n');
                    if (change.RemainingFindings.Count > 0)
                    {
                        text.Append("   - Kontrola: ").Append(change.Status == RewriteStatus.WaitingForFacts ? "do doplnění stále " : "stále ")
                            .Append(string.Join("; ", change.RemainingFindings.Select(r => $"{Title(r)} („{r.Text}“)"))).Append('\n');
                    }

                    if (change.VerifyFindings.Count > 0)
                    {
                        text.Append("   - K ověření: ").Append(string.Join("; ", change.VerifyFindings.Select(r => $"{Title(r)} („{r.Text}“)"))).Append('\n');
                    }

                    if (change.UpcomingFindings.Count > 0)
                    {
                        text.Append("   - Platí později: ").Append(string.Join("; ", change.UpcomingFindings.Select(r =>
                            $"{Title(r)} („{r.Text}“, od {r.Strictest.EffectiveFrom?.ToString("d. M. yyyy", cz)})"))).Append('\n');
                    }
                }

                text.Append('\n');
            }

            if (page.Kept.Count > 0)
            {
                text.Append("### Ponecháno\n\n");
                foreach (var kept in page.Kept)
                {
                    text.Append("- ").Append(kept.FindingId).Append(": ").Append(kept.Reason).Append('\n');
                }

                text.Append('\n');
            }

            if (page.Changes.Count > 0)
            {
                AppendFullText(text, page);
            }
        }

        return text.ToString();
    }

    /// <summary>The whole text of the page after the changes: changed blocks in bold, deleted ones struck through.</summary>
    private static void AppendFullText(StringBuilder text, RewritePage page)
    {
        text.Append("### Celý opravený text\n\n");
        text.Append("Změněné odstavce jsou tučně, smazané přeškrtnuté; ostatní text je beze změny.\n\n");
        foreach (var block in page.Blocks.Where(b => !b.IsMainText && b.Changed))
        {
            var label = block.Id.StartsWith("TITLE", StringComparison.Ordinal) ? "Titulek"
                : block.Id.StartsWith("META", StringComparison.Ordinal) ? "Meta popis"
                : block.Id.StartsWith("JSONLD", StringComparison.Ordinal) ? "Popis ve strukturovaných datech"
                : "Text mimo hlavní obsah (patička, menu)";
            text.Append(label).Append(": ").Append(Marked(block)).Append("\n\n");
        }

        foreach (var block in page.Blocks.Where(b => b.IsMainText))
        {
            text.Append("> ").Append(Marked(block)).Append("\n>\n");
        }

        text.Append('\n');
    }

    private static string Marked(RewriteBlock block) =>
        !block.Changed ? block.Original
        : block.Rewritten.Length == 0 ? $"~~{Escape(block.Original)}~~"
        : $"**{Escape(block.Rewritten)}**";

    private static string Escape(string text) => text.Replace("*", "\\*", StringComparison.Ordinal).Replace("~", "\\~", StringComparison.Ordinal);

    private static string OneLine(string text) => text.Replace("\r", "", StringComparison.Ordinal).Replace("\n", " / ", StringComparison.Ordinal);
}
