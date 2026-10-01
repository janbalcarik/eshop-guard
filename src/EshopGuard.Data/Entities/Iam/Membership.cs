using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Iam;

/// <summary>Membership of a user in a tenant with a role. Table <c>iam.memberships</c>.</summary>
public sealed class Membership : ITenantOwned, IHasTimestamps
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public MembershipRole Role { get; set; }

    public Guid? InvitedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
