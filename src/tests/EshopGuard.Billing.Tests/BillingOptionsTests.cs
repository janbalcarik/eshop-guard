using EshopGuard.Billing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace EshopGuard.Billing.Tests;

/// <summary>The settings of billing (change 12, tasks 1.2 and 1.3; requirement „Testovací režim a ochrana platebních klíčů“).</summary>
public sealed class BillingOptionsTests
{
    private const string TestKey = "sk_test_51NcFakeKeyForTheTestsOnly0000000000";
    // Fake live keys are put together at run time, so the scanning of secrets of GitHub does not take them for real ones.
    private static readonly string LiveKey = string.Concat("sk_", "live_", "51NcFakeKeyForTheTestsOnly0000000000");
    private static readonly string RestrictedLiveKey = string.Concat("rk_", "live_", "51NcFake");
    private const string WebhookSecret = "whsec_FakeSecretForTheTestsOnly";

    [Fact]
    public void ToString_masks_secrets()
    {
        var options = Valid();
        options.SuperFaktura.ApiKey = "sf-api-key-of-the-test";
        options.SuperFaktura.Email = "fakturacia@eshopguard.test";

        var text = options.ToString();

        Assert.DoesNotContain(TestKey, text, StringComparison.Ordinal);
        Assert.DoesNotContain(WebhookSecret, text, StringComparison.Ordinal);
        Assert.DoesNotContain("sf-api-key-of-the-test", text, StringComparison.Ordinal);
        Assert.DoesNotContain("fakturacia@", text, StringComparison.Ordinal);
        Assert.Contains("Stripe.SecretKey = ***", text, StringComparison.Ordinal);
        Assert.Contains("Stripe.Mode = test", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveKeyInDevelopment_FailsWithoutTheValue()
    {
        var options = Valid();
        options.Stripe.SecretKey = LiveKey;

        var result = Validate(options, Environments.Development);

        Assert.True(result.Failed);
        Assert.Contains("config.billing_invalid: Billing:Stripe:SecretKey", result.FailureMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("sk_live_", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ModeMustMatchThePrefixOfTheKey()
    {
        var test = Valid();
        test.Stripe.SecretKey = RestrictedLiveKey;
        var live = Valid();
        live.Stripe.Mode = StripeModes.Live;

        Assert.Contains("Billing:Stripe:SecretKey", Validate(test, Environments.Production).FailureMessage, StringComparison.Ordinal);
        Assert.Contains("Billing:Stripe:SecretKey", Validate(live, Environments.Production).FailureMessage, StringComparison.Ordinal);
        live.Stripe.SecretKey = RestrictedLiveKey;
        live.SuperFaktura.Sandbox = false;
        Fill(live.SuperFaktura);
        Assert.True(Validate(live, Environments.Production).Succeeded);
        Assert.Contains("Billing:Stripe:Mode", Validate(live, Environments.Staging).FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingWebhookSecret_Fails()
    {
        var options = Valid();
        options.Stripe.WebhookSecret = string.Empty;

        Assert.Contains("Billing:Stripe:WebhookSecret", Validate(options, Environments.Development).FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void SuperFakturaOutsideProduction_OnlyInTheSandbox()
    {
        var options = Valid();
        options.SuperFaktura.Sandbox = false;

        Assert.Contains("Billing:SuperFaktura:Sandbox", Validate(options, Environments.Development).FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingMode_Fails_AndDisabledOnlyOutsideProduction()
    {
        var missing = Valid();
        missing.Stripe.Mode = string.Empty;
        var disabled = new BillingOptions { Stripe = { Mode = StripeModes.Disabled }, Tax = Tax() };

        Assert.Contains("Billing:Stripe:Mode", Validate(missing, Environments.Development).FailureMessage, StringComparison.Ordinal);
        Assert.True(Validate(disabled, Environments.Development).Succeeded);
        Assert.Contains("Billing:Stripe:Mode", Validate(disabled, Environments.Production).FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiVersionMustBeThatOfThePackage()
    {
        var options = Valid();
        options.Stripe.ApiVersion = "2020-08-27";

        Assert.Contains("Billing:Stripe:ApiVersion", Validate(options, Environments.Development).FailureMessage, StringComparison.Ordinal);
        options.Stripe.ApiVersion = global::Stripe.StripeConfiguration.ApiVersion;
        Assert.True(Validate(options, Environments.Development).Succeeded);
    }

    [Fact]
    public void TaxData_IsRequired()
    {
        var options = Valid();
        options.Tax = new BillingOptions.TaxSettings();

        var message = Validate(options, Environments.Development).FailureMessage;

        Assert.Contains("Billing:Tax:SupplierCountry", message, StringComparison.Ordinal);
        Assert.Contains("Billing:Tax:EuVatCountries", message, StringComparison.Ordinal);
        Assert.Contains("Billing:Tax:DomesticVatRate", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionWithStripe_NeedsSuperFaktura()
    {
        var options = Valid();
        options.SuperFaktura.Sandbox = false;

        Assert.Contains("Billing:SuperFaktura", Validate(options, Environments.Production).FailureMessage, StringComparison.Ordinal);
        Fill(options.SuperFaktura);
        Assert.True(Validate(options, Environments.Production).Succeeded);
    }

    internal static BillingOptions Valid() => new()
    {
        Stripe = { Mode = StripeModes.Test, SecretKey = TestKey, WebhookSecret = WebhookSecret },
        Tax = Tax(),
    };

    /// <summary>The tax data of <c>appsettings.json</c>.</summary>
    internal static BillingOptions.TaxSettings Tax() => new()
    {
        SupplierCountry = "SK",
        EuVatCountries = ["AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "ES", "FI", "FR", "GR", "HR", "HU", "IE", "IT", "LT", "LU", "LV", "MT", "NL", "PL", "PT", "RO", "SE", "SI", "SK"],
        DomesticVatRate = 23m,
    };

    private static void Fill(BillingOptions.SuperFakturaSettings settings)
    {
        settings.Email = "api@eshopguard.test";
        settings.ApiKey = "sf-key";
        settings.CompanyId = "1";
    }

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(BillingOptions options, string environment) =>
        new BillingOptionsValidator(new Environment(environment)).Validate(null, options);

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "EshopGuard.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
