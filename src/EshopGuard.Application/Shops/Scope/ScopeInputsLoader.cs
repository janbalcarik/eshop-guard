using System.Text.Json.Nodes;
using EshopGuard.Application.Problems;
using EshopGuard.Core.Markets;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Shops.Scope;

/// <summary>The stored state of an e-shop for its scope: the markets, the versions, the sample of the basis.</summary>
/// <param name="MarketsConfirmed">The client confirmed the markets (<c>PUT …/markets</c>); otherwise the preselected suggestions are used.</param>
public sealed record StoredScope(
    Shop Shop,
    IReadOnlyList<ShopMarket> Markets,
    IReadOnlyList<ShopLanguage> Languages,
    IReadOnlyList<string> ActiveMarkets,
    bool MarketsConfirmed,
    Run? Sample,
    JsonObject? Basis)
{
    /// <summary>
    /// The input of <see cref="ShopScopeCalculator"/> for other markets or exclusions (null keeps the stored ones). Given
    /// exclusions are the whole set: a version the client excluded before is available again unless it is in the set.
    /// </summary>
    public ShopScopeInput Input(IReadOnlyList<string>? activeMarkets = null, IReadOnlyCollection<string>? excluded = null) => new(
        activeMarkets ?? ActiveMarkets,
        Languages.Select(l => new ScopeVersionInput(
            l.Language, l.BaseUrl,
            excluded is not null && l.Status == ShopLanguageStatus.Excluded && l.DecidedAt is not null ? ShopLanguageStatus.Active : l.Status,
            l.DecidedAt is not null, l.Source == LanguageSource.Main, l.ProductCount, OtherPages(l.Language))).ToList(),
        excluded ?? [],
        Sample?.Status is RunStatus.Finished or RunStatus.Partial ? Sample.Id : null);

    /// <summary>Other pages of the version from the basis of the sample (<c>runs.estimate.basis.versions[].other_pages</c>).</summary>
    public int? OtherPages(string language) =>
        (Basis?["versions"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(v => (string?)v["language"] == language)
            .Select(v => v["other_pages"] is JsonValue value && value.TryGetValue<int>(out var n) ? n : (int?)null)
            .FirstOrDefault();
}

/// <summary>
/// Reads the stored state of an e-shop for its scope (change 10, task 7.2) in the open transaction of the tenant: the rows
/// of <c>shop_markets</c> and <c>shop_languages</c> and the newest free sample with its basis (<c>runs.estimate.basis</c>,
/// change 8).
/// </summary>
public sealed class ScopeInputsLoader(EshopGuardDb db, ShopReader reader)
{
    public async Task<StoredScope> LoadAsync(Guid shopId, MarketCatalog catalog, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var shop = await reader.ReadAsync(shopId, ct).ConfigureAwait(false);
        var markets = await db.ShopMarkets.AsNoTracking().Where(m => m.ShopId == shopId).OrderByDescending(m => m.IsHome).ThenBy(m => m.CountryCode)
            .ToListAsync(ct).ConfigureAwait(false);
        var languages = await db.ShopLanguages.AsNoTracking().Where(l => l.ShopId == shopId)
            .OrderByDescending(l => l.Source == LanguageSource.Main).ThenBy(l => l.Language).ToListAsync(ct).ConfigureAwait(false);
        var sample = await db.Runs.AsNoTracking().Where(r => r.ShopId == shopId && r.Kind == RunKind.FreeSample)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        var basis = sample?.Estimate is { } estimate && JsonNode.Parse(estimate.RootElement.GetRawText()) is JsonObject root ? root["basis"] as JsonObject : null;

        var confirmed = markets.Any(m => m.ConfirmedAt is not null);
        var active = markets
            .Where(m => confirmed ? m.Status == ShopMarketStatus.Active : m.Status == ShopMarketStatus.Suggested && MarketEvidenceMapper.Preselected(m))
            .Select(m => catalog.ByCountry(m.CountryCode)?.Code).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        return new StoredScope(shop, markets, languages, active, confirmed, sample, basis);
    }

    /// <summary>The basis must come from a finished sample: <c>409 quote.sample_not_finished</c> while it runs, <c>basis_missing</c> without one.</summary>
    public static void RequireBasis(StoredScope stored, string missingCode)
    {
        ArgumentNullException.ThrowIfNull(stored);
        switch (stored.Sample?.Status)
        {
            case null or RunStatus.Failed or RunStatus.Canceled:
                throw new DomainException(missingCode, 409);
            case RunStatus.Finished or RunStatus.Partial:
                if (stored.Languages.Count == 0)
                {
                    throw new DomainException(missingCode, 409);
                }

                return;
            default:
                throw new DomainException(ProblemCodes.QuoteSampleNotFinished, 409, new Dictionary<string, object?> { ["status"] = ShopReader.Text(stored.Sample.Status) });
        }
    }
}
