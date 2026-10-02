using EshopGuard.Data;
using EshopGuard.Data.Entities.Billing;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Billing.Pricing;

/// <summary>A price list with its tiers (ordered by the lower bound) and volume discounts.</summary>
public sealed record PriceListSnapshot(PriceList List, IReadOnlyList<PriceTier> Tiers, IReadOnlyList<VolumeDiscount> Discounts)
{
    public PriceTier? Tier(string code) => Tiers.FirstOrDefault(t => t.Code == code);
}

/// <summary>
/// Reads the global price lists (no RLS; in the API and in the worker, inside or outside a transaction of a tenant). The active
/// list of a market and currency is the published one with <c>valid_from</c> ≤ now, the newest (requirement „Ceník v databázi“).
/// </summary>
public sealed class PriceListReader(EshopGuardDb db, TimeProvider time)
{
    public async Task<PriceListSnapshot?> ActiveAsync(string marketCode, string currency, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var list = await db.PriceLists.AsNoTracking()
            .Where(l => l.MarketCode == marketCode && l.Currency == currency && l.Status == PriceListStatus.Published && l.ValidFrom <= now)
            .OrderByDescending(l => l.ValidFrom).ThenByDescending(l => l.PublishedAt)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return list is null ? null : await WithTiersAsync(list, ct).ConfigureAwait(false);
    }

    public async Task<PriceListSnapshot?> GetAsync(Guid id, CancellationToken ct)
    {
        var list = await db.PriceLists.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct).ConfigureAwait(false);
        return list is null ? null : await WithTiersAsync(list, ct).ConfigureAwait(false);
    }

    /// <summary>The price lists, newest first, of a market or of all.</summary>
    public async Task<IReadOnlyList<PriceList>> ListAsync(string? marketCode, CancellationToken ct) =>
        await db.PriceLists.AsNoTracking()
            .Where(l => marketCode == null || l.MarketCode == marketCode)
            .OrderByDescending(l => l.ValidFrom).ThenByDescending(l => l.CreatedAt)
            .Take(200)
            .ToListAsync(ct).ConfigureAwait(false);

    /// <summary>The currency of a market (<c>ref.markets.currency</c>), used while a tenant has none fixed.</summary>
    public async Task<string?> MarketCurrencyAsync(string marketCode, CancellationToken ct) =>
        await db.Markets.AsNoTracking().Where(m => m.Code == marketCode).Select(m => m.Currency).FirstOrDefaultAsync(ct).ConfigureAwait(false);

    private async Task<PriceListSnapshot> WithTiersAsync(PriceList list, CancellationToken ct)
    {
        var tiers = await db.PriceTiers.AsNoTracking().Where(t => t.PriceListId == list.Id).OrderBy(t => t.MinProducts).ToListAsync(ct).ConfigureAwait(false);
        var discounts = await db.VolumeDiscounts.AsNoTracking().Where(d => d.PriceListId == list.Id).OrderBy(d => d.FromShopNumber).ToListAsync(ct).ConfigureAwait(false);
        return new PriceListSnapshot(list, tiers, discounts);
    }
}
