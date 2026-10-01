using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Checks;

/// <summary>
/// Cached sieve answer of a tenant: the whole Jev answer for one chunk of a main text, keyed like
/// <see cref="JevAnswer"/> by the legacy cache key "sha256:…" (HASH partitions by tenant). Table <c>checks.sieve_answers</c>.
/// </summary>
public sealed class SieveAnswer : ITenantOwned, IHasCreatedAt
{
    public Guid TenantId { get; set; }

    public required string CacheKey { get; set; }

    public required JsonDocument Response { get; set; }

    public required string Model { get; set; }

    public byte[]? QuestionSetHash { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }
}
