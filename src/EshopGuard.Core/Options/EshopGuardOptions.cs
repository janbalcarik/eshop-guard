using EshopGuard.Core.Fix;
using EshopGuard.Core.Jev;

namespace EshopGuard.Core.Options;

/// <summary>
/// Root options of the EshopGuard library. The host fills them in <c>AddEshopGuard</c>.
/// </summary>
public sealed class EshopGuardOptions
{
    /// <summary>Jev API settings.</summary>
    public JevOptions Jev { get; set; } = new();

    /// <summary>Crawling rules and limits.</summary>
    public CrawlOptions Crawl { get; set; } = new();

    /// <summary>Sentence and paragraph segmentation.</summary>
    public SegmentationOptions Segmentation { get; set; } = new();

    /// <summary>Location of rule sets and label lists.</summary>
    public RulesOptions Rules { get; set; } = new();

    /// <summary>Language of the outputs written by the report writers.</summary>
    public ReportOptions Report { get; set; } = new();

    /// <summary>Price used for the cost estimate.</summary>
    public CostOptions Cost { get; set; } = new();

    /// <summary>Budget protection.</summary>
    public BudgetOptions Budget { get; set; } = new();

    /// <summary>Rewrite of problematic passages by an OpenAI model.</summary>
    public RewriteOptions Rewrite { get; set; } = new();

    /// <summary>Profiles of page templates written by the same OpenAI model; they only leave out interface text.</summary>
    public ProfileOptions Profiles { get; set; } = new();

    /// <summary>Analysis of the places of sale and of the language versions of a shop (change 7).</summary>
    public MarketsOptions Markets { get; set; } = new();
}

/// <summary>
/// Analysis of the places of sale and of the language versions (change 7). The markets themselves are data in
/// <c>config/jurisdictions.yaml</c>; these are the limits of the analysis.
/// </summary>
public sealed class MarketsOptions
{
    /// <summary>Most pages about sales and delivery the model may pick from the links of the home page and the footer.</summary>
    public int MaxSelectedPages { get; set; } = 4;

    /// <summary>Characters of the text of one picked page sent to the model.</summary>
    public int PageTextChars { get; set; } = 8_000;

    /// <summary>Characters of the text of the home page sent to the model.</summary>
    public int HomeTextChars { get; set; } = 6_000;

    /// <summary>Most links of the home page offered to the model.</summary>
    public int MaxHomeLinks { get; set; } = 300;

    /// <summary>Reasoning effort of the pick of pages (a short list of links).</summary>
    public string PickReasoningEffort { get; set; } = "low";

    /// <summary>Reasoning effort of the analysis of the places of sale.</summary>
    public string SalesReasoningEffort { get; set; } = "medium";

    /// <summary>Reasoning effort of the language of texts.</summary>
    public string LanguageReasoningEffort { get; set; } = "low";

    /// <summary>Expected output tokens of the pick (estimate).</summary>
    public int PickOutputTokens { get; set; } = 800;

    /// <summary>Expected output tokens of the analysis of the places of sale (estimate).</summary>
    public int SalesOutputTokens { get; set; } = 4_000;

    /// <summary>Expected output tokens of the language of texts of one version (estimate).</summary>
    public int LanguageOutputTokens { get; set; } = 500;

    /// <summary>
    /// Fewest product pages with sentences in the sample of a version for the language of its descriptions to rest on enough
    /// products (proposal, not measured); with fewer the version has <c>version_sample_insufficient</c> (a notice for 3c).
    /// </summary>
    public int MinSampleProducts { get; set; } = 10;

    /// <summary>Pages of the sample of the analysis of versions (all versions together).</summary>
    public int SamplePages { get; set; } = 100;

    /// <summary>Random products of a version whose description the model labels with its language.</summary>
    public int LanguageProducts { get; set; } = 20;

    /// <summary>
    /// Most random pages of the sitemap of a version without a product sitemap downloaded to find its products by the
    /// structure of the page (proposal, not measured).
    /// </summary>
    public int ProductProbePages { get; set; } = 40;

    /// <summary>Sentences of the product descriptions of a version whose language the model labels (two of each product).</summary>
    public int LanguageFragmentsPerVersion { get; set; } = 40;

    /// <summary>Shortest and longest sentence sent for the language of texts.</summary>
    public int LanguageFragmentMinChars { get; set; } = 30;

    /// <summary>Longest sentence sent for the language of texts.</summary>
    public int LanguageFragmentMaxChars { get; set; } = 300;

