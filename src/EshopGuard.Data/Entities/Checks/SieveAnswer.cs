using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Checks;

/// <summary>Cached sieve probabilities of a text chunk (HASH partitions by tenant). Table <c>checks.sieve_answers</c>.</summary>
public sealed class SieveAnswer : ITenantOwned, IHasCreatedAt
{
    public Guid TenantId { get; set; }

    public required byte[] QuestionSetHash { get; set; }

    public required byte[] ChunkHash { get; set; }

    public float[] Probabilities { get; set; } = [];

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }
}
