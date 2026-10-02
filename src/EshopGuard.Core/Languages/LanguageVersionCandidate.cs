namespace EshopGuard.Core.Languages;

/// <summary>States of a language version (<c>shop.shop_languages.status</c>).</summary>
public static class VersionStatus
{
    /// <summary>Checked when a market needs it.</summary>
    public const string Active = "active";

    /// <summary>On another domain than the shop: not checked nor counted until the client confirms it belongs to the shop.</summary>
    public const string NeedsConfirmation = "needs_confirmation";

    /// <summary>In the language of a market the tool does not check: kept, not checked, not shown.</summary>
    public const string Unsupported = "unsupported";

    /// <summary>Switched only by script: not checked until pages are rendered in a browser.</summary>
    public const string NeedsBrowser = "needs_browser";

    /// <summary>Its address answers with another language (redirect by IP or language): not checked, a connector is offered.</summary>
    public const string Mismatch = "mismatch";

    /// <summary>Left out by the client (setting of the shop).</summary>
    public const string Excluded = "excluded";
}

/// <summary>How a version is reached (<c>shop.shop_languages.switch_method</c>).</summary>
public static class SwitchMethods
{
    public const string Path = "path";
    public const string Subdomain = "subdomain";
    public const string Domain = "domain";
    public const string Query = "query";
    public const string Cookie = "cookie";
    public const string AcceptLanguage = "accept_language";
    public const string Script = "script";
    public const string BrowserTranslation = "browser_translation";

    /// <summary>Not known before the probe: the version has no address of its own (cookie, browser language, script, translation).</summary>
    public const string Unknown = "unknown";
}

/// <summary>Where a version was found (<c>shop.shop_languages.source</c>).</summary>
public static class VersionSources
{
    /// <summary>The site the client gave.</summary>
    public const string Main = "main";

    public const string Hreflang = "hreflang";
    public const string Switcher = "switcher";
    public const string Connector = "connector";

    /// <summary>Named only by the model; tried by a download before it is used.</summary>
    public const string Llm = "llm";
}

/// <summary>Codes of versions for the client (sentence on 3c, details on 3d).</summary>
public static class VersionCodes
{
    public const string NeedsBrowser = "version_needs_browser";
    public const string BrowserTranslation = "version_browser_translation";
    public const string LanguageMismatch = "version_language_mismatch";
    public const string TextNotLoaded = "version_text_not_loaded";
    public const string OtherDomainNeedsConfirmation = "version_other_domain_needs_confirmation";
    public const string SampleInsufficient = "version_sample_insufficient";
    public const string ProductCountUnknown = "version_product_count_unknown";

    /// <summary>A product sitemap was not read whole: the number of products is unknown, the row has its lower bound.</summary>
    public const string ProductCountIncomplete = "version_product_count_incomplete";
    public const string LanguageUnknown = "version_language_unknown";
    public const string NotNeeded = "version_not_needed";
    public const string Unreachable = "version_unreachable";
    public const string RobotsDisallowed = "version_robots_disallowed";
    public const string UntranslatedText = "untranslated_text";
}

/// <summary>
/// A language version of a shop: its language (BCP 47, lower case; null until a download shows it), its root address, how it
/// is switched, where it was found and its state. After <see cref="VersionAccessProbe"/> it has the crawl scope it is checked in.
/// </summary>
public sealed record LanguageVersionCandidate
{
    public string? Language { get; init; }

    /// <summary>Root address of the version (the home page of the site for the main version).</summary>
    public required string BaseUrl { get; init; }

    public required string SwitchMethod { get; init; }

    public required string Source { get; init; }

    public string Status { get; init; } = VersionStatus.Active;

    /// <summary>The version of the site the client gave.</summary>
    public bool IsMain { get; init; }

    /// <summary>What shows the version: signals (<c>hreflang=sk</c>), links, a quote of the model.</summary>
    public IReadOnlyList<string> Evidence { get; init; } = [];

    /// <summary>Codes of the version (<see cref="VersionCodes"/>).</summary>
    public IReadOnlyList<string> Codes { get; init; } = [];

    /// <summary><c>html lang</c> of its home page as downloaded by the probe; null before the probe.</summary>
    public string? DeclaredLanguage { get; init; }

    /// <summary>The crawl scope of the version after the probe; null when it cannot be crawled on its own.</summary>
    public VersionCrawlScope? Scope { get; init; }

    /// <summary>The version may be checked: active and with a scope.</summary>
    public bool IsCheckable => Status == VersionStatus.Active && Scope is not null;
}
