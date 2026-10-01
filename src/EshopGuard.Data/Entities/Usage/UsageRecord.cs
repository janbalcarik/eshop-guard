using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Usage;

/// <summary>Internal usage and cost of a batch (monthly partitions; customers never see it). Table <c>usage.usage_records</c>.</summary>
public sealed class UsageRecord : IHasCreatedAt
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid? ShopId { get; set; }

    public Guid? RunId { get; set; }

    public long? JobId { get; set; }

    public UsageProvider Provider { get; set; }

    public string? Model { get; set; }

    public UsageOperation Operation { get; set; }

    public int Calls { get; set; }

    public int CacheHits { get; set; }

    public long InputTokens { get; set; }

    public long CachedTokens { get; set; }

    public long OutputTokens { get; set; }

    public long BytesIn { get; set; }

    public decimal CostUsd { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }
}
