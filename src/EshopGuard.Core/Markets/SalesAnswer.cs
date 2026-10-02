using System.Text.Json;
using System.Text.Json.Nodes;

namespace EshopGuard.Core.Markets;

/// <summary>A quote of the model: a passage copied from a page (<see cref="Source"/> = its URL) or a signal (<c>signal</c>, <c>name=value</c>).</summary>
public sealed record ModelQuote(string Quote, string Source);

/// <summary>A country in the answer of the model, not yet verified.</summary>
public sealed record CountryAnswer(string Country, string EvidenceLevel, IReadOnlyList<ModelQuote> Evidence, string Reason);

/// <summary>A language version named by the model, not yet verified.</summary>
public sealed record VersionAnswer(string Language, string Url, string Switch, ModelQuote? Evidence);

/// <summary>A yes or no of the model with its quote (general delivery to the EU, delivery terms found).</summary>
public sealed record FlagAnswer(bool Value, ModelQuote? Evidence);

/// <summary>The answer of the analysis of the places of sale as the model returned it (<see cref="MarketPrompts.SalesSchema"/>).</summary>
public sealed record SalesAnswer
{
    public string HomeCountry { get; init; } = "";

    public IReadOnlyList<ModelQuote> HomeEvidence { get; init; } = [];

    public IReadOnlyList<CountryAnswer> Countries { get; init; } = [];

    public FlagAnswer EuWideDelivery { get; init; } = new(false, null);

    public FlagAnswer DeliveryTerms { get; init; } = new(false, null);

    public IReadOnlyList<VersionAnswer> LanguageVersions { get; init; } = [];

    /// <summary>What the model could not decide (internal audit only).</summary>
    public string Uncertain { get; init; } = "";

    /// <summary>Reads the JSON of the model; fields missing or of another type are empty.</summary>
    /// <exception cref="JsonException">The text is not JSON.</exception>
    public static SalesAnswer Parse(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject ?? throw new JsonException("The answer is not a JSON object.");
        return new SalesAnswer
        {
            HomeCountry = Text(root["home_country"]),
            HomeEvidence = Quotes(root["home_evidence"]),
            Countries = (root["countries"] as JsonArray ?? []).OfType<JsonObject>()
                .Select(c => new CountryAnswer(Text(c["country"]), Text(c["evidence_level"]), Quotes(c["evidence"]), Text(c["reason"])))
                .ToList(),
            EuWideDelivery = Flag(root["eu_wide_delivery"], "stated"),
            DeliveryTerms = Flag(root["delivery_terms"], "found"),
            LanguageVersions = (root["language_versions"] as JsonArray ?? []).OfType<JsonObject>()
                .Select(v => new VersionAnswer(Text(v["language"]), Text(v["url"]), Text(v["switch"]), Quote(v["evidence"])))
                .ToList(),
            Uncertain = Text(root["uncertain"]),
        };
    }

    /// <summary>The URLs of the pick of pages.</summary>
    public static IReadOnlyList<string> ParsePick(string json) =>
        ((JsonNode.Parse(json) as JsonObject)?["urls"] as JsonArray ?? []).Select(Text).Where(u => u.Length > 0).ToList();

    private static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : "";

    private static ModelQuote? Quote(JsonNode? node) =>
        node is JsonObject quote && Text(quote["quote"]) is { Length: > 0 } text ? new ModelQuote(text, Text(quote["source"])) : null;

    private static List<ModelQuote> Quotes(JsonNode? node) => (node as JsonArray ?? []).Select(Quote).OfType<ModelQuote>().ToList();

    private static FlagAnswer Flag(JsonNode? node, string name) =>
        node is JsonObject flag
            ? new FlagAnswer(flag[name] is JsonValue v && v.TryGetValue<bool>(out var b) && b, Quote(flag))
            : new FlagAnswer(false, null);
}