    /// <summary>
    /// Signatures of scripts of translation services in the browser, matched in the address of a script (technical signs like
    /// the framework names of the render check, never words of the text): a version made by them has no text of its own.
    /// </summary>
    public List<string> BrowserTranslationScripts { get; set; } = ["weglot", "gtranslate", "translate.google", "conveythis", "localizejs", "transifex"];
}

/// <summary>
/// Profiles of page templates. A profile lists the regions of a template with a CSS selector and whether their text is
/// checked or skipped (navigation, listings of other products, cookie bar, forms, cart); it is written once by the
/// rewrite model from outlines of sample pages, stored and reused. A page uses the stored profile that leaves the least
/// of its text outside the known regions; when even the best one leaves more than <see cref="MaxUnknownShare"/>, the page
/// does not fit any profile, is checked whole and its template gets a profile of its own.
/// </summary>
public sealed class ProfileOptions
{
    /// <summary>When false, no profile is used or written and every page is checked whole.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// A page fits a profile when at most this share of its visible text is outside the regions of the profile.
    /// Measured on 1. 10. 2026 (vegis.sk, naturfyt.sk, 16 pages): 0 % with the shop's own profile, 94–97 % with the other.
    /// </summary>
    public double MaxUnknownShare { get; set; } = 0.10;

    /// <summary>
    /// Pages of one template are at least this similar in structure (Jaccard index of element and class names). Measured on
    /// 1. 10. 2026: the same template 0.83–0.99, other page types of the same shop 0.32–0.67, another shop 0.04–0.05.
    /// </summary>
    public double TemplateSimilarity { get; set; } = 0.75;

    /// <summary>A template gets a profile only when at least this many pages without a profile share it.</summary>
    public int MinTemplatePages { get; set; } = 3;

    /// <summary>At most this many new profiles are written in one scan; the other pages are checked whole.</summary>
    public int MaxNewProfilesPerScan { get; set; } = 5;

    /// <summary>Sample pages sent to the model for one profile.</summary>
    public int SamplePages { get; set; } = 3;

    /// <summary>Longest outline of one sample page in characters; a longer one is cut.</summary>
    public int MaxOutlineChars { get; set; } = 80_000;

    /// <summary>Output tokens of one profile assumed by the estimate (measured 3 074 and 3 243 on 1. 10. 2026).</summary>
    public int EstimatedOutputTokens { get; set; } = 3_500;

    /// <summary>Above this estimated price of new profiles in USD the host must confirm the run.</summary>
    public decimal MaxUsdWithoutConfirm { get; set; } = 1.00m;
}

/// <summary>
/// Crawling rules. Defaults follow the specification: 1 request per second, 20 s timeout, 5 MB pages.
/// </summary>
public sealed class CrawlOptions
{
    /// <summary>Maximum number of downloaded pages. The home page and legal pages are always downloaded.</summary>
    public int MaxPages { get; set; } = 200;

    /// <summary>Maximum number of product pages included in the analysis.</summary>
    public int SampleProducts { get; set; } = 100;

    /// <summary>Requests per second to the scanned site at the start. Zero or less means no delay (tests only).</summary>
    public double RequestsPerSecond { get; set; } = 1.0;

    /// <summary>
    /// Upper limit of the adaptive pace: while the server answers quickly and without errors, the pace rises from
    /// <see cref="RequestsPerSecond"/> up to this value; Crawl-delay in robots.txt lowers it.
    /// </summary>
    public double MaxRequestsPerSecond { get; set; } = 3.0;

    /// <summary>User-Agent with a contact, e.g. <c>EshopGuard/0.1 (+mailto:...)</c>.</summary>
    public string UserAgent { get; set; } = "EshopGuard/0.1";

    /// <summary>Timeout of one request in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>Maximum size of one response in bytes.</summary>
    public long MaxPageBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Maximum number of redirects followed within the same site.</summary>
    public int MaxRedirects { get; set; } = 5;

    /// <summary>Link depth when the site has no sitemap.</summary>
    public int MaxLinkDepth { get; set; } = 3;

    /// <summary>
    /// Maximum size of one sitemap file in bytes: the protocol allows 50 MB, and product sitemaps of large shops are bigger
    /// than a page (bonami.sk, 2. 10. 2026: <c>product_0.xml</c> over 5 MB).
    /// </summary>
    public long MaxSitemapBytes { get; set; } = 50 * 1024 * 1024;

    /// <summary>Maximum number of URLs read from sitemaps.</summary>
    public int MaxSitemapUrls { get; set; } = 50_000;

