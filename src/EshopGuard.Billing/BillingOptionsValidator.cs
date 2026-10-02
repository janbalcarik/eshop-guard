using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EshopGuard.Billing;

/// <summary>
/// Refuses the start of the API and the worker when the settings of billing are not safe (AD 13, fail-closed): the mode must
/// match the prefix of the key, a live key only in <c>Production</c>, the secret of the webhook is required, SuperFaktúra
/// outside <c>Production</c> only in the sandbox. The messages name the setting, never its value.
/// </summary>
internal sealed class BillingOptionsValidator(IHostEnvironment environment) : IValidateOptions<BillingOptions>
{
    public const string Code = "config.billing_invalid";

    private static readonly string[] TestPrefixes = ["sk_test_", "rk_test_"];
    private static readonly string[] LivePrefixes = ["sk_live_", "rk_live_"];

    public ValidateOptionsResult Validate(string? name, BillingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var production = environment.IsProduction();
        var stripe = options.Stripe;
        var mode = stripe.Mode;
        var enabled = mode is StripeModes.Test or StripeModes.Live;
        var key = stripe.SecretKey ?? string.Empty;
        var isTestKey = TestPrefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal));
        var isLiveKey = LivePrefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal));
        return Collect(
            (mode is not (StripeModes.Test or StripeModes.Live or StripeModes.Disabled), "Billing:Stripe:Mode"),
            (mode == StripeModes.Disabled && production, "Billing:Stripe:Mode"),
            (mode == StripeModes.Live && !production, "Billing:Stripe:Mode"),
            (!production && isLiveKey, "Billing:Stripe:SecretKey"),
            (mode == StripeModes.Test && !isTestKey, "Billing:Stripe:SecretKey"),
            (mode == StripeModes.Live && !isLiveKey, "Billing:Stripe:SecretKey"),
            (enabled && !(stripe.WebhookSecret ?? string.Empty).StartsWith("whsec_", StringComparison.Ordinal), "Billing:Stripe:WebhookSecret"),
            (!string.IsNullOrEmpty(stripe.ApiVersion) && stripe.ApiVersion != global::Stripe.StripeConfiguration.ApiVersion, "Billing:Stripe:ApiVersion"),
            (stripe.WebhookToleranceSeconds is < 1 or > 600, "Billing:Stripe:WebhookToleranceSeconds"),
            (!production && !options.SuperFaktura.Sandbox, "Billing:SuperFaktura:Sandbox"),
            (production && enabled && !options.SuperFaktura.IsConfigured, "Billing:SuperFaktura"),
            (!string.IsNullOrEmpty(options.SuperFaktura.BaseUrl) && !Uri.TryCreate(options.SuperFaktura.BaseUrl, UriKind.Absolute, out _), "Billing:SuperFaktura:BaseUrl"),
            (options.SuperFaktura.TimeoutSeconds is < 1 or > 120, "Billing:SuperFaktura:TimeoutSeconds"),
            (options.TrialMode is not (TrialModes.CalendarMonth or TrialModes.Days30), "Billing:TrialMode"),
            (options.TierChangeNoticeDays is < 0 or > 60, "Billing:TierChangeNoticeDays"),
            (options.CheckoutExpiresMinutes is < 30 or > 24 * 60, "Billing:CheckoutExpiresMinutes"),
            (!string.IsNullOrEmpty(options.PublicAppUrl) && (!Uri.TryCreate(options.PublicAppUrl, UriKind.Absolute, out _) || options.PublicAppUrl.EndsWith('/')), "Billing:PublicAppUrl"),
            (options.Tax.SupplierCountry.Length != 2, "Billing:Tax:SupplierCountry"),
            (!options.Tax.EuVatCountries.Contains(options.Tax.SupplierCountry, StringComparer.OrdinalIgnoreCase), "Billing:Tax:EuVatCountries"),
            (options.Tax.DomesticVatRate is <= 0 or > 50, "Billing:Tax:DomesticVatRate"),
            (!options.CheckoutSuccessPath.StartsWith('/') || !options.CheckoutCancelPath.StartsWith('/') || !options.PortalReturnPath.StartsWith('/'), "Billing:Checkout paths"));
    }

    private static ValidateOptionsResult Collect(params (bool Invalid, string Key)[] checks)
    {
        var invalid = checks.Where(c => c.Invalid).Select(c => $"{Code}: {c.Key}").Distinct(StringComparer.Ordinal).ToList();
        return invalid.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(invalid);
    }
}
