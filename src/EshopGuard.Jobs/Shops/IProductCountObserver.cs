using Npgsql;

namespace EshopGuard.Jobs.Shops;

/// <summary>
/// Learns that the counted products of an e-shop changed (a full analysis wrote <c>shop.shops.product_count</c>, later a
/// connector), in the transaction of that write: billing plans a change of the tier from it (change 12, task 8.3). An observer
/// only enqueues work; <paramref name="sourceId"/> (the run) makes it once per source.
/// </summary>
public interface IProductCountObserver
{
    Task ChangedAsync(NpgsqlTransaction transaction, Guid tenantId, Guid shopId, Guid sourceId, CancellationToken ct);
}