    /// <summary>
    /// Longest extraction of one page in seconds; a page that takes longer is not read and is reported among the pages
    /// that were not checked. SmartReader cannot be interrupted, so its work finishes in the background.
    /// </summary>
    public int ExtractTimeoutSeconds { get; set; } = 30;

    /// <summary>Pages downloaded in one batch of the fetch step; the state of the crawl is kept between batches.</summary>
    public int FetchBatchMaxPages { get; set; } = 100;

    /// <summary>Longest batch of the fetch step in seconds; the next batch goes on where it stopped.</summary>
    public int FetchBatchMaxSeconds { get; set; } = 60;

    /// <summary>
    /// Allows addresses in internal and local networks and ports other than 80 and 443. Off by default (protection against
    /// SSRF); only for a local test shop (<c>--allow-private-network</c>), never in the web application.
    /// </summary>
    public bool AllowPrivateNetwork { get; set; }

    /// <summary>
    /// A page with less readable text in its HTML (header, menu and footer included) is reported as not loaded:
    /// the site most likely renders it with JavaScript, which the tool does not run.
    /// </summary>
    public int MinPageTextChars { get; set; } = 200;

    /// <summary>URL and title fragments that identify legal pages.</summary>
    public List<string> LegalPageSlugs { get; set; } =
    [
        "obchodni-podminky", "obchodne-podmienky", "reklamacni-rad", "reklamacny-poriadok",
        "odstoupeni", "odstupenie", "vraceni", "vratenie", "doprava", "kontakt",
    ];

    /// <summary>Sitemap URL fragments that suggest the sitemap lists product pages.</summary>
    public List<string> ProductSitemapHints { get; set; } = ["product", "produkt", "zbozi", "tovar", "item"];

    /// <summary>Regular expressions of URLs that are never downloaded (cart, login, search, sorting, paging, filters).</summary>
    public List<string> ExcludeUrlPatterns { get; set; } =
    [
        @"/(kosik|cart|basket|checkout|pokladna|objednavka|order)(/|$|\.|\?)",
        @"/(prihlaseni|prihlasenie|login|signin|registrace|registracia|register|muj-ucet|moj-ucet|account|customer)(/|$|\.|\?)",
        @"/(vyhledavani|hledani|vyhladavanie|hladanie|search)(/|$|\.|\?)",
        @"/(porovnani|porovnanie|compare|wishlist|oblibene|oblubene)(/|$|\.|\?)",
        @"/(page|strana|stranka)[-/]\d+",
        @"[?&](q|query|search|s|sort|order|orderby|razeni|dir|filter|filtr|f|limit|view|page|strana|stranka)=",
    ];
}

/// <summary>
/// Segmentation settings.
/// </summary>
public sealed class SegmentationOptions
{
    /// <summary>
    /// Segments shorter than this many characters are dropped. Characters are counted after whitespace is
    /// collapsed; letters, digits, spaces and punctuation all count.
    /// </summary>
    public int MinSegmentLength { get; set; } = 3;

    /// <summary>
    /// A segment must contain at least one word of this many letters (diacritics included, digits and symbols not),
    /// so prices, ratings and codes such as "189,90 Kč" or "★★★★★" are dropped. Zero turns the check off.
    /// </summary>
    public int MinWordLetters { get; set; } = 3;

    /// <summary>Sentences longer than this are split.</summary>
    public int MaxSentenceLength { get; set; } = 600;

    /// <summary>Maximum length of a legal paragraph including its heading.</summary>
    public int MaxParagraphLength { get; set; } = 1500;

    /// <summary>Number of neighbouring sentences on each side sent to Jev as context.</summary>
    public int ContextSentences { get; set; } = 2;

    /// <summary>Maximum length of the context on each side.</summary>
    public int MaxContextChars { get; set; } = 600;

    /// <summary>Text found on a larger share of pages is marked as boilerplate.</summary>
    public double BoilerplatePageShare { get; set; } = 0.3;

    /// <summary>Text must be on at least this many pages to be marked as boilerplate.</summary>
    public int MinBoilerplatePages { get; set; } = 3;

    /// <summary>Abbreviations that never end a sentence (without the trailing dot, lower case).</summary>
    public List<string> NonTerminalAbbreviations { get; set; } =
    [
        "např", "napr", "tzv", "tj", "tzn", "resp", "cca", "č", "čís", "min", "max", "mj", "popř", "príp", "příp",
        "pozn", "str", "sv", "ul", "tel", "kupř", "zejm", "najm", "vč", "vr", "nám", "mil", "tis", "mld", "hod",
        "ks", "obr", "tab", "ing", "mgr", "bc", "mudr", "judr", "phdr", "rndr", "doc", "prof", "dr", "st",
    ];

