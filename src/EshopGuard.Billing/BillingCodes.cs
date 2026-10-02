using EshopGuard.Application.Problems;

namespace EshopGuard.Billing;

/// <summary>Codes of the errors and states of billing (change 12); texts are composed by the frontend from the code and its parameters.</summary>
public static class BillingCodes
{
    /// <summary>Stripe is switched off or does not answer (503).</summary>
    public const string Unavailable = ProblemCodes.BillingUnavailable;

    public const string PriceListMissing = "billing.price_list_missing";
    public const string PriceListNotFound = "billing.price_list_not_found";
    public const string PriceListNotEditable = "billing.price_list_not_editable";
    public const string PriceListTiersInvalid = "billing.price_list_tiers_invalid";
    public const string PriceListValidFromInvalid = "billing.price_list_valid_from_invalid";
    public const string StripeModeMismatch = "billing.stripe_mode_mismatch";
    public const string FairUseExceeded = "billing.fair_use_exceeded";
    public const string IndividualOffer = "billing.individual_offer";
    public const string QuoteNotPayable = "billing.quote_not_payable";
    public const string QuoteNotFound = "billing.quote_not_found";
    public const string QuoteStale = "quote.stale";
    public const string OrderNotFound = "billing.order_not_found";
    public const string OrderNotOpen = "billing.order_not_open";
    public const string SavedCardMissing = "billing.saved_card_missing";
    public const string TaxIdPending = "billing.tax_id_pending";
    public const string TaxTreatmentUndetermined = "billing.tax_treatment_undetermined";
    public const string CompanyIdRequired = "billing.company_id_required";
    public const string SubscriptionNotFound = "billing.subscription_not_found";
    public const string SubscriptionNotCancelable = "billing.subscription_not_cancelable";
    public const string SubscriptionNotResumable = "billing.subscription_not_resumable";
    public const string SubscriptionAlreadyRunning = "billing.subscription_already_running";
    public const string InvoiceNotFound = "billing.invoice_not_found";
    public const string InvoicePdfMissing = "billing.invoice_pdf_missing";
    public const string ZipTooLarge = "billing.zip_too_large";
    public const string AdminRequired = "auth.platform_admin_required";
}
