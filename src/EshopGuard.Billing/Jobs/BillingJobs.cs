using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Queue;

namespace EshopGuard.Billing.Jobs;

/// <summary>
/// The jobs <c>billing.*</c> (design of change 12, table of jobs): kinds, resource classes and deduplication keys. Jobs calling
/// Stripe or SuperFaktúra are <c>io</c>, the planning ones <c>system</c>. Payloads carry ids, codes and numbers only.
/// </summary>
public static class BillingJobs
{
    public const string PriceListAdminKind = "billing.price_list_admin";
    public const string SyncPriceListKind = "billing.sync_price_list";
    public const string ActivatePriceListKind = "billing.activate_price_list";
    public const string SchedulePriceListTransferKind = "billing.schedule_price_list_transfer";
    public const string ArchiveUnusedPricesKind = "billing.archive_unused_prices";
    public const string ComposeScheduleKind = "billing.compose_schedule";
    public const string ProcessStripeEventKind = "billing.process_stripe_event";
    public const string IssueInvoiceKind = "billing.issue_invoice";
    public const string IssueCreditNoteKind = "billing.issue_credit_note";
    public const string CheckEinvoiceStatusKind = "billing.check_einvoice_status";
    public const string EvaluateTiersKind = "billing.evaluate_tiers";
    public const string TrialReminderKind = "billing.trial_reminder";
    public const string ReconcileStripeKind = "billing.reconcile_stripe";
    public const string ExpireOrderKind = "billing.expire_order";

    /// <summary>A command of the admin API on a global price list, run by the worker (the API only reads price lists).</summary>
    public static JobRequest PriceListAdmin(JsonObject command) => new(
        PriceListAdminKind, JobResourceClass.System, JobPriority.P1, JsonDocument.Parse(command.ToJsonString()), MaxAttempts: 3);

    /// <summary>The synchronization to Stripe of one request to publish (a new attempt after an error is a new request).</summary>
    public static JobRequest SyncPriceList(Guid priceListId, Guid publishRequestId) => new(
        SyncPriceListKind, JobResourceClass.Io, JobPriority.P1, Payload(new JsonObject { ["price_list_id"] = Id(priceListId) }),
        DedupeKey: $"price-sync:{priceListId:N}:{publishRequestId:N}", ConcurrencyKey: $"price-list:{priceListId:N}", MaxAttempts: 6);

    public static JobRequest ActivatePriceList(Guid priceListId, DateTimeOffset validFrom) => new(
        ActivatePriceListKind, JobResourceClass.Io, JobPriority.P1, Payload(new JsonObject { ["price_list_id"] = Id(priceListId) }),
        DedupeKey: $"price-activate:{priceListId:N}", ConcurrencyKey: $"price-list:{priceListId:N}", MaxAttempts: 6, NotBefore: validFrom);

    public static JobRequest SchedulePriceListTransfer(Guid priceListId) => new(
        SchedulePriceListTransferKind, JobResourceClass.System, JobPriority.P2, Payload(new JsonObject { ["price_list_id"] = Id(priceListId) }),
        DedupeKey: $"price-transfer:{priceListId:N}", ConcurrencyKey: "price-transfer", MaxAttempts: 5);

    public static JobRequest ArchiveUnusedPrices(DateTimeOffset now) => new(
        ArchiveUnusedPricesKind, JobResourceClass.Io, JobPriority.P4, JobRequest.EmptyPayload(), DedupeKey: $"price-archive:{Day(now)}", MaxAttempts: 3);

    /// <summary>The invoice in SuperFaktúra of a paid invoice of Stripe (one per invoice of Stripe).</summary>
    public static JobRequest IssueInvoice(Guid tenantId, Guid? shopId, string stripeInvoiceId) => new(
        IssueInvoiceKind, JobResourceClass.Io, JobPriority.P2, Payload(new JsonObject { ["stripe_invoice_id"] = stripeInvoiceId }),
        TenantId: tenantId, ShopId: shopId, DedupeKey: "invoice:" + stripeInvoiceId, MaxAttempts: 12);

    /// <summary>The credit note of a refund (one per refund of Stripe).</summary>
    public static JobRequest IssueCreditNote(Guid tenantId, Guid? shopId, string refundId, string chargeId) => new(
        IssueCreditNoteKind, JobResourceClass.Io, JobPriority.P2, Payload(new JsonObject { ["refund_id"] = refundId, ["charge_id"] = chargeId }),
        TenantId: tenantId, ShopId: shopId, DedupeKey: "credit:" + refundId, MaxAttempts: 12);

    public static JobRequest ReconcileStripe(DateTimeOffset now) => new(
        ReconcileStripeKind, JobResourceClass.Io, JobPriority.P3, JobRequest.EmptyPayload(), DedupeKey: $"stripe-reconcile:{Day(now)}", MaxAttempts: 5);

    public static string Day(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Id(Guid id) => id.ToString("D");

    public static JsonDocument Payload(JsonObject payload) => JsonDocument.Parse(payload.ToJsonString());

    /// <summary>A Guid of the payload.</summary>
    public static Guid GuidOf(JsonDocument payload, string name) => Guid.Parse(payload.RootElement.GetProperty(name).GetString()!);
}
