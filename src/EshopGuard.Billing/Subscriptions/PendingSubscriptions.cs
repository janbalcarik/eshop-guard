using Npgsql;

namespace EshopGuard.Billing.Subscriptions;

/// <summary>
/// A subscription created for the saved card that never started (<c>incomplete</c>, 3-D Secure not confirmed) and was canceled in
/// Stripe (task 7.3): its row stops running at once, so the unique running subscription of the e-shop is free for the next payment
/// before the webhook of the cancellation comes. The e-shop is not touched; the webhook then changes nothing more.
/// </summary>
public static class PendingSubscriptions
{
    public const string NotStarted = "not_started";

    /// <summary>In the transaction of the tenant; the number of rows changed (0 when the webhook has not stored it yet).</summary>
    public static Task<int> MarkNotStartedAsync(NpgsqlTransaction transaction, string stripeSubscriptionId, DateTimeOffset now, CancellationToken ct) =>
        BillingSql.ExecuteAsync(transaction,
            """
            UPDATE billing.subscriptions SET status = 'canceled', canceled_at = $2, pause_reason = $3, updated_at = $2
            WHERE stripe_subscription_id = $1 AND status = 'incomplete'
            """, ct, stripeSubscriptionId, now, NotStarted);
}
