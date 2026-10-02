namespace EshopGuard.Billing;

/// <summary>Values of <c>Billing:Stripe:Mode</c>.</summary>
public static class StripeModes
{
    /// <summary>Test keys (<c>sk_test_</c>, <c>rk_test_</c>) and test clocks.</summary>
    public const string Test = "test";

    /// <summary>Live keys (<c>sk_live_</c>, <c>rk_live_</c>); only in the environment <c>Production</c>.</summary>
    public const string Live = "live";

    /// <summary>
    /// No Stripe at all (development without keys, never <c>Production</c>): prices and quotes work from the database, every
    /// call of Stripe answers <c>503 billing.unavailable</c> and the webhook refuses everything.
    /// </summary>
    public const string Disabled = "disabled";
}

/// <summary>Values of <c>Billing:TrialMode</c> (K rozhodnutí 5).</summary>
public static class TrialModes
{
    /// <summary>To the same day and hour of the next month (31. 1. → 28. 2.); the default.</summary>
    public const string CalendarMonth = "calendar_month";

    /// <summary>30 days.</summary>
    public const string Days30 = "days_30";
}

/// <summary>
/// Settings under <c>Billing</c> (design of change 12). The keys only from user-secrets or the environment (never in
/// <c>appsettings.json</c>); <see cref="ToString"/> masks them.
/// </summary>
public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    public StripeSettings Stripe { get; set; } = new();

    public SuperFakturaSettings SuperFaktura { get; set; } = new();

    public TaxSettings Tax { get; set; } = new();

    /// <summary><see cref="TrialModes.CalendarMonth"/> or <see cref="TrialModes.Days30"/>.</summary>
    public string TrialMode { get; set; } = TrialModes.CalendarMonth;

    /// <summary>Days between the e-mail about a higher tier and its first period (K rozhodnutí 7).</summary>
    public int TierChangeNoticeDays { get; set; } = 7;

    /// <summary>Lifetime of a Checkout Session (Stripe allows 30 minutes to 24 hours).</summary>
    public int CheckoutExpiresMinutes { get; set; } = 60;

    /// <summary>Origin of the web application for the returns from Stripe; empty = <c>Frontend:BaseUrl</c>.</summary>
    public string PublicAppUrl { get; set; } = string.Empty;

    /// <summary>Return after a payment; <c>{tenantId}</c>, <c>{shopId}</c> and <c>{orderId}</c> are replaced, Stripe adds the session.</summary>
    public string CheckoutSuccessPath { get; set; } = "/app/{tenantId}/obchody/{shopId}/platba/{orderId}?session_id={CHECKOUT_SESSION_ID}";

    /// <summary>Return after leaving the payment page.</summary>
    public string CheckoutCancelPath { get; set; } = "/app/{tenantId}/obchody/{shopId}/rozsah";

    /// <summary>Return from the customer portal of Stripe (change of the card).</summary>
    public string PortalReturnPath { get; set; } = "/app/{tenantId}/predplatne";

    public sealed class StripeSettings
    {
        /// <summary><see cref="StripeModes.Test"/>, <see cref="StripeModes.Live"/> or <see cref="StripeModes.Disabled"/>; no default (fail-closed).</summary>
        public string Mode { get; set; } = string.Empty;

        /// <summary>Secret or restricted key (<c>sk_…</c>, <c>rk_…</c>).</summary>
        public string SecretKey { get; set; } = string.Empty;

        /// <summary>Secret of the webhook endpoint (<c>whsec_…</c>).</summary>
        public string WebhookSecret { get; set; } = string.Empty;

        /// <summary>Version of the API of Stripe; empty = the version of the package Stripe.net, else it must equal it.</summary>
        public string ApiVersion { get; set; } = string.Empty;

        /// <summary>Tolerance of the timestamp of <c>Stripe-Signature</c>.</summary>
        public int WebhookToleranceSeconds { get; set; } = 300;

        /// <summary>Configuration of the customer portal (only the change of the card); empty = the default of the account.</summary>
        public string PortalConfigurationId { get; set; } = string.Empty;
    }

    public sealed class SuperFakturaSettings
    {
        /// <summary>E-mail of the account of the API.</summary>
        public string Email { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;

        /// <summary>Company of the account (more companies under one login).</summary>
        public string CompanyId { get; set; } = string.Empty;

        /// <summary>Sandbox of SuperFaktúra; outside <c>Production</c> always true.</summary>
        public bool Sandbox { get; set; } = true;

        /// <summary>Base address of the API; empty = the address of the sandbox or of the production by <see cref="Sandbox"/>.</summary>
        public string BaseUrl { get; set; } = string.Empty;

        public int TimeoutSeconds { get; set; } = 20;

        /// <summary>True when the account is filled in (all three values).</summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(CompanyId);
    }

    /// <summary>
    /// Tax data of the supplier (design „Daňový režim“): its country, the countries of the EU VAT area (reverse charge) and the
    /// domestic rate for the preview only (Stripe Tax computes the charged tax). Data in <c>appsettings.json</c>, not code.
    /// </summary>
    public sealed class TaxSettings
    {
        public string SupplierCountry { get; set; } = string.Empty;

        public List<string> EuVatCountries { get; set; } = [];

        public decimal DomesticVatRate { get; set; }
    }

    /// <summary>True unless <c>Billing:Stripe:Mode</c> is <see cref="StripeModes.Disabled"/>.</summary>
    public bool StripeEnabled => Stripe.Mode is StripeModes.Test or StripeModes.Live;

    /// <summary>The settings without any secret: which keys are set, never their values.</summary>
    public override string ToString() =>
        $"Billing {{ Stripe.Mode = {Stripe.Mode}, Stripe.SecretKey = {Mask(Stripe.SecretKey)}, Stripe.WebhookSecret = {Mask(Stripe.WebhookSecret)}, "
        + $"SuperFaktura.Email = {Mask(SuperFaktura.Email)}, SuperFaktura.ApiKey = {Mask(SuperFaktura.ApiKey)}, SuperFaktura.Sandbox = {SuperFaktura.Sandbox}, "
        + $"TrialMode = {TrialMode}, TierChangeNoticeDays = {TierChangeNoticeDays}, CheckoutExpiresMinutes = {CheckoutExpiresMinutes} }}";

    private static string Mask(string value) => string.IsNullOrEmpty(value) ? "(empty)" : "***";
}
