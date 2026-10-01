using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EshopGuard.Core.Models;

namespace EshopGuard.Core.Report;

/// <summary>
/// Writes one JSON line per downloaded page with its type, title, product category text, meta description, JSON-LD
/// description and extracted main text, so that the extraction can be checked and pages can be evaluated or rewritten.
/// </summary>
internal sealed class PagesJsonlWriter : IReportWriter
{
    // Relaxed escaping keeps Czech and Slovak letters readable in the file.
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string FileName => "pages.jsonl";

    public async Task WriteAsync(ScanResult result, string outputDirectory, CancellationToken ct = default)
    {
        await using var writer = new StreamWriter(Path.Combine(outputDirectory, FileName), append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        foreach (var page in result.Pages)
        {
            ct.ThrowIfCancellationRequested();
            var line = JsonSerializer.Serialize(
                new
                {
                    page.Url,
                    Type = TextTools.Snake(page.Type),
                    page.Title,
                    page.Category,
                    page.MetaDescription,
                    page.JsonLdDescription,
                    Extraction = TextTools.Snake(page.Extraction),
                    page.IncludedInAnalysis,
                    page.TextNotLoaded,
                    page.VisibleTextChars,
                    page.CheckedTextChars,
                    page.NavigationTextChars,
                    page.ListingTextChars,
                    page.ProfileSkippedTextChars,
                    page.UncheckedTextChars,
                    page.ProfileId,
                    ProfileUnknownShare = page.ProfileUnknownShare is { } share ? Math.Round(share, 3) : (double?)null,
                    page.ScriptApp,
                    page.SentenceCount,
                    page.ParagraphCount,
                    page.MainText,
                    page.RestText,
                    page.ProfileSkippedText,
                },
                Json);
            await writer.WriteLineAsync(line.AsMemory(), ct);
        }
    }
}
