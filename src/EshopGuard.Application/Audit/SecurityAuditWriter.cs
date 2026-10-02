using System.Text.Json.Nodes;
using EshopGuard.Application.Security;
using EshopGuard.Data;
using EshopGuard.Data.Tenancy;

namespace EshopGuard.Application.Audit;

/// <summary>An event of the security audit; <see cref="Data"/> holds codes, ids and hashes only.</summary>
public sealed record AuditEvent(string Action, Guid? TenantId, Guid? ActorUserId, string? EntityType = null, string? EntityId = null, JsonObject? Data = null);

/// <summary>
/// Writes <c>ops.audit_log</c> (AD 13) in the open transaction, or in a transaction of its own (user scope) when there is none.
/// The client address goes in as <c>data.ipHash</c> (HMAC, the column <c>ip</c> stays empty). Keys that could carry a secret
/// or a readable address (<c>email</c>, <c>token</c>, <c>tokenHash</c>, <c>password</c>) are refused: a mistake fails, it does
/// not leak.
/// </summary>
public sealed class SecurityAuditWriter(EshopGuardDb db, RequestContext request, IpHasher hasher, TimeProvider time)
{
    private static readonly string[] Forbidden = ["email", "token", "tokenhash", "password", "newpassword", "currentpassword", "ip"];

    private const string Insert = """
        INSERT INTO ops.audit_log (at, tenant_id, actor_user_id, actor_kind, action, entity_type, entity_id, data, created_at)
        VALUES (@at, @tenant, @actor, @kind, @action, @entity_type, @entity_id, @data, @at)
        """;

    public async Task WriteAsync(AuditEvent entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var data = entry.Data?.DeepClone().AsObject() ?? [];
        if (data.Select(p => p.Key.ToLowerInvariant()).FirstOrDefault(k => Forbidden.Contains(k)) is { } key)
        {
            throw new InvalidOperationException($"audit.forbidden_key: {key}");
        }

        if (request.Ip is not null)
        {
            data["ipHash"] = hasher.HashIp(request.Ip);
        }

        if (db.Database.CurrentTransaction is not null)
        {
            await InsertAsync(entry, data, ct).ConfigureAwait(false);
            return;
        }

        await db.ExecuteInUserTransactionAsync(() => InsertAsync(entry, data, ct), ct).ConfigureAwait(false);
    }

    private Task<int> InsertAsync(AuditEvent entry, JsonObject data, CancellationToken ct) => DbSql.ExecuteAsync(db, Insert, ct,
        DbSql.P("at", time.GetUtcNow()),
        DbSql.P("tenant", entry.TenantId),
        DbSql.P("actor", entry.ActorUserId),
        DbSql.P("kind", entry.ActorUserId is null ? "system" : "user"),
        DbSql.P("action", entry.Action),
        DbSql.P("entity_type", entry.EntityType),
        DbSql.P("entity_id", entry.EntityId),
        DbSql.Json("data", data.Count == 0 ? null : data.ToJsonString()));
}
