namespace EshopGuard.Core.Markets;

/// <summary>
/// Pages of a shop known from its connector (terms, delivery, contact), the fallback of the pick of pages about sales
/// (change 7). Implemented by the connectors of change 15; without one nothing is added.
/// </summary>
public interface IShopPagesSource
{
    /// <summary>Addresses of the pages about sales and delivery of the shop, or nothing.</summary>
    Task<IReadOnlyList<Uri>> GetSalesPagesAsync(Uri site, CancellationToken ct);
}
