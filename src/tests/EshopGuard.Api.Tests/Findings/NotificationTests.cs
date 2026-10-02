using System.Net;
using System.Text.Json.Nodes;
using EshopGuard.Data;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Notifications;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>
/// Notifications and the settings of their e-mails (change 11, task 10.5; spec „Upozornění a jejich nastavení“): a row for
/// every member, reading is per user, the setting of the e-shop wins over the account, the defaults without a row, only
/// one's own settings, and no text of a page in a notification.
/// </summary>
public sealed class NotificationTests : FindingsTestBase
{
    [Fact]
    public async Task ReadingByJana_DoesNotHideItFromPeter()
    {
        await using var factory = Factory();
        using var jana = await People.OwnerAsync(factory);
        using var peter = await People.MemberAsync(factory, jana, "editor");
        var shopId = (await CreateShopAsync(jana)).GetProperty("id").GetGuid();
        await NotifyAsync(factory, jana.TenantId, shopId, NotificationKinds.ProtocolReady, new JsonObject { ["number"] = "EG-2026-0142" });
        var path = $"/api/t/{jana.TenantId}/notifications";
        var mine = (await ApiClient.JsonAsync(await jana.Browser.GetAsync(path))).GetProperty("items")[0].GetProperty("id").GetGuid();

        using var read = await jana.Browser.PostAsync($"{path}/{mine}/read");
        using var foreign = await peter.Browser.PostAsync($"{path}/{mine}/read");

        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        await ProblemAsync(foreign, HttpStatusCode.NotFound, "notification.not_found");
        var janaList = await ApiClient.JsonAsync(await jana.Browser.GetAsync(path));
        var peterList = await ApiClient.JsonAsync(await peter.Browser.GetAsync(path));
        Assert.Equal(0, janaList.GetProperty("unreadCount").GetInt32());
        Assert.Equal(1, peterList.GetProperty("unreadCount").GetInt32());
        var item = peterList.GetProperty("items")[0];
        Assert.Equal("protocol_ready", item.GetProperty("kind").GetString());
        Assert.Equal("protocols.item", item.GetProperty("route").GetProperty("key").GetString());
        Assert.Equal(JsonValueKindNull, item.GetProperty("readAt").ValueKind);
    }

    [Fact]
    public async Task EmailOffForTheShop_WinsOverTheAccount()
    {
        await using var factory = Factory();
        using var jana = await People.OwnerAsync(factory);
        using var peter = await People.MemberAsync(factory, jana, "editor");
        var shopId = (await CreateShopAsync(jana)).GetProperty("id").GetGuid();
        var settings = $"/api/t/{jana.TenantId}/notification-settings";
        using (var account = await peter.Browser.PutAsync(settings, new { emailNewViolation = true, emailWeeklySummary = true, emailRunFinished = true }))
        {
            Assert.Equal(HttpStatusCode.OK, account.StatusCode);
        }

        using var shop = await peter.Browser.PutAsync(settings, new { shopId, emailNewViolation = true, emailWeeklySummary = true, emailRunFinished = false });
        await NotifyAsync(factory, jana.TenantId, shopId, NotificationKinds.RunFinished, new JsonObject { ["run_id"] = Guid.NewGuid().ToString("D") });

        Assert.Equal(HttpStatusCode.OK, shop.StatusCode);
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM iam.notifications WHERE tenant_id = $1 AND user_id = $2 AND kind = 'run_finished'", jana.TenantId, peter.UserId));
        Assert.Equal(0L, await OutboxAsync(jana.TenantId, peter.UserId, "run_finished"));
        // Jana has no row: the defaults send it.
        Assert.Equal(1L, await OutboxAsync(jana.TenantId, jana.UserId, "run_finished"));
    }

