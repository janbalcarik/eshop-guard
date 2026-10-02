using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Application.Localization;
using EshopGuard.Application.Options;
using EshopGuard.Application.Tenants;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Outbox;
using EshopGuard.Jobs.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Email;

/// <summary>
/// The job <c>email.send</c> of one row of <c>ops.outbox</c> (tenant of the job set): the recipient's language (his
/// <c>users.locale</c>, then the row's, then the tenant's), the parameters read now (codes, numbers, names and a link into the
/// application, never texts of pages), the template composed with <see cref="EmailComposer.ComposeFromOutbox"/> and sent. A
/// sent row is not sent again; a failure counts <c>attempts</c>, keeps the code in <c>error</c> and the queue retries with a
/// backoff. A template with a token never goes out of the outbox: the row stays with its error and operations are alerted.
/// </summary>
public sealed partial class EmailSendHandler(
    EmailComposer composer,
    IEmailTransport transport,
    LocaleResolver locales,
    IOptions<FrontendOptions> frontend,
    TimeProvider time,
    ILogger<EmailSendHandler> logger) : IJobHandler
{
    public string Kind => OutboxEmails.SendKind;

    public JobResourceClass ResourceClass => JobResourceClass.Io;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var outboxId = context.Job.Payload.RootElement.GetProperty("outbox_id").GetInt64();

        // The context of the job's scope (tenant set); not a constructor dependency, so the registry can list the handler
        // without a database.
        var db = context.Services.GetRequiredService<EshopGuardDb>();
        var tenantId = context.Job.TenantId ?? throw new InvalidOperationException("email.send without a tenant");

        var row = await db.ExecuteInTenantTransactionAsync(() => db.Outbox.AsNoTracking().FirstOrDefaultAsync(o => o.Id == outboxId, ct), ct).ConfigureAwait(false);
        if (row is null)
        {
            return new JobResult.Fail("email.outbox_missing");
        }

        if (row.SentAt is not null)
        {
            return JobResult.Done;
        }

        var payload = JsonNode.Parse(row.Payload.RootElement.GetRawText())!.AsObject();
        var kind = EmailTemplateKind.Find((string?)payload["template"]);
        if (kind is null || kind.ContainsToken)
        {
            var code = kind is null ? "email.template_unknown" : "email.token_template_in_outbox";
            LogRefused(logger, outboxId, tenantId, code);
            await MarkAsync(db, outboxId, code, ct).ConfigureAwait(false);
            return new JobResult.Fail(code);
        }

        EmailMessage message;
        try
        {
            var (to, locale, values) = await PrepareAsync(db, tenantId, kind, payload, ct).ConfigureAwait(false);
            if (to is null)
            {
                await MarkAsync(db, outboxId, "email.recipient_missing", ct).ConfigureAwait(false);
                return new JobResult.Fail("email.recipient_missing");
            }

            message = composer.ComposeFromOutbox(kind, locale, to, values);
        }
        catch (EmailTemplateException ex)
        {
            LogRefused(logger, outboxId, tenantId, ex.Code);
            await MarkAsync(db, outboxId, ex.Code, ct).ConfigureAwait(false);
            return new JobResult.Fail(ex.Code);
        }

        try
        {
            await transport.SendAsync(message, ct).ConfigureAwait(false);
        }
        catch (EmailSendException ex)
        {
            await MarkAsync(db, outboxId, ex.Code, ct).ConfigureAwait(false);
            return new JobResult.Retry(ex.Code);
        }

        await context.CompleteAsync(tx => tx.Db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE ops.outbox SET sent_at = {time.GetUtcNow()}, attempts = attempts + 1, error = NULL, updated_at = {time.GetUtcNow()} WHERE id = {outboxId} AND sent_at IS NULL",
            ct), ct).ConfigureAwait(false);
        LogSent(logger, outboxId, tenantId, kind.Code, message.Locale);
        return JobResult.Done;
    }

    private Task MarkAsync(EshopGuardDb db, long outboxId, string code, CancellationToken ct) => db.ExecuteInTenantTransactionAsync(async () =>
    {
        var now = time.GetUtcNow();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE ops.outbox SET attempts = attempts + 1, error = {code}, updated_at = {now} WHERE id = {outboxId} AND sent_at IS NULL", ct).ConfigureAwait(false);
    }, ct);

    /// <summary>Recipient, language and parameters of the template.</summary>
    private async Task<(string? To, string Locale, Dictionary<string, object?> Values)> PrepareAsync(
        EshopGuardDb db, Guid tenantId, EmailTemplateKind kind, JsonObject payload, CancellationToken ct) => await db.ExecuteInTenantTransactionAsync(async () =>
    {
        var parameters = payload["params"]?.AsObject() ?? [];
        var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantId, ct).ConfigureAwait(false);
        string? to = (string?)payload["to"];
        string? userLocale = null;
        if (Guid.TryParse((string?)payload["to_user_id"], out var userId))
        {
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct).ConfigureAwait(false);
            to = user?.Email;
            userLocale = user?.Locale;
        }

        var locale = userLocale is not null && await locales.IsEnabledAsync(userLocale, ct).ConfigureAwait(false)
            ? userLocale
            : (string?)payload["locale"] ?? tenant.Locale;
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        var links = frontend.Value;
        if (kind == EmailTemplateKind.InvitationAccepted)
        {
            var memberId = Guid.Parse((string)parameters["member_user_id"]!);
            var member = await db.Users.AsNoTracking().FirstAsync(u => u.Id == memberId, ct).ConfigureAwait(false);
            var role = await db.Memberships.AsNoTracking().Where(m => m.UserId == memberId).Select(m => (Data.Entities.Iam.MembershipRole?)m.Role)
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);
            values["memberEmail"] = member.Email;
            values["tenantName"] = tenant.Name;
            values["roleName"] = role is { } r ? composer.Label(locale, "role." + r.Code()) : null;
            values["link"] = links.BaseUrl + links.MembersPath.Replace("{tenantId}", tenantId.ToString("D"), StringComparison.Ordinal);
        }
        else if (EmailTemplateKind.Notifications.Contains(kind))
        {
            // A notification (change 11): codes and counts of its parameters, the name of the e-shop, the link of its target.
            if (Guid.TryParse((string?)parameters["shop_id"], out var shopId))
            {
                var shop = await db.Shops.IgnoreQueryFilters([EshopGuardDb.SoftDeleteFilter]).AsNoTracking().Where(s => s.Id == shopId)
                    .Select(s => new { s.Domain, s.Name }).FirstOrDefaultAsync(ct).ConfigureAwait(false);
                values["shopName"] = shop is null ? null : string.IsNullOrWhiteSpace(shop.Name) ? shop.Domain : shop.Name;
            }

            foreach (var name in kind.Parameters.Where(p => p is not ("email" or "link" or "shopName")))
            {
                values[name] = parameters[name] is JsonValue value ? value.GetValue<object>() : null;
            }

            foreach (var flag in kind.Flags)
            {
                values[flag] = parameters[flag] is JsonValue value && value.TryGetValue<bool>(out var on) && on;
            }

            EmailValues.Format(values, parameters, locale);

            var route = parameters["route"] as JsonObject;
            var routeParams = (route?["params"] as JsonObject ?? []).ToDictionary(p => p.Key, p => (string?)p.Value?.ToString(), StringComparer.Ordinal);
            values["link"] = links.Route(tenantId, (string?)route?["key"], routeParams);
        }
        else
        {
            var runId = Guid.Parse((string)parameters["run_id"]!);
            var run = await db.Runs.AsNoTracking().Where(r => r.Id == runId)
                .Join(db.Shops.IgnoreQueryFilters([EshopGuardDb.SoftDeleteFilter]), r => r.ShopId, s => s.Id, (r, s) => new { r.Stats, s.Domain, s.Name })
                .FirstAsync(ct).ConfigureAwait(false);
            var stats = run.Stats is null ? new JsonObject() : JsonNode.Parse(run.Stats.RootElement.GetRawText())!.AsObject();
            values["shopName"] = string.IsNullOrWhiteSpace(run.Name) ? run.Domain : run.Name;
            values["pages"] = (long?)stats["pages_checked"] ?? 0;
            values["findings"] = Sum(stats["findings_by_severity"]);
            values["unchecked"] = Sum(stats["unchecked"]);
            values["link"] = links.BaseUrl + links.RunPath.Replace("{tenantId}", tenantId.ToString("D"), StringComparison.Ordinal)
                .Replace("{runId}", runId.ToString("D"), StringComparison.Ordinal);
        }

        return (to, locale, values);
    }, ct).ConfigureAwait(false);

    private static long Sum(JsonNode? counts) =>
        counts is JsonObject o ? o.Sum(p => p.Value is JsonValue v && v.TryGetValue<long>(out var n) ? n : 0) : 0;

    [LoggerMessage(Level = LogLevel.Information, Message = "email.sent {OutboxId} {TenantId} {Kind} {Locale}")]
    private static partial void LogSent(ILogger logger, long outboxId, Guid tenantId, string kind, string locale);

    [LoggerMessage(Level = LogLevel.Critical, Message = "email.refused {OutboxId} {TenantId} {Code}")]
    private static partial void LogRefused(ILogger logger, long outboxId, Guid tenantId, string code);
}
