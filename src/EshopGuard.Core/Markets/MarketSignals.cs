namespace EshopGuard.Core.Markets;

/// <summary>A link of the home page or of the footer: its text and absolute address.</summary>
public sealed record LinkInfo(string Text, string Url);

/// <summary>An alternate of the site in a language (<c>hreflang</c>), from the home page (<c>page</c>) or the sitemap (<c>sitemap</c>).</summary>
public sealed record HreflangSignal(string Language, string Url, string Source);

/// <summary>
/// A link that may switch to another language version, recognized by the structure of its address only: <c>path</c> (a
/// language segment), <c>query</c> (a language parameter), <c>subdomain</c> (a language label) or <c>domain</c> (another
/// domain with the same first label, <c>goodie.cz</c> and <c>goodie.sk</c>). The language is null when the address does not name it.
/// </summary>
public sealed record SwitcherCandidate(string Url, string Kind, string? Language);

/// <summary>An element that switches the language only by script (no address), with the language it names, if any.</summary>
public sealed record ScriptSwitchSignal(string Tag, string? Language);

/// <summary>
/// Technical signs of the markets and language versions of a shop, read without a model from the home page and the sitemap
/// (change 7, design section 2). The model gets them ready; a quote <c>signal:&lt;name&gt;=&lt;value&gt;</c> of the model is
/// valid only when <see cref="Values"/> has it.
/// </summary>
public sealed class MarketSignals
{
    /// <summary>The site as the client gave it.</summary>
    public required string Site { get; init; }

    /// <summary><c>html lang</c> of the home page, normalized.</summary>
    public string? HtmlLang { get; init; }

    /// <summary>Alternates in languages from the home page and the sitemap.</summary>
    public IReadOnlyList<HreflangSignal> Hreflang { get; init; } = [];

    /// <summary>Currencies of prices in the structured data of the home page (ISO 4217).</summary>
    public IReadOnlyList<string> Currencies { get; init; } = [];

    /// <summary>Country calling codes of the international phone numbers of the home page (<c>421</c>).</summary>
    public IReadOnlyList<string> PhonePrefixes { get; init; } = [];

    /// <summary>Top-level domain of the site (<c>sk</c>).</summary>
    public string Tld { get; init; } = "";

    /// <summary>Links of the home page that may switch to another version, by the structure of their address.</summary>
    public IReadOnlyList<SwitcherCandidate> SwitcherCandidates { get; init; } = [];

    /// <summary>Elements that switch the language only by script.</summary>
    public IReadOnlyList<ScriptSwitchSignal> ScriptOnlySwitchElements { get; init; } = [];

    /// <summary>Signatures of translation services in the browser found among the scripts of the home page.</summary>
    public IReadOnlyList<string> BrowserTranslationWidgets { get; init; } = [];

    /// <summary>Links of the home page (at most <c>markets.max_home_links</c>).</summary>
    public IReadOnlyList<LinkInfo> HomeLinks { get; init; } = [];

    /// <summary>Links of the footer of the home page.</summary>
    public IReadOnlyList<LinkInfo> FooterLinks { get; init; } = [];

    /// <summary>The home page had almost no readable text (rendered by JavaScript).</summary>
    public bool HomeTextNotLoaded { get; init; }

    /// <summary>
    /// Every sign as a name and a value, as the model may quote them (<c>signal:hreflang=sk</c>): <c>html_lang</c>,
    /// <c>hreflang</c>, <c>currency</c>, <c>phone_prefix</c>, <c>tld</c>, <c>switcher</c> (the host of a domain switch or the
    /// language of another one), <c>script_switch</c>, <c>browser_translation</c>.
    /// </summary>
    public IEnumerable<(string Name, string Value)> Values()
    {
        if (HtmlLang is { } lang)
        {
            yield return ("html_lang", lang);
            yield return ("html_lang", LanguageTags.Primary(lang));
        }

        foreach (var alternate in Hreflang)
        {
            yield return ("hreflang", alternate.Language);
            yield return ("hreflang", LanguageTags.Primary(alternate.Language));
        }

        foreach (var currency in Currencies)
        {
            yield return ("currency", currency);
        }

        foreach (var prefix in PhonePrefixes)
        {
            yield return ("phone_prefix", prefix);
        }

        if (Tld.Length > 0)
        {
            yield return ("tld", Tld);
        }

        foreach (var switcher in SwitcherCandidates)
        {
            yield return ("switcher", switcher.Kind == "domain" ? new Uri(switcher.Url).Host : switcher.Language ?? switcher.Url);
        }

        foreach (var element in ScriptOnlySwitchElements.Where(e => e.Language is not null))
        {
            yield return ("script_switch", element.Language!);
        }

        foreach (var widget in BrowserTranslationWidgets)
        {
            yield return ("browser_translation", widget);
        }
    }
}
