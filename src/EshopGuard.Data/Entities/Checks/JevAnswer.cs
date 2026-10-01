using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Checks;

/// <summary>Cached Jev answer of a tenant (key = legacy cache key "sha256:…"; HASH partitions by tenant). Table <c>checks.jev_answers</c>.</summary>
public sealed class JevAnswer : ITenantOwned, IHasCreatedAt
{
    public Guid TenantId { get; set; }

    public required string CacheKey { get; set; }

    public required JsonDocument Response { get; set; }

    public required string Model { get; set; }

    public byte[]? QuestionSetHash { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }
}
