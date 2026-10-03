using EshopGuard.Billing.Stripe;
using EshopGuard.Billing.Webhooks;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Scheduling;
using Microsoft.Extensions.Logging;

namespace EshopGuard.Billing.Jobs;

/// <summary><c>billing.process_stripe_event</c>: one stored event of Stripe (an error of Stripe: retry when transient, else fail).</summary>
public sealed class ProcessStripeEventHandler(StripeEventProcessor processor) : IJobHandler
{
    public string Kind => BillingJobs.ProcessStripeEventKind;

    public JobResourceClass ResourceClass => JobResourceClass.Io;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var eventId = context.Job.Payload.RootElement.GetProperty("event_id").GetString()!;
        var outcome = await processor.ProcessAsync(eventId, ct).ConfigureAwait(false);
        return outcome switch
        {
            { Status: "failed", Transient: true } => new JobResult.Retry(outcome.ErrorCode!),
            { Status: "failed" } => new JobResult.Fail(outcome.ErrorCode!),
            _ => JobResult.Done,
        };
    }
}

/// <summary>
/// <c>billing.reconcile_stripe</c> (daily 4:00 UTC, task 6.4): the events of the last 72 hours that change payments,
/// subscriptions, cards or refunds are read from Stripe and stored like a webhook; a lost webhook is processed by the same
/// processor, a delivered one is not stored twice (<c>stripe_events.id</c>), so no second invoice ever comes from it.
/// </summary>
public sealed class ReconcileStripeHandler(IStripeGateway stripe, StripeEventIntake intake, TimeProvider time, ILogger<ReconcileStripeHandler> logger) : IJobHandler
{
    public static readonly string[] Types =
    [
        "checkout.session.completed", "checkout.session.expired", "invoice.paid", "invoice.payment_failed", "customer.subscription.created",
        "customer.subscription.updated", "customer.subscription.deleted", "customer.updated", "customer.tax_id.updated", "charge.refunded",
        "setup_intent.succeeded",
    ];

    public string Kind => BillingJobs.ReconcileStripeKind;

    public JobResourceClass ResourceClass => JobResourceClass.Io;

    public Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct) => RunAsync(ct);

    public async Task<JobResult> RunAsync(CancellationToken ct)
    {
        IReadOnlyList<StripeEventEnvelope> events;
        try
        {
            events = await stripe.ListEventsAsync(time.GetUtcNow().AddHours(-72), Types, ct).ConfigureAwait(false);
        }
        catch (StripeGatewayException e)
        {
            return e.Transient ? new JobResult.Retry(e.Code) : new JobResult.Fail(e.Code);
        }

        var added = 0;
        foreach (var stripeEvent in events)
        {
            if (await intake.StoreAsync(stripeEvent, ct).ConfigureAwait(false))
            {
                added++;
            }
        }

        logger.LogInformation("stripe.reconciled {Events} {Added}", events.Count, added);
        return JobResult.Done;
    }
}

/// <summary>Enqueues <c>billing.reconcile_stripe</c> once a day from 4:00 UTC.</summary>
public sealed class ReconcileStripeTask(IJobQueue queue) : IScheduledTask
{
    public string Name => BillingJobs.ReconcileStripeKind;

    public TimeSpan Interval => TimeSpan.FromMinutes(10);

    public Task RunAsync(ScheduledTaskContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Now.UtcDateTime.Hour >= 4
            ? queue.EnqueueAsync(BillingJobs.ReconcileStripe(context.Now), context.Transaction, ct)
            : Task.CompletedTask;
    }
}
