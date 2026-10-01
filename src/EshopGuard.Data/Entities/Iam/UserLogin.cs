using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Iam;

/// <summary>External login of a user ("Pokračovať cez Google"). Table <c>iam.user_logins</c>.</summary>
public sealed class UserLogin : IHasCreatedAt
{
    /// <summary>Identifier (<c>uuid</c> v7).</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public required string Provider { get; set; }

    public required string ProviderKey { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }
}
