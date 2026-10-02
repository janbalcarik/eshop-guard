using System.Text.Json;
using EshopGuard.Application.Contracts;
using EshopGuard.Data.Entities.Shops;

namespace EshopGuard.Application.Shops;

/// <summary>
/// The reasons of a market for 3c (change 10, AD 5) from <c>shop_markets.evidence</c> (change 7): only verified quotes (the
/// analysis stores no other) with the page they come from, and technical signs as codes with parameters (<c>seat</c>,
/// <c>tld</c>, <c>currency</c>, <c>language_version</c>, <c>phone_prefix</c>). The internal note of the model is never
/// returned.
/// </summary>
public static class MarketEvidenceMapper
{
    public const string Signal = "signal";
    public const string Citation = "citation";

    /// <summary>3c ticks a detected market in advance with strong evidence or delivery; a general claim or a manual one is not.</summary>
    public static bool Preselected(ShopMarket market)
    {
        ArgumentNullException.ThrowIfNull(market);
        return market.Source == MarketSource.Detected && market.EvidenceLevel is EvidenceLevel.Strong or EvidenceLevel.Delivery;
    }

    public static IReadOnlyList<MarketEvidenceDto> Map(JsonDocument? evidence, bool isHome)
    {
        var items = new List<MarketEvidenceDto>();
        if (evidence?.RootElement is not { ValueKind: JsonValueKind.Object } root)
        {
            return items;
        }

        if (isHome && Text(root, "home_basis") == "quote")
        {
            items.Add(new MarketEvidenceDto(Signal, "seat", new Dictionary<string, string>(), null, null));
        }

        if (root.TryGetProperty("quotes", out var quotes) && quotes.ValueKind == JsonValueKind.Array)
        {
            foreach (var quote in quotes.EnumerateArray())
            {
                var text = Text(quote, "quote") ?? "";
                var source = Text(quote, "source") ?? "";
                if (source.Equals("signal", StringComparison.OrdinalIgnoreCase) || text.StartsWith("signal:", StringComparison.OrdinalIgnoreCase))
                {
                    if (SignalOf(text.StartsWith("signal:", StringComparison.OrdinalIgnoreCase) ? text[7..] : text) is { } signal)
                    {
                        items.Add(signal);
                    }
                }
                else if (text.Length > 0)
                {
                    items.Add(new MarketEvidenceDto(Citation, "quote", new Dictionary<string, string>(), text,
                        Uri.TryCreate(source, UriKind.Absolute, out var page) && page.Scheme is "http" or "https" ? source : null));
                }
            }
        }

        if (Text(root, "raised_by") is { } raised && raised.Split('=', 2) is [var kind, var value])
        {
            items.Add(new MarketEvidenceDto(Signal, "language_version",
                new Dictionary<string, string> { [kind == "version_domain" ? "domain" : "language"] = value }, null, null));
        }

        return items.DistinctBy(i => (i.Kind, i.Code, string.Join('&', i.Params.Select(p => p.Key + "=" + p.Value)), i.Quote)).ToList();
    }

    private static MarketEvidenceDto? SignalOf(string text)
    {
        var parts = text.Trim().Split('=', 2);
        if (parts.Length != 2 || parts[1].Trim().Length == 0)
        {
            return null;
        }

        var name = parts[0].Trim().ToLowerInvariant();
        var value = parts[1].Trim();
        var (code, key) = name switch
        {
            "tld" => ("tld", "tld"),
            "currency" => ("currency", "currency"),
            "phone_prefix" => ("phone_prefix", "prefix"),
            "html_lang" or "hreflang" or "switcher" or "script_switch" => ("language_version", value.Contains('.', StringComparison.Ordinal) ? "domain" : "language"),
            _ => (name, "value"),
        };
        return new MarketEvidenceDto(Signal, code, new Dictionary<string, string> { [key] = code == "currency" ? value.ToUpperInvariant() : value }, null, null);
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
