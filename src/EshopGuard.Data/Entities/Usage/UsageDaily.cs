using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Usage;

/// <summary>Daily totals of usage per tenant, e-shop, provider and operation. Table <c>usage.usage_daily</c>.</summary>
public sealed class UsageDaily : IHasTimestamps
{
    public DateOnly Day { get; set; }

    public Guid TenantId { get; set; }

    public Guid ShopId { get; set; }

    public UsageProvider Provider { get; set; }

    public UsageOperation Operation { get; set; }

    public long Calls { get; set; }

    public long CacheHits { get; set; }

    public long InputTokens { get; set; }

    public long CachedTokens { get; set; }

    public long OutputTokens { get; set; }

    public long BytesIn { get; set; }

    public decimal CostUsd { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
