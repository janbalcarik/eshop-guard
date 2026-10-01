using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Iam;

/// <summary>Invitation of a person into a tenant (only the token hash is stored). Table <c>iam.invitations</c>.</summary>
public sealed class Invitation : TenantEntity
{
    public required string Email { get; set; }

    public MembershipRole Role { get; set; }

    public required byte[] TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? AcceptedAt { get; set; }

    public Guid InvitedBy { get; set; }
}
