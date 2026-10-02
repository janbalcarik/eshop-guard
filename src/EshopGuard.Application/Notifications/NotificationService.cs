using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Findings;
using EshopGuard.Application.Problems;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Notifications;

/// <summary>
/// The notifications of the signed-in user and the settings of their e-mails (change 11, design I). Everything is the user's
/// own: another member's notification is <c>404 notification.not_found</c>, reading it changes nobody else's, and the settings
/// change only the user's own row (any role, the viewer included). The e-mails of an e-shop win over those of the account,
/// which win over <c>Notifications:Defaults</c>.
/// </summary>
public sealed class NotificationService(EshopGuardDb db, SecurityAuditWriter audit, IOptions<NotificationsOptions> options, TimeProvider time)
{
    public async Task<NotificationListDto> ListAsync(Guid userId, bool unreadOnly, Guid? shopId, string? cursor, int? limit, CancellationToken ct)
    {
        var size = Findings.Cursor.Limit(limit);
        var after = Findings.Cursor.Decode(cursor) is { Length: 1 } key && key[0].TryGetGuid(out var last) ? last : (Guid?)null;
        if (cursor is not null && after is null)
        {
            throw Findings.Cursor.Invalid();
        }

        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var mine = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
            var unread = await mine.CountAsync(n => n.ReadAt == null, ct).ConfigureAwait(false);
            var query = mine;
            if (unreadOnly)
            {
                query = query.Where(n => n.ReadAt == null);
            }

            if (shopId is { } shop)
            {
                query = query.Where(n => n.ShopId == shop);
            }

            if (after is { } id)
            {
                query = query.Where(n => n.Id.CompareTo(id) < 0);
            }

            var rows = await query.OrderByDescending(n => n.Id).Take(size + 1).ToListAsync(ct).ConfigureAwait(false);
            var page = rows.Take(size).Select(Dto).ToList();
            return new NotificationListDto(page, unread, rows.Count > size ? Findings.Cursor.Encode(page[^1].Id) : null);
        }, ct).ConfigureAwait(false);
    }

    public async Task ReadAsync(Guid userId, Guid notificationId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct).ConfigureAwait(false)
                ?? throw new DomainException(ProblemCodes.NotificationNotFound, 404);
            if (notification.ReadAt is null)
            {
                notification.ReadAt = time.GetUtcNow();
                notification.UpdatedAt = time.GetUtcNow();
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
        }, ct).ConfigureAwait(false);

    public async Task ReadAllAsync(Guid userId, Guid? shopId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var now = time.GetUtcNow();
            await db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null && (shopId == null || n.ShopId == shopId))
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now).SetProperty(n => n.UpdatedAt, now), ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    public async Task<NotificationSettingsDto> SettingsAsync(Guid userId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(() => SettingsInTransactionAsync(userId, ct), ct).ConfigureAwait(false);

    /// <summary>Writes the user's own row of the account (<paramref name="shopId"/> null) or of an e-shop of the tenant.</summary>
    public async Task<NotificationSettingsDto> UpdateAsync(Guid tenantId, Guid userId, Guid? shopId, bool? newViolation, bool? weeklySummary, bool? runFinished, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            if (shopId is { } shop && !await db.Shops.AnyAsync(s => s.Id == shop, ct).ConfigureAwait(false))
            {
                throw new DomainException(ProblemCodes.ShopNotFound, 404);
            }

            var validation = new ValidationResult();
            if (newViolation is null)
            {
                validation.Add("emailNewViolation", ProblemCodes.Fields.Required);
            }

            if (weeklySummary is null)
            {
                validation.Add("emailWeeklySummary", ProblemCodes.Fields.Required);
            }

            if (runFinished is null)
            {
                validation.Add("emailRunFinished", ProblemCodes.Fields.Required);
            }

            if (!validation.IsValid)
            {
                throw DomainException.Validation(validation);
            }

            var row = await db.NotificationSettings.FirstOrDefaultAsync(s => s.UserId == userId && s.ShopId == shopId, ct).ConfigureAwait(false);
            if (row is null)
            {
                row = new NotificationSetting { UserId = userId, ShopId = shopId };
                db.NotificationSettings.Add(row);
            }

            row.EmailNewViolation = newViolation!.Value;
            row.EmailWeeklySummary = weeklySummary!.Value;
            row.EmailRunFinished = runFinished!.Value;
            row.UpdatedAt = time.GetUtcNow();
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await audit.WriteAsync(new AuditEvent(AuditActions.NotificationSettingsChanged, tenantId, userId, "notification_settings", userId.ToString("D"), new JsonObject
            {
                ["shopId"] = shopId?.ToString("D"),
                ["emailNewViolation"] = row.EmailNewViolation,
                ["emailWeeklySummary"] = row.EmailWeeklySummary,
                ["emailRunFinished"] = row.EmailRunFinished,
            }), ct).ConfigureAwait(false);
            return await SettingsInTransactionAsync(userId, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    private async Task<NotificationSettingsDto> SettingsInTransactionAsync(Guid userId, CancellationToken ct)
    {
        var rows = await db.NotificationSettings.AsNoTracking().Where(s => s.UserId == userId).OrderBy(s => s.ShopId).ToListAsync(ct).ConfigureAwait(false);
        var defaults = options.Value.Defaults;
        var account = rows.FirstOrDefault(r => r.ShopId is null) is { } own
            ? Prefs(own)
            : new NotificationPrefsDto(defaults.EmailNewViolation, defaults.EmailWeeklySummary, defaults.EmailRunFinished);
        return new NotificationSettingsDto(account, rows.Where(r => r.ShopId is not null).Select(r => new ShopNotificationPrefsDto(r.ShopId!.Value, Prefs(r))).ToList());
    }

    private static NotificationPrefsDto Prefs(NotificationSetting row) => new(row.EmailNewViolation, row.EmailWeeklySummary, row.EmailRunFinished);

    private static NotificationDto Dto(Notification n)
    {
        NotificationRouteDto? route = null;
        if (n.Route?.RootElement is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String)
        {
            route = new NotificationRouteDto(key.GetString()!, root.TryGetProperty("params", out var p) ? p.Clone() : null);
        }

        return new NotificationDto(n.Id, n.Kind, n.ShopId, n.Params?.RootElement.Clone(), route, n.CreatedAt, n.ReadAt);
    }
}
