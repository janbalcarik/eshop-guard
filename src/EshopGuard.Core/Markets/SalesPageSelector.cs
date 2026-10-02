using System.Text;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Options;

namespace EshopGuard.Core.Markets;

/// <summary>A page for the analysis of the places of sale and where it came from: <c>model</c>, <c>connector</c> or <c>legal</c>.</summary>
public sealed record SelectedPage(string Url, string Source);

/// <summary>The pages for the analysis, the URLs of the model that were not used and the call of the model.</summary>
public sealed record SalesPageSelection(IReadOnlyList<SelectedPage> Pages, IReadOnlyList<string> RejectedUrls, MarketModelResponse? Call);

/// <summary>
/// The model picks at most <c>markets.max_selected_pages</c> pages about sales and delivery from the links of the home page and
/// the footer (change 7, design section 3.1). Only URLs of the list are accepted, of the same site or of a candidate of another
/// version. When the pick gives fewer pages, pages of the connector and legal pages the sample already downloaded fill it up.
/// </summary>
internal static class SalesPageSelector
{
    public const string HomeHeading = "HOME PAGE LINKS:";
    public const string FooterHeading = "FOOTER LINKS:";

    /// <summary>The list of links for the model: "text | URL" per line, the home page first, the footer last.</summary>
    public static string Input(MarketSignals signals)
    {
        var text = new StringBuilder();
        text.AppendLine(HomeHeading);
        foreach (var link in signals.HomeLinks)
        {
            text.Append(Clean(link.Text)).Append(" | ").AppendLine(link.Url);
        }

        text.AppendLine().AppendLine(FooterHeading);
        foreach (var link in signals.FooterLinks)
        {
            text.Append(Clean(link.Text)).Append(" | ").AppendLine(link.Url);
        }

        return text.ToString();
    }

    /// <summary>Asks the model and fills the pick up; a failure of the model is thrown to the caller.</summary>
    public static async Task<SalesPageSelection> SelectAsync(
        IMarketModel model, MarketSignals signals, Uri site, IReadOnlyList<Uri> connectorPages, IReadOnlyList<Uri> legalPages,
        MarketsOptions options, CancellationToken ct)
    {
        var call = await model.AskAsync(new MarketModelRequest(MarketCall.Pick, MarketPrompts.PickInstructions, Input(signals), MarketPrompts.PickSchema, options.PickReasoningEffort), ct);
        var (picked, rejected) = Validate(SalesAnswer.ParsePick(call.Json), signals, site, options.MaxSelectedPages);
        return new SalesPageSelection(WithFallback(picked, connectorPages, legalPages, options.MaxSelectedPages), rejected, call);
    }

    /// <summary>The URLs of the model that are in the list of links, of the site or of a candidate version, at most <paramref name="max"/>.</summary>
    public static (List<string> Picked, List<string> Rejected) Validate(IEnumerable<string> urls, MarketSignals signals, Uri site, int max)
    {
        var listed = signals.HomeLinks.Concat(signals.FooterLinks).Select(l => Key(l.Url)).ToHashSet(StringComparer.Ordinal);
        var versionHosts = signals.SwitcherCandidates.Select(c => new Uri(c.Url).Host).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var picked = new List<string>();
        var rejected = new List<string>();
        foreach (var url in urls)
        {
            var ok = Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && listed.Contains(Key(url))
                && (UrlTools.IsSameSite(uri!, site) || versionHosts.Contains(uri!.Host))
                && !picked.Contains(UrlTools.Normalize(uri!).AbsoluteUri)
                && picked.Count < max;
            if (ok)
            {
                picked.Add(UrlTools.Normalize(uri!).AbsoluteUri);
            }
            else
            {
                rejected.Add(url);
            }
        }

        return (picked, rejected);
    }

    /// <summary>The pick, then pages of the connector, then legal pages of the sample, until there are <paramref name="max"/>.</summary>
    public static List<SelectedPage> WithFallback(IReadOnlyList<string> picked, IEnumerable<Uri> connectorPages, IEnumerable<Uri> legalPages, int max)
    {
        var pages = picked.Select(u => new SelectedPage(u, "model")).ToList();
        foreach (var (url, source) in connectorPages.Select(u => (u, "connector")).Concat(legalPages.Select(u => (u, "legal"))))
        {
            var key = UrlTools.Normalize(url).AbsoluteUri;
            if (pages.Count < max && !pages.Any(p => p.Url == key))
            {
                pages.Add(new SelectedPage(key, source));
            }
        }

        return pages;
    }

    private static string Key(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? UrlTools.Normalize(uri).AbsoluteUri : url;

    private static string Clean(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Replace("|", "/");
}