    /// <summary>Abbreviations that end a sentence when an upper-case letter follows.</summary>
    public List<string> TerminalAbbreviations { get; set; } =
    [
        "atd", "atď", "apod", "s.r.o", "spol", "a.s", "v.o.s", "k.s", "o.p.s", "z.s", "inc", "ltd",
    ];
}

/// <summary>
/// Location of rule sets, label lists and the list of legal requirements.
/// </summary>
public sealed class RulesOptions
{
    /// <summary>Directory with YAML rule sets.</summary>
    public string Directory { get; set; } = "rules";

    /// <summary>YAML file with label lists and image keywords.</summary>
    public string LabelsFile { get; set; } = "config/labels.yaml";

    /// <summary>YAML file with the list of legal requirements for all products of a category (SK point 15).</summary>
    public string LegalRequirementsFile { get; set; } = "config/legal_requirements.yaml";

    /// <summary>YAML file with the block sieve; when it is missing, the sieve is off.</summary>
    public string SieveFile { get; set; } = "config/sieve.yaml";

    /// <summary>YAML file with the known jurisdictions and the language of their laws.</summary>
    public string JurisdictionsFile { get; set; } = "config/jurisdictions.yaml";

    /// <summary>YAML file with the technical signatures of the platforms of e-shops (change 10).</summary>
    public string PlatformsFile { get; set; } = "config/platforms.yaml";

    /// <summary>Folder with the texts of the rules, one subfolder per language; null means <c>texts</c> in <see cref="Directory"/>.</summary>
    public string? TextsDirectory { get; set; }

    /// <summary>Languages of the texts to load; empty means every subfolder of the texts folder.</summary>
    public List<string> Locales { get; set; } = [];

    /// <summary>
    /// Languages whose texts of the tool and of the labels must be complete, otherwise the rules do not load. Texts of a rule
    /// set missing in such a language are shown in the language the set was written in.
    /// </summary>
    public List<string> RequiredLocales { get; set; } = ["cs"];

    /// <summary>JSON file with the fingerprint of the questions of every rule set version; null means <c>question-set-hashes.json</c> in <see cref="Directory"/>.</summary>
    public string? QuestionSetHashesFile { get; set; }

    /// <summary>The texts folder.</summary>
    public string ResolvedTextsDirectory => TextsDirectory ?? Path.Combine(Directory, "texts");

    /// <summary>The file of question set fingerprints.</summary>
    public string ResolvedQuestionSetHashesFile => QuestionSetHashesFile ?? Path.Combine(Directory, "question-set-hashes.json");

    /// <summary>Makes every relative path of the rules and their data absolute under <paramref name="root"/> (a host with a content root).</summary>
    public void ResolveUnder(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Directory = Rooted(root, Directory);
        LabelsFile = Rooted(root, LabelsFile);
        LegalRequirementsFile = Rooted(root, LegalRequirementsFile);
        SieveFile = Rooted(root, SieveFile);
        JurisdictionsFile = Rooted(root, JurisdictionsFile);
        PlatformsFile = Rooted(root, PlatformsFile);
        TextsDirectory = TextsDirectory is null ? null : Rooted(root, TextsDirectory);
        QuestionSetHashesFile = QuestionSetHashesFile is null ? null : Rooted(root, QuestionSetHashesFile);
    }

    private static string Rooted(string root, string path) => Path.IsPathRooted(path) ? path : Path.Combine(root, path);
}

/// <summary>
/// Language of the texts in the outputs of the host (report, CSV and JSON of findings, prompt of the rewrite fallback).
/// </summary>
public sealed class ReportOptions
{
    /// <summary>Language of the texts of rules and of the tool, e.g. <c>cs</c>.</summary>
    public string Locale { get; set; } = "cs";
}

/// <summary>
/// Price used for the cost estimate.
/// </summary>
public sealed class CostOptions
{
    /// <summary>USD per million input tokens; output tokens are free.</summary>
    public decimal UsdPerMillionInputTokens { get; set; } = 0.042m;
}

/// <summary>
/// Budget protection.
/// </summary>
public sealed class BudgetOptions
{
    /// <summary>Above this number of Jev calls the host must confirm the run.</summary>
    public int MaxCallsWithoutConfirm { get; set; } = 5000;
}
