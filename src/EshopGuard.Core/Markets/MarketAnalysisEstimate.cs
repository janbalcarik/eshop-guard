using EshopGuard.Core.Fix;
using EshopGuard.Core.Options;

namespace EshopGuard.Core.Markets;

/// <summary>
/// Price of the calls of the model in the analysis of markets and versions, before they are sent (change 7, design section 8):
/// tokens = characters / 3.2, input and output at the prices of <c>rewrite</c>; output 800 tokens for the pick, 4 000 for the
/// analysis of the places of sale and 500 for the language of the texts of a version. Checked on 1. 10. 2026: 0.03–0.08 USD
/// per shop for the places of sale, 0.043 USD for the language of 46 pairs of 5 shops.
/// </summary>
public sealed record MarketAnalysisEstimate(int Calls, long InputTokens, long OutputTokens, decimal CostUsd)
{
    /// <summary>Of <see cref="CostUsd"/>, the new profiles of page templates (priced from the outlines of their sample pages).</summary>
    public decimal ProfilesUsd { get; init; }

    /// <summary>Characters per token of the estimate.</summary>
    public const double CharsPerToken = 3.2;

    /// <summary>Characters of the technical signals in the input of the analysis (the design counts 2 000).</summary>
    public const int SignalChars = 2_000;

    /// <summary>Nothing.</summary>
    public static MarketAnalysisEstimate None { get; } = new(0, 0, 0, 0m);

    /// <summary>The pick of pages over a list of links of the given length.</summary>
    public static MarketAnalysisEstimate Pick(int linkChars, RewriteOptions rewrite, MarketsOptions markets) =>
        Of(MarketPrompts.PickInstructions.Length + linkChars, markets.PickOutputTokens, rewrite);

    /// <summary>The analysis of the places of sale over pages of the given length (home page and picked pages, already cut).</summary>
    public static MarketAnalysisEstimate Sales(int pageChars, RewriteOptions rewrite, MarketsOptions markets) =>
        Of(MarketPrompts.SalesInstructions.Length + SignalChars + pageChars, markets.SalesOutputTokens, rewrite);

    /// <summary>The language of the texts of one version over fragments of the given length.</summary>
    public static MarketAnalysisEstimate Language(int fragmentChars, RewriteOptions rewrite, MarketsOptions markets) =>
        Of(MarketPrompts.LanguageInstructions.Length + fragmentChars, markets.LanguageOutputTokens, rewrite);

    /// <summary>
    /// The most the whole analysis of a shop may cost: the pick over its links, the analysis over the home page and the most
    /// pages at their limits, and the language of the most fragments of each version.
    /// </summary>
    public static MarketAnalysisEstimate Before(int linkChars, int versions, RewriteOptions rewrite, MarketsOptions markets) =>
        Pick(linkChars, rewrite, markets)
        + Sales(markets.HomeTextChars + markets.MaxSelectedPages * markets.PageTextChars, rewrite, markets)
        + (versions > 1 ? Language(markets.LanguageFragmentsPerVersion * markets.LanguageFragmentMaxChars, rewrite, markets).Times(versions) : None);

    /// <summary>New profiles of page templates for the comparison of versions (the plan of <c>ProfileStep</c>).</summary>
    public static MarketAnalysisEstimate Profiles(int calls, decimal usd) => new(calls, 0, 0, usd) { ProfilesUsd = usd };

    public static MarketAnalysisEstimate operator +(MarketAnalysisEstimate a, MarketAnalysisEstimate b) =>
        new(a.Calls + b.Calls, a.InputTokens + b.InputTokens, a.OutputTokens + b.OutputTokens, a.CostUsd + b.CostUsd) { ProfilesUsd = a.ProfilesUsd + b.ProfilesUsd };

    private MarketAnalysisEstimate Times(int count) => new(Calls * count, InputTokens * count, OutputTokens * count, CostUsd * count);

    private static MarketAnalysisEstimate Of(int inputChars, int outputTokens, RewriteOptions rewrite)
    {
        var input = (long)Math.Ceiling(inputChars / CharsPerToken);
        var cost = input * rewrite.InputUsdPerMillion / 1_000_000m + outputTokens * rewrite.OutputUsdPerMillion / 1_000_000m;
        return new MarketAnalysisEstimate(1, input, outputTokens, cost);
    }
}
