using EshopGuard.Application.Problems;
using EshopGuard.Application.Shops;
using EshopGuard.Application.Shops.Scope;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Billing.Subscriptions;

/// <summary>
/// The counted products of an e-shop for its tier (design „Pásma“: the products of the version checked for each ticked market,
/// summed). <paramref name="LowerBound"/>: counted by a partial analysis, so the e-shop has at least this many.
/// </summary>
public sealed record CountedProducts(int Count, bool LowerBound = false);

/// <summary>Reads the counted products of an e-shop of the tenant set in the context; null when not known (task 8.3).</summary>
public interface ICountedProductsReader
{
    Task<CountedProducts?> ReadAsync(Guid shopId, CancellationToken ct);
}

/// <summary>
/// The counted products from the last full analysis (<c>shop.shops.product_count</c> of <c>last_full_run_id</c>) times the ticked
/// markets each checked version is checked for; when the versions serve a different number of markets (the products of each
/// version are not stored), or before the first full analysis, the basis of the price of the scope (<see cref="ShopScope.PriceCount"/>).
/// </summary>
public sealed class CountedProductsReader(EshopGuardDb db, ShopCatalog catalog, ScopeInputsLoader loader) : ICountedProductsReader
{
    public async Task<CountedProducts?> ReadAsync(Guid shopId, CancellationToken ct)
    {
        var markets = await catalog.MarketsAsync(ct).ConfigureAwait(false);
        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            StoredScope stored;
            try
            {
                stored = await loader.LoadAsync(shopId, markets, ct).ConfigureAwait(false);
            }
            catch (DomainException e) when (e.Code == ProblemCodes.ShopNotFound)
            {
                return null;
            }

            var shop = stored.Shop;
            var status = shop.LastFullRunId is { } runId
                ? await db.Runs.AsNoTracking().Where(r => r.Id == runId).Select(r => (RunStatus?)r.Status).FirstOrDefaultAsync(ct).ConfigureAwait(false)
                : null;
            return From(ShopScopeCalculator.Calculate(stored.Input(), markets), status is null ? null : shop.ProductCount, status == RunStatus.Partial);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The counted products of a scope: <paramref name="analyzedProducts"/> (of the last full analysis, null without one) times the
    /// markets of every checked version when they all serve the same number, else the basis of the price of the scope.
    /// </summary>
    public static CountedProducts? From(ShopScope scope, int? analyzedProducts, bool partial)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var perVersion = scope.CheckedVersions.Select(v => v.Markets.Count).Distinct().ToList();
        if (analyzedProducts is { } products && perVersion is [> 0 and var markets])
        {
            return new CountedProducts(products * markets, partial);
        }

        return scope.PriceCount is { } count ? new CountedProducts(count) : null;
    }
}
