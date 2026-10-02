using EshopGuard.Billing.Jobs;
using EshopGuard.Billing.Stripe;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Queue;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Billing.Webhooks;

/// <summary>
/// The intake of a verified event of Stripe (requirement „Příjem webhooků Stripe s odstraněním duplicit“, task 6.1): one
/// transaction inserts the event into <c>billing.stripe_events</c> (<c>ON CONFLICT (id) DO NOTHING</c>) and, only when the row
/// is new, enqueues <c>billing.process_stripe_event</c> (<c>dedupe_key = stripe:{evt}</c>). The same event delivered again adds
/// neither a row nor a job. Used by the webhook (role app) and by the nightly reconciliation (role worker).
/// </summary>
public sealed class StripeEventIntake(EshopGuardDataSource dataSource, IJobQueue queue, TimeProvider time, ILogger<StripeEventIntake> logger)
{
    /// <summary>True when the event is new.</summary>
    public async Task<bool> StoreAsync(StripeEventEnvelope stripeEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stripeEvent);
        var now = time.GetUtcNow();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var inserted = await BillingSql.ExecuteAsync(transaction,
            """
            INSERT INTO billing.stripe_events (id, type, received_at, status, payload, livemode, attempts, object_id, created_at, updated_at)
            VALUES ($1, $2, $3, 'received', $4, $5, 0, $6, $3, $3)
            ON CONFLICT (id) DO NOTHING
            """, ct, stripeEvent.Id, stripeEvent.Type, now, new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = stripeEvent.Json }, stripeEvent.Livemode,
            stripeEvent.ObjectId).ConfigureAwait(false);
        if (inserted == 1)
        {
            await queue.EnqueueAsync(Process(stripeEvent.Id), transaction, ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        logger.LogInformation("stripe.event_received {EventId} {Type} {New}", stripeEvent.Id, stripeEvent.Type, inserted == 1);
        return inserted == 1;
    }

    /// <summary>The job that processes an event (P1, <c>io</c>: it reads the current object from Stripe).</summary>
    public static JobRequest Process(string eventId) => new(
        BillingJobs.ProcessStripeEventKind, JobResourceClass.Io, JobPriority.P1, BillingJobs.Payload(new System.Text.Json.Nodes.JsonObject { ["event_id"] = eventId }),
        DedupeKey: "stripe:" + eventId, MaxAttempts: 8);
}