    [Fact]
    public async Task WithoutARow_TheDefaultsApply_AndEvidenceGoesToEditorsOnly()
    {
        await using var factory = Factory();
        using var jana = await People.OwnerAsync(factory);
        using var viewer = await People.MemberAsync(factory, jana, "viewer");

        var defaults = await ApiClient.JsonAsync(await viewer.Browser.GetAsync($"/api/t/{jana.TenantId}/notification-settings"));
        await NotifyAsync(factory, jana.TenantId, null, NotificationKinds.EvidenceExpiring, new JsonObject { ["days"] = 20 });

        Assert.True(defaults.GetProperty("account").GetProperty("emailRunFinished").GetBoolean());
        Assert.Empty(defaults.GetProperty("shops").EnumerateArray());
        Assert.Equal(2L, await AdminScalarAsync<long>("SELECT count(*) FROM iam.notifications WHERE tenant_id = $1 AND kind = 'evidence_expiring'", jana.TenantId));
        Assert.Equal(1L, await OutboxAsync(jana.TenantId, jana.UserId, "evidence_expiring"));
        Assert.Equal(0L, await OutboxAsync(jana.TenantId, viewer.UserId, "evidence_expiring"));
    }

    [Fact]
    public async Task Viewer_ChangesOnlyTheirOwnSettings()
    {
        await using var factory = Factory();
        using var jana = await People.OwnerAsync(factory);
        using var viewer = await People.MemberAsync(factory, jana, "viewer");
        var settings = $"/api/t/{jana.TenantId}/notification-settings";

        using var changed = await viewer.Browser.PutAsync(settings, new { emailNewViolation = false, emailWeeklySummary = false, emailRunFinished = false });
        using var unknownShop = await viewer.Browser.PutAsync(settings, new { shopId = Guid.NewGuid(), emailNewViolation = true, emailWeeklySummary = true, emailRunFinished = true });
        using var missing = await viewer.Browser.PutAsync(settings, new { emailNewViolation = true });

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.False((await ApiClient.JsonAsync(changed)).GetProperty("account").GetProperty("emailNewViolation").GetBoolean());
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM iam.notification_settings WHERE tenant_id = $1", jana.TenantId));
        Assert.Equal(viewer.UserId, await AdminScalarAsync<Guid>("SELECT user_id FROM iam.notification_settings WHERE tenant_id = $1", jana.TenantId));
        Assert.True((await ApiClient.JsonAsync(await jana.Browser.GetAsync(settings))).GetProperty("account").GetProperty("emailNewViolation").GetBoolean());
        await ProblemAsync(unknownShop, HttpStatusCode.NotFound, "shop.not_found");
        await ProblemAsync(missing, HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task NotificationWithATextOfAPage_IsRefused()
    {
        await using var factory = Factory();
        using var jana = await People.OwnerAsync(factory);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            NotifyAsync(factory, jana.TenantId, null, NotificationKinds.NewViolation, new JsonObject { ["text"] = "Všetky naše produkty balíme ekologicky." }));
        await NotifyAsync(factory, jana.TenantId, null, NotificationKinds.NewViolation, new JsonObject { ["findings"] = 3 });

        var payload = (string)(await AdminRowsAsync("SELECT payload::text FROM ops.outbox WHERE tenant_id = $1", jana.TenantId)).Single()[0]!;
        Assert.Contains("\"template\": \"new_violation\"", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("ekologick", payload, StringComparison.Ordinal);
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM iam.notifications WHERE tenant_id = $1 AND params::text LIKE '%ekologick%'", jana.TenantId));
    }

    private const System.Text.Json.JsonValueKind JsonValueKindNull = System.Text.Json.JsonValueKind.Null;

    private static async Task NotifyAsync(ApiFactory factory, Guid tenantId, Guid? shopId, NotificationKind kind, JsonObject parameters)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<EshopGuardDb>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDispatcher>();
        await db.ExecuteInTenantTransactionAsync(() => dispatcher.NotifyAsync((Npgsql.NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction(), new NotificationRequest(tenantId, shopId, kind, parameters,
            NotificationRoutes.Protocol, new JsonObject { ["protocolId"] = Guid.NewGuid().ToString("D") }), TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
    }

    private static Task<long> OutboxAsync(Guid tenantId, Guid userId, string template) => AdminScalarAsync<long>(
        "SELECT count(*) FROM ops.outbox WHERE tenant_id = $1 AND payload->>'to_user_id' = $2 AND payload->>'template' = $3", tenantId, userId.ToString("D"), template);
}
