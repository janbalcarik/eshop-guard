using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Ops;

/// <summary>Politeness state of a foreign domain across all tenants (public data only). Table <c>ops.domains</c>.</summary>
public sealed class CrawlDomain : IHasTimestamps
{
    public required string Domain { get; set; }

    public string? RobotsTxt { get; set; }

    public DateTimeOffset? RobotsFetchedAt { get; set; }

    public int? CrawlDelayMs { get; set; }

    public string[] Sitemaps { get; set; } = [];

    public long? LeaseJobId { get; set; }

    public DateTimeOffset? LeaseUntil { get; set; }

    public DateTimeOffset? LastRequestAt { get; set; }

    public double? Rate { get; set; }

    public int ConsecutiveErrors { get; set; }

    public DateTimeOffset? BlockedUntil { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
