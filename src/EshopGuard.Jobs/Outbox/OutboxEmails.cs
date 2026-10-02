using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Queue;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Jobs.Outbox;

/// <summary>An e-mail of the outbox: template, recipient (a user, or an address of someone without an account), language and parameters (codes and numbers only).</summary>
public sealed record OutboxEmail(string Template, Guid? ToUserId, string? To, string? Locale, JsonObject Params);

/// <summary>
/// E-mails without a token go through <c>ops.outbox</c> (change 9, AD 3). The outbox has RLS by tenant, so a worker cannot
/// scan it across tenants; every row therefore gets its own job <c>email.send</c> of its tenant in the same transaction
/// (dedupe key by row), and the job sends it (<c>EmailSendHandler</c> of <c>EshopGuard.Application</c>). The queue gives the
/// retries with backoff and one sender at a time; the row keeps <c>sent_at</c>, <c>attempts</c> and the error code.
/// </summary>
public static class OutboxEmails
{
    public const string SendKind = "email.send";

    /// <summary>Attempts of the job (the row's <c>attempts</c> count the same).</summary>
    public const int MaxAttempts = 8;

    /// <summary>Writes the row and its job in the caller's transaction (of the tenant); returns the id of the row.</summary>
    public static async Task<long> AddAsync(NpgsqlTransaction transaction, IJobQueue queue, Guid tenantId, OutboxEmail email, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(email);
        if (email.ToUserId is null == string.IsNullOrWhiteSpace(email.To))
        {
            throw new ArgumentException("An e-mail of the outbox has either a user or an address.", nameof(email));
        }

        var payload = new JsonObject
        {
            ["template"] = email.Template,
            ["to_user_id"] = email.ToUserId?.ToString("D"),
            ["to"] = email.To,
            ["locale"] = email.Locale,
            ["params"] = email.Params.DeepClone(),
        };
        await using var insert = new NpgsqlCommand(
            "INSERT INTO ops.outbox (tenant_id, kind, payload, attempts, created_at, updated_at) VALUES ($1, 'email', $2, 0, now(), now()) RETURNING id",
            transaction.Connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = tenantId },
                new NpgsqlParameter { Value = payload.ToJsonString(), NpgsqlDbType = NpgsqlDbType.Jsonb },
            },
        };
        var id = (long)(await insert.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
        await queue.EnqueueAsync(new JobRequest(
            SendKind, JobResourceClass.Io, JobPriority.P2, JsonDocument.Parse($$"""{"outbox_id":{{id}}}"""),
            TenantId: tenantId, DedupeKey: $"{SendKind}:{id}", MaxAttempts: MaxAttempts), transaction, ct).ConfigureAwait(false);
        return id;
    }
}

/// <summary>Codes of the templates of e-mails the worker writes to the outbox (the templates are in <c>EshopGuard.Application</c>).</summary>
public static class EmailTemplates
{
    public const string SampleFinished = "sample_finished";
    public const string RunFinished = "run_finished";
    public const string RunPartial = "run_partial";
    public const string RunFailed = "run_failed";
    public const string InvitationAccepted = "invitation_accepted";
}
