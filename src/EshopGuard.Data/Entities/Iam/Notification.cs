using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Iam;

/// <summary>Notification for a user (user_id null = everyone in the tenant); text is composed by kind. Table <c>iam.notifications</c>.</summary>
public sealed class Notification : TenantEntity
{
    public Guid? UserId { get; set; }

    public Guid? ShopId { get; set; }

    public required string Kind { get; set; }

    public JsonDocument? Params { get; set; }

    public string? Link { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
}
