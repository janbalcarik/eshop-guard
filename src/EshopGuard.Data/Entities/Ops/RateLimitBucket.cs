using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Ops;

/// <summary>Token bucket of a rate limit (jev, openai, connector:…). Table <c>ops.rate_limit_buckets</c>.</summary>
public sealed class RateLimitBucket : IHasTimestamps
{
    public required string Key { get; set; }

    public double Capacity { get; set; }

    public double Tokens { get; set; }

    public double RefillPerSec { get; set; }

    public JsonDocument? Reserved { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
