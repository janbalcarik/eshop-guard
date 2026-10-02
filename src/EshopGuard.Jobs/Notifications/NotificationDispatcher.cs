using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EshopGuard.Jobs.Outbox;
using EshopGuard.Jobs.Queue;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Jobs.Notifications;

/// <summary>A notification to send: tenant, e-shop, kind, parameters (codes, counts and ids only) and the target in the application.</summary>
public sealed record NotificationRequest(Guid TenantId, Guid? ShopId, NotificationKind Kind, JsonObject Params, string RouteKey, JsonObject RouteParams);

/// <summary>
/// Notifications (change 11, AD 12) for the runs of change 8, the evidence, protocols and publications of change 11 and the
/// monitoring of change 16. In the caller's transaction of the tenant: one row of <c>iam.notifications</c> for every member
/// (<c>user_id</c> always filled, K rozhodnutí 8), and an e-mail to <c>ops.outbox</c> (template = kind) for the members who get
/// it: by the setting of the e-shop, else of the account (<c>shop_id = NULL</c>), else <c>Notifications:Defaults</c>; the kinds
/// of evidence, protocols and publications always to the roles editor and above. Parameters are refused unless they are codes,
/// numbers or ids: no text of a page ever goes into a notification or an e-mail. Here (and not in <c>EshopGuard.Application</c>)
/// because the jobs of the worker send them too.
/// </summary>
public sealed partial class NotificationDispatcher(IJobQueue queue, IOptions<NotificationsOptions> options)
{
    private static readonly string[] Editors = ["owner", "admin", "editor"];

    /// <summary>Sends the notification; returns the number of members it went to.</summary>
    public async Task<int> NotifyAsync(NpgsqlTransaction transaction, NotificationRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(request);
        EnsureNoText(request.Params, "params");
        EnsureNoText(request.RouteParams, "route");
        var connection = transaction.Connection!;
        var members = new List<(Guid UserId, string Role)>();
        await using (var select = new NpgsqlCommand("SELECT user_id, role FROM iam.memberships WHERE tenant_id = $1 ORDER BY user_id", connection, transaction)
        {
            Parameters = { new NpgsqlParameter { Value = request.TenantId } },
        })
        await using (var reader = await select.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                members.Add((reader.GetGuid(0), reader.GetString(1)));
            }
        }

        var route = new JsonObject { ["key"] = request.RouteKey, ["params"] = request.RouteParams.DeepClone() };
        foreach (var (userId, _) in members)
        {
            await using var insert = new NpgsqlCommand(
                "INSERT INTO iam.notifications (id, tenant_id, user_id, shop_id, kind, params, route, created_at, updated_at) VALUES ($1, $2, $3, $4, $5, $6, $7, now(), now())",
                connection, transaction)
            {
                Parameters =
                {
                    new NpgsqlParameter { Value = Guid.CreateVersion7() },
                    new NpgsqlParameter { Value = request.TenantId },
                    new NpgsqlParameter { Value = userId },
                    new NpgsqlParameter { Value = (object?)request.ShopId ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Uuid },
                    new NpgsqlParameter { Value = request.Kind.Code },
                    new NpgsqlParameter { Value = request.Params.ToJsonString(), NpgsqlDbType = NpgsqlDbType.Jsonb },
                    new NpgsqlParameter { Value = route.ToJsonString(), NpgsqlDbType = NpgsqlDbType.Jsonb },
                },
            };
            await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        foreach (var userId in await EmailRecipientsAsync(transaction, request, members, ct).ConfigureAwait(false))
        {
            var email = request.Params.DeepClone().AsObject();
            email["shop_id"] = request.ShopId?.ToString("D");
            email["route"] = route.DeepClone();
            await OutboxEmails.AddAsync(transaction, queue, request.TenantId, new OutboxEmail(request.Kind.Code, userId, null, null, email), ct).ConfigureAwait(false);
        }

        return members.Count;
    }

    /// <summary>The members who get the e-mail of the kind.</summary>
    private async Task<List<Guid>> EmailRecipientsAsync(NpgsqlTransaction transaction, NotificationRequest request, List<(Guid UserId, string Role)> members, CancellationToken ct)
    {
        var email = request.Kind.Email;
        if (email == NotificationEmail.None)
        {
            return [];
        }

        if (email == NotificationEmail.EditorsAlways)
        {
            return members.Where(m => Editors.Contains(m.Role)).Select(m => m.UserId).ToList();
        }

        var column = email switch
        {
            NotificationEmail.NewViolation => "email_new_violation",
            NotificationEmail.WeeklySummary => "email_weekly_summary",
            _ => "email_run_finished",
        };
        var defaults = options.Value.Defaults;
        var fallback = email switch
        {
            NotificationEmail.NewViolation => defaults.EmailNewViolation,
            NotificationEmail.WeeklySummary => defaults.EmailWeeklySummary,
            _ => defaults.EmailRunFinished,
        };
        var account = new Dictionary<Guid, bool>();
        var shop = new Dictionary<Guid, bool>();
        await using (var select = new NpgsqlCommand(
            $"SELECT user_id, shop_id, {column} FROM iam.notification_settings WHERE tenant_id = $1 AND (shop_id IS NULL OR shop_id = $2)",
            transaction.Connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = request.TenantId },
                new NpgsqlParameter { Value = (object?)request.ShopId ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Uuid },
            },
        })
        await using (var reader = await select.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                (reader.IsDBNull(1) ? account : shop)[reader.GetGuid(0)] = reader.GetBoolean(2);
            }
        }

        // The setting of the e-shop wins over the one of the account, which wins over the defaults.
        return members.Where(m => shop.TryGetValue(m.UserId, out var s) ? s : account.TryGetValue(m.UserId, out var a) ? a : fallback)
            .Select(m => m.UserId).ToList();
    }

    /// <summary>Refuses anything but codes, numbers, booleans and ids (a text of a page never goes into a notification).</summary>
    public static void EnsureNoText(JsonNode? node, string path)
    {
        switch (node)
        {
            case null:
                return;
            case JsonObject o:
                foreach (var (key, value) in o)
                {
                    EnsureNoText(value, $"{path}.{key}");
                }

                return;
            case JsonArray a:
                for (var i = 0; i < a.Count; i++)
                {
                    EnsureNoText(a[i], $"{path}[{i}]");
                }

                return;
            case JsonValue v when v.TryGetValue<string>(out var text):
                if (!Code().IsMatch(text))
                {
                    throw new ArgumentException($"A notification carries codes, numbers and ids only ({path}).", nameof(node));
                }

                return;
            default:
                return;
        }
    }

    [GeneratedRegex(@"^[A-Za-z0-9_.:+\-]{0,100}$")]
    private static partial Regex Code();
}
