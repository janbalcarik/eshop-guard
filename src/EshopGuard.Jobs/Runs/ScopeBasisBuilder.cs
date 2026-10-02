using System.Text.Json.Nodes;
using EshopGuard.Core.Languages;
using EshopGuard.Core.Markets;

namespace EshopGuard.Jobs.Runs;

/// <summary>
/// The scope basis of an e-shop (<c>runs.estimate.basis</c>) from its free sample (price for every country, decision of
/// 2. 10. 2026): by language version its status, product count (or its lower bound), the other pages of its sitemap (pages
/// of content; unknown for a version the sample did not check), all pages of its sitemap (the basis of the price when the
/// products are not known, decision of 2. 10. 2026) and the share of products with a description in its
/// language; by ticked market the version checked for it and its products, with the reason when they are not known; the URLs
/// of the sitemaps. No band and no amount: the scope and its price are computed by change 10 (<c>ShopScopeCalculator</c>)
/// and change 12 (<c>IPriceQuoteService</c>) from this basis.
/// </summary>
public static class ScopeBasisBuilder
{
    /// <param name="sitemaps">Pages of the checked versions by their sitemaps; a version without a row has its other pages unknown (null).</param>
    public static JsonObject Build(
        MarketsAnalysisResult? analysis, Guid sampleRunId, int sitemapUrls, DateTimeOffset now, IReadOnlyList<VersionSitemap>? sitemaps = null)
    {
        var versions = new JsonArray();
        foreach (var version in analysis?.Versions ?? [])
        {
            var sitemap = sitemaps?.FirstOrDefault(s => s.BaseUrl == version.BaseUrl && s.Language == version.Language);
            versions.Add(new JsonObject
            {
                ["language"] = version.Language,
                ["base_url"] = version.BaseUrl,
                ["status"] = version.Status,
                ["product_count"] = version.ProductCount,
                ["product_count_at_least"] = version.ProductCountAtLeast,
                ["other_pages"] = sitemap?.OtherPages,
                ["sitemap_pages"] = sitemap is null ? null : sitemap.ProductPages + sitemap.OtherPages,
                ["translated_share"] = version.TranslatedShare is { } share ? Math.Round(share, 4) : null,
            });
        }

        var markets = new JsonArray();
        foreach (var market in analysis?.Plan?.ByMarket ?? [])
        {
            var row = analysis!.Versions.FirstOrDefault(v => v.BaseUrl == market.BaseUrl && (v.Language ?? "und") == market.Language);
            markets.Add(new JsonObject
            {
                ["market"] = market.Market,
                ["language"] = market.Language,
                ["base_url"] = market.BaseUrl,
                ["product_count"] = market.ProductCount,
                ["product_count_at_least"] = row?.ProductCountAtLeast,
                ["unknown_reason"] = market.ProductCount is null ? UnknownReason(row) : null,
            });
        }

        return new JsonObject
        {
            ["source_run_id"] = sampleRunId.ToString("D"),
            ["sitemap_url_count"] = sitemapUrls,
            ["versions"] = versions,
            ["markets"] = markets,
            ["computed_at"] = now,
        };
    }

    /// <summary>Why the products of a version are not known (codes taken over by change 10).</summary>
    public static string UnknownReason(ShopLanguageRow? version) =>
        version?.Codes.Contains(VersionCodes.ProductCountIncomplete) == true ? "product_count_incomplete" : "product_count_unknown";
}
