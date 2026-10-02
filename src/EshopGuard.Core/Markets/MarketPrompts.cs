namespace EshopGuard.Core.Markets;

/// <summary>
/// Instructions and answer schemas of the model in the analysis of the places of sale and of the language of texts
/// (change 7, design section 3). Written from the design on 2. 10. 2026; the research scripts of 1. 10. 2026 are kept only
/// locally, so the measured numbers of the research (62 of 62 quotes, 44 of 46 pairs) hold for these instructions only
/// after the paid checks 7.3 and 7.4. Every country and the home country stand on a verbatim quote or a technical signal
/// that <see cref="QuoteVerifier"/> finds; the free text of the model is never shown to the client.
/// </summary>
internal static class MarketPrompts
{
    /// <summary>Version of the instructions, part of the prompt cache key and of the result.</summary>
    public const string Version = "markets-2026-10-02";

    public const string PickInstructions = """
        You help a consumer-law checker find out in which countries an e-shop sells.
        You get the links of the shop's home page and footer, one per line as "text | URL".
        Pick at most 4 pages on which the shop most likely states:
        - to which countries it delivers, at what price, with which carriers (shipping, delivery and payment pages);
        - who operates the shop and where the operator is established (terms and conditions, contact, about us, imprint);
        - in which countries or currencies it sells.
        Prefer a page about delivery or shipping and the terms and conditions. Copy URLs exactly from the list; never invent
        or change a URL. Return an empty list when no link fits.
        """;

    public const string SalesInstructions = """
        You determine where an e-shop sells, for a consumer-law checker. Every statement must be proven by a verbatim quote
        from the pages below or by one of the technical signals listed.
        Input: technical signals of the site as "name=value" lines, then pages as "=== PAGE <URL> ===" followed by their text.
        Signal "currency" is a currency of the prices on the home page; "offered_currency" is a currency the shop's own currency
        switch offers (all prices of the shop can be shown in it); "switcher" is a link to another language version.

        Return:
        - home_country: ISO 3166-1 alpha-2 code (upper case) of the country where the operator of the shop is established
          (the registered seat or the address of the seller, usually in the terms, contact or footer); empty string when no
          page states it. home_evidence: the quotes that prove it.
        - countries: every country whose consumers the shop sells or delivers to. For each country:
          - country: ISO 3166-1 alpha-2, upper case;
          - evidence_level:
            - "strong": the shop targets consumers of that country: its own language version or domain for the country,
              prices in the currency of the country, the seat of the seller there, or terms naming the consumer authority,
              courts or laws of that country;
            - "delivery": concrete delivery terms for that country: a price, a carrier or a delivery time for it, also as a
              row of a table of shipping prices;
            - "generic": only a general statement that does not name the country, such as delivery "to the whole EU",
              "to Europe" or "worldwide";
          - evidence: quotes proving the country; each is either a short passage (at most 200 characters) copied character for
            character from one page, with source = the URL of that page, or a technical signal with quote = "name=value"
            exactly as listed and source = "signal";
          - reason: one short sentence for an internal audit.
          Never list a country only because of the language of the texts: customers in Slovakia and Czechia read both Slovak
          and Czech. A table of shipping prices to many countries is "delivery" for each of them, never "strong".
        - eu_wide_delivery: stated = true when a page says in general that the shop delivers to the whole EU or Europe, with
          that quote and its source page; otherwise false with empty quote and source.
        - delivery_terms: found = true when a page states delivery terms (countries, prices or carriers), with one quote and
          its source page; otherwise false with empty quote and source.
        - language_versions: other language versions of the shop named on the pages or in the signals: language (BCP 47 tag,
          lower case), url (absolute, empty when unknown), switch ("path", "subdomain", "domain", "query",
          "cookie_or_script" or "unknown") and evidence (a quote or a signal, as above).
        - uncertain: what could not be decided and why (internal audit), or an empty string.

        Rules: copy quotes exactly, never translate, shorten inside or join two passages; use only the pages and signals
        given; when unsure, leave a country out or mark it "generic" and say why in uncertain.
        """;

    public const string LanguageInstructions = """
        You label the language of short texts from an e-shop for a consumer-law checker.
        For every numbered text return its id and its language as a lower-case BCP 47 primary subtag ("cs", "sk", "en",
        "de", "pl", "hu", ...), or "und" when the text has no language of its own (a name, a code, numbers).
        Decide by the whole text, not by single words or product names. Czech and Slovak are different languages; tell them
        apart carefully. Return a label for every id.
        """;

    private static Dictionary<string, object> Str() => new() { ["type"] = "string" };

    private static Dictionary<string, object> Obj(Dictionary<string, object> properties) => new()
    {
        ["type"] = "object",
        ["properties"] = properties,
        ["required"] = properties.Keys.ToArray(),
        ["additionalProperties"] = false,
    };

    private static Dictionary<string, object> Arr(object items) => new() { ["type"] = "array", ["items"] = items };

    private static Dictionary<string, object> Quote() => Obj(new() { ["quote"] = Str(), ["source"] = Str() });

    /// <summary>Schema of the pick of pages.</summary>
    public static object PickSchema { get; } = Obj(new() { ["urls"] = Arr(Str()), ["reason"] = Str() });

    /// <summary>Schema of the analysis of the places of sale.</summary>
    public static object SalesSchema { get; } = Obj(new()
    {
        ["home_country"] = Str(),
        ["home_evidence"] = Arr(Quote()),
        ["countries"] = Arr(Obj(new()
        {
            ["country"] = Str(),
            ["evidence_level"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = new[] { "strong", "delivery", "generic" } },
            ["evidence"] = Arr(Quote()),
            ["reason"] = Str(),
        })),
        ["eu_wide_delivery"] = Obj(new() { ["stated"] = new Dictionary<string, object> { ["type"] = "boolean" }, ["quote"] = Str(), ["source"] = Str() }),
        ["delivery_terms"] = Obj(new() { ["found"] = new Dictionary<string, object> { ["type"] = "boolean" }, ["quote"] = Str(), ["source"] = Str() }),
        ["language_versions"] = Arr(Obj(new()
        {
            ["language"] = Str(),
            ["url"] = Str(),
            ["switch"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = new[] { "path", "subdomain", "domain", "query", "cookie_or_script", "unknown" } },
            ["evidence"] = Quote(),
        })),
        ["uncertain"] = Str(),
    });

    /// <summary>Schema of the language of texts.</summary>
    public static object LanguageSchema { get; } = Obj(new()
    {
        ["labels"] = Arr(Obj(new() { ["id"] = new Dictionary<string, object> { ["type"] = "integer" }, ["language"] = Str() })),
    });
}
