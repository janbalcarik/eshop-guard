using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Iam;

/// <summary>Password reset, e-mail confirmation and sign-in link tokens (only the hash is stored). Table <c>iam.user_tokens</c>.</summary>
public sealed class UserToken : GlobalEntity
{
    public Guid? UserId { get; set; }

    public required string Email { get; set; }

    public UserTokenPurpose Purpose { get; set; }

    public required byte[] TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public byte[]? RequestedIpHash { get; set; }
}
