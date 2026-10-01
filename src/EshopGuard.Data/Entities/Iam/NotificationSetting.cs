using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Iam;

/// <summary>E-mail notifications of a user for one e-shop or all (shop_id null). Table <c>iam.notification_settings</c>.</summary>
public sealed class NotificationSetting : TenantEntity
{
    public Guid UserId { get; set; }

    public Guid? ShopId { get; set; }

    public bool EmailNewViolation { get; set; }

    public bool EmailWeeklySummary { get; set; }

    public bool EmailRunFinished { get; set; }
}
