using EshopGuard.Api.Contracts;
using EshopGuard.Api.Problems;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Notifications;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Tenants;

namespace EshopGuard.Api.Endpoints;

/// <summary>The user's own notifications and the settings of their e-mails (change 11, design I); every member, the viewer too.</summary>
public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder tenant)
    {
        tenant.MapGet("/notifications", async (bool? unreadOnly, Guid? shopId, string? cursor, int? limit, HttpContext context, NotificationService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListAsync(context.User.RequireUserId(), unreadOnly ?? false, shopId, cursor, limit, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ValidationFailed);

        tenant.MapPost("/notifications/{notificationId:guid}/read", async (Guid notificationId, HttpContext context, NotificationService service, CancellationToken ct) =>
            {
                await service.ReadAsync(context.User.RequireUserId(), notificationId, ct);
                return TypedResults.NoContent();
            })
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.NotificationNotFound);

        tenant.MapPost("/notifications/read-all", async (ReadAllNotificationsRequest? body, HttpContext context, NotificationService service, CancellationToken ct) =>
            {
                await service.ReadAllAsync(context.User.RequireUserId(), body?.ShopId, ct);
                return TypedResults.NoContent();
            })
            .RequireTenantRole(TenantRole.Viewer);

        tenant.MapGet("/notification-settings", async (HttpContext context, NotificationService service, CancellationToken ct) =>
                TypedResults.Ok(await service.SettingsAsync(context.User.RequireUserId(), ct)))
            .RequireTenantRole(TenantRole.Viewer);

        tenant.MapPut("/notification-settings", async (Guid tenantId, NotificationSettingsRequest? body, HttpContext context, NotificationService service, CancellationToken ct) =>
                TypedResults.Ok(await service.UpdateAsync(tenantId, context.User.RequireUserId(), body?.ShopId, body?.EmailNewViolation, body?.EmailWeeklySummary,
                    body?.EmailRunFinished, ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ValidationFailed);
        return tenant;
    }
}
