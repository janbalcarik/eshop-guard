using System.Text;
using EshopGuard.Core.Extract;
using EshopGuard.Core.Options;

namespace EshopGuard.Core.Markets;

/// <summary>A page given to the model: its address and its text (title, descriptions, main text, frame and other text).</summary>
public sealed record SalesPage(string Url, string Text, bool IsHome);

/// <summary>
/// The call of the analysis of the places of sale (change 7, design section 3.3): the technical signals as <c>name=value</c>
/// lines and the texts of the home page (cut to <c>markets.home_text_chars</c>) and of the picked pages (cut to
/// <c>markets.page_text_chars</c>).
/// </summary>
internal static class PlacesOfSaleModel
{
    /// <summary>The readable text of an extracted page, one block per line.</summary>
    public static string PageText(ExtractedPage page) => string.Join('\n',
        new[] { page.Title, page.MetaDescription }.OfType<string>()
            .Concat(page.MainBlocks.Select(b => b.Text))
            .Concat(page.ChromeRegions.SelectMany(r => r).Select(b => b.Text))
            .Concat(page.RestBlocks.Select(b => b.Text)));

    /// <summary>The input of the model.</summary>
    public static string Input(MarketSignals signals, IReadOnlyList<SalesPage> pages, MarketsOptions options)
    {
        var text = new StringBuilder();
        text.AppendLine("TECHNICAL SIGNALS:");
        text.Append("site=").AppendLine(signals.Site);
        foreach (var (name, value) in signals.Values().Distinct())
        {
            text.Append(name).Append('=').AppendLine(value);
        }

        foreach (var page in pages)
        {
            var limit = page.IsHome ? options.HomeTextChars : options.PageTextChars;
            text.AppendLine().Append("=== PAGE ").Append(page.Url).AppendLine(" ===");
            text.AppendLine(page.Text.Length <= limit ? page.Text : page.Text[..limit]);
        }

        return text.ToString();
    }

    /// <summary>Characters of the pages as sent (for the estimate).</summary>
    public static int PageChars(IReadOnlyList<SalesPage> pages, MarketsOptions options) =>
        pages.Sum(p => Math.Min(p.Text.Length, p.IsHome ? options.HomeTextChars : options.PageTextChars));

    public static async Task<(SalesAnswer Answer, MarketModelResponse Call)> AnalyzeAsync(IMarketModel model, string input, MarketsOptions options, CancellationToken ct)
    {
        var call = await model.AskAsync(new MarketModelRequest(MarketCall.Sales, MarketPrompts.SalesInstructions, input, MarketPrompts.SalesSchema, options.SalesReasoningEffort), ct);
        return (SalesAnswer.Parse(call.Json), call);
    }
}
