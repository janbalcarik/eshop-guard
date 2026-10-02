using System.Text.Json.Nodes;
using EshopGuard.Core.Languages;
using EshopGuard.Core.Markets;

namespace EshopGuard.Jobs.Runs;

/// <summary>
/// The scope basis of an e-shop (<c>runs.estimate.basis</c>) from its free sample: by language version the product count, the
/// other pages of its sitemap (pages of content; unknown for a version the sample did not check), whether the version counts
/// and why not (<c>menu_only_translation</c>, <c>sample_insufficient</c>, <c>unsupported_market</c>…),
/// the share of its own texts; the URLs of the sitemaps. No band and no amount: the scope and its price are computed by
/// change 10 (<c>ShopScopeCalculator</c>) and change 12 (<c>IPriceQuoteService</c>) from this basis.
/// </summary>
public static class ScopeBasisBuilder
{
    /// <param name="sitemaps">Pages of the checked versions by their sitemaps; a version without a row has its other pages unknown (null).</param>
    public static JsonObject Build(
        MarketsAnalysisResult? analysis, Guid sampleRunId, int sitemapUrls, double countedMinOwnShare, DateTimeOffset now, IReadOnlyList<VersionSitemap>? sitemaps = null)
    {
        var versions = new JsonArray();
        foreach (var version in analysis?.Versions ?? [])
        {
            var sitemap = sitemaps?.FirstOrDefault(s => s.BaseUrl == version.BaseUrl && s.Language == version.Language);
            versions.Add(new JsonObject
            {
                ["language"] = version.Language,
                ["base_url"] = version.BaseUrl,
                ["product_count"] = version.ProductCount,
                ["product_count_at_least"] = version.ProductCountAtLeast,
                ["other_pages"] = sitemap?.OtherPages,
                ["counted"] = version.Counted,
                ["not_counted_reason"] = version.Counted ? null : NotCountedReason(version, countedMinOwnShare),
                ["own_text_share"] = version.OwnTextShare is { } share ? Math.Round(share, 4) : null,
            });
        }

        return new JsonObject
        {
            ["source_run_id"] = sampleRunId.ToString("D"),
            ["sitemap_url_count"] = sitemapUrls,
            ["versions"] = versions,
            ["computed_at"] = now,
        };
    }

    /// <summary>Why a version does not count for the scope (codes taken over by change 12).</summary>
    public static string NotCountedReason(ShopLanguageRow version, double countedMinOwnShare)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (version.Status == VersionStatus.Unsupported)
        {
            return "unsupported_market";
        }

        if (version.Status is VersionStatus.Excluded)
        {
            return "excluded";
        }

        if (version.Codes.Contains(VersionCodes.SampleInsufficient))
        {
            return "sample_insufficient";
        }

        if (version.Codes.Contains(VersionCodes.ProductCountIncomplete))
        {
            return "product_count_incomplete";
        }

        if (version.Codes.Contains(VersionCodes.ProductCountUnknown))
        {
            return "product_count_unknown";
        }

        if (version.Codes.Contains(VersionCodes.LanguageUnknown))
        {
            return "language_unknown";
        }

        if (version.OwnTextShare is { } share && share < countedMinOwnShare)
        {
            return "menu_only_translation";
        }

        return version.Codes.FirstOrDefault() ?? "not_checked";
    }
}
