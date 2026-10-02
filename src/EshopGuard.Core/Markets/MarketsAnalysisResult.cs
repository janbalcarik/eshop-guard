using EshopGuard.Core.Languages;
using EshopGuard.Core.Models;

namespace EshopGuard.Core.Markets;

/// <summary>What the model said about a row, for an internal audit only; never shown to the client.</summary>
public sealed record InternalNote(string Reason, string Uncertain);

/// <summary>Evidence of a country: verified quotes and signals in the language of the shop, the basis of the home country, the raising version.</summary>
public sealed record MarketEvidence(IReadOnlyList<VerifiedQuote> Quotes, string? HomeBasis, string? RaisedBy, InternalNote Internal);

/// <summary>A row of <c>shop.shop_markets</c>.</summary>
/// <param name="Status"><c>suggested</c> (supported, offered on 3c) or <c>unsupported</c> (kept, not shown).</param>
/// <param name="Source"><c>detected</c> (the analysis); <c>user</c> comes from 3c.</param>
public sealed record ShopMarketRow(
    string CountryCode, string? Market, bool IsHome, string Status, string EvidenceLevel, string Source, bool Preselected, bool HomeNeedsConfirmation, MarketEvidence Evidence);

/// <summary>Kinds of difference of the pairs of a version with examples (3d).</summary>
public sealed record VersionComparisonSummary(
    IReadOnlyDictionary<string, int> PairKinds, IReadOnlyList<PairComparison> Examples, double? SentenceOverlapShare, bool MandatoryPagesDiffer,
    IReadOnlyList<string> MandatoryPagesDifferUrls);

/// <summary>A row of <c>shop.shop_languages</c>.</summary>
public sealed record ShopLanguageRow
{
    public string? Language { get; init; }

    public required string BaseUrl { get; init; }

    public required string SwitchMethod { get; init; }

    public required string Source { get; init; }

    public required string Status { get; init; }

    public bool IsMain { get; init; }

    public double? OwnTextShare { get; init; }

    public IReadOnlyDictionary<string, double> LanguageShare { get; init; } = new Dictionary<string, double>();

    public VersionComparisonSummary? Comparison { get; init; }

    public bool Counted { get; init; }

    public int? ProductCount { get; init; }

    /// <summary>Codes of the version (<see cref="VersionCodes"/>).</summary>
    public IReadOnlyList<string> Codes { get; init; } = [];

    /// <summary>Warnings that are not violations (<c>untranslated_text</c>).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>What shows the version (signals, links, a quote).</summary>
    public IReadOnlyList<string> Evidence { get; init; } = [];

    /// <summary>The crawl scope of the version (for the analysis of change 8).</summary>
    public VersionCrawlScope? Scope { get; init; }
}

/// <summary>One sentence for 3c as a code with parameters; the sentence itself is composed by the application.</summary>
public sealed record VersionSummary(string Code, IReadOnlyDictionary<string, object> Params);

/// <summary>Details of a version for 3d.</summary>
public sealed record VersionDetails(
    string? Language, string BaseUrl, string Status, double? OwnTextShare, VersionComparisonSummary? Comparison, IReadOnlyList<string> Codes,
    IReadOnlyList<string> Warnings, IReadOnlyList<string> UntranslatedExamples);

/// <summary>Language and alternates group of a downloaded page (<c>content.pages.language</c>, <c>hreflang_group</c>).</summary>
public sealed record PageLanguageRow(string Url, string? Language, string? HreflangGroup);

/// <summary>Calls of the model, tokens, price and requests to the shop.</summary>
public sealed record MarketsUsage(int Calls, long InputTokens, long CachedTokens, long OutputTokens, decimal CostUsd, int Requests)
{
    public static MarketsUsage None { get; } = new(0, 0, 0, 0, 0m, 0);

    public MarketsUsage Add(MarketModelResponse? call) => call is null ? this : this with
    {
        Calls = Calls + 1,
        InputTokens = InputTokens + call.InputTokens,
        CachedTokens = CachedTokens + call.CachedTokens,
        OutputTokens = OutputTokens + call.OutputTokens,
        CostUsd = CostUsd + call.CostUsd,
    };
}

/// <summary>
/// The analysis of the places of sale and of the language versions of a shop (change 7, design section 7): rows for
/// <c>shop_markets</c> and <c>shop_languages</c>, the language of the downloaded pages, one sentence for 3c and the details for
/// 3d as codes with parameters, the plan of versions and the usage. Holds no sentence for the client; the free text of the
/// model is only in <see cref="InternalNote"/>.
/// </summary>
public sealed record MarketsAnalysisResult
{
    /// <summary>Version of this format.</summary>
    public int SchemaVersion { get; init; } = 1;

    public required string Site { get; init; }

    public DateTimeOffset AnalyzedAt { get; init; }

    /// <summary>Version of the instructions of the model.</summary>
    public string PromptVersion { get; init; } = MarketPrompts.Version;

    /// <summary>Model that answered (<c>mock</c> for the fake model), null without a call.</summary>
    public string? Model { get; init; }

    public MarketSignals? Signals { get; init; }

    /// <summary>Pages given to the analysis of the places of sale and where they came from.</summary>
    public IReadOnlyList<SelectedPage> SalesPages { get; init; } = [];

    public string? HomeCountry { get; init; }

    public string? HomeBasis { get; init; }

    public bool HomeNeedsConfirmation { get; init; }

    public IReadOnlyList<ShopMarketRow> Markets { get; init; } = [];

    /// <summary>Countries the model named without verified evidence.</summary>
    public IReadOnlyList<RejectedCountry> RejectedCountries { get; init; } = [];

    public int QuotesVerified { get; init; }

    public int QuotesDropped { get; init; }

    public IReadOnlyList<ShopLanguageRow> Versions { get; init; } = [];

    /// <summary>The sentence of 3c about the versions.</summary>
    public VersionSummary? Summary { get; init; }

    /// <summary>Other things 3c must say (another domain to confirm, a browser needed, untranslated texts), never left out.</summary>
    public IReadOnlyList<VersionSummary> Notices { get; init; } = [];

    public IReadOnlyList<VersionDetails> Details { get; init; } = [];

    public VersionPlan? Plan { get; init; }

    /// <summary>How the pairs of the analysis of versions were made (<c>hreflang</c>, <c>identifiers</c>, <c>sentence_overlap</c>), null without it.</summary>
    public string? PairingMode { get; init; }

    public IReadOnlyList<PageLanguageRow> Pages { get; init; } = [];

    /// <summary>Codes of the analysis (<see cref="MarketCodes"/>, <c>model_mock</c>, <c>model_missing_key</c>).</summary>
    public IReadOnlyList<string> Codes { get; init; } = [];

    /// <summary>Warnings of the download (robots.txt, sitemap).</summary>
    public IReadOnlyList<ScanWarning> Warnings { get; init; } = [];

    public MarketsUsage Usage { get; init; } = MarketsUsage.None;

    /// <summary>The estimate shown before the calls of the model.</summary>
    public MarketAnalysisEstimate? Estimate { get; init; }

    private static readonly System.Text.Json.JsonSerializerOptions Indented = new(Pipeline.PipelineJson.Options) { WriteIndented = true };

    /// <summary>The result as <c>markets.json</c> (snake_case, letters of the shop's language unescaped).</summary>
    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this, Indented);
}
