using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Fixes;

/// <summary>Cached rewrite answer of a tenant. Table <c>fixes.rewrite_cache</c>.</summary>
public sealed class RewriteCacheEntry : ITenantOwned, IHasCreatedAt
{
    public Guid TenantId { get; set; }

    public required string Key { get; set; }

    public required JsonDocument Answer { get; set; }

    public required string Model { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }
}
