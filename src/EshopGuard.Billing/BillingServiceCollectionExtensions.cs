using EshopGuard.Application.Shops.Pricing;
using EshopGuard.Billing.Jobs;
using EshopGuard.Billing.Orders;
using EshopGuard.Billing.Pricing;
using EshopGuard.Billing.Stripe;
using EshopGuard.Billing.Subscriptions;
using EshopGuard.Billing.Webhooks;
using EshopGuard.Jobs.Notifications;
using EshopGuard.Jobs;
using EshopGuard.Jobs.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EshopGuard.Billing;

/// <summary>Registration of billing (change 12) for the API and the worker.</summary>
public static class BillingServiceCollectionExtensions
{
    /// <summary>
    /// <c>Billing</c> checked at start (<see cref="BillingOptionsValidator"/>), the gateway of Stripe (switched off with
    /// <c>Billing:Stripe:Mode = disabled</c>) and the check of webhooks. Requires the data and the application services.
    /// </summary>
    public static IServiceCollection AddEshopGuardBilling(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<BillingOptions>().BindConfiguration(BillingOptions.SectionName).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<BillingOptions>, BillingOptionsValidator>());
        services.TryAddSingleton<IStripeGateway>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<BillingOptions>>();
            return options.Value.StripeEnabled
                ? new StripeGateway(options).WithPortalConfiguration(options.Value.Stripe.PortalConfigurationId)
                : new DisabledStripeGateway();
        });
        services.TryAddSingleton<StripeWebhookVerifier>();
        services.TryAddSingleton(TimeProvider.System);

        // Prices and quotes (change 10 asks IPriceQuoteService for the price of a scope).
        services.TryAddScoped<PriceListReader>();
        services.TryAddScoped<PriceQuoteService>();
        services.TryAddScoped<IPriceQuoteService>(provider => provider.GetRequiredService<PriceQuoteService>());

        // Orders and payments (groups 5–7).
        services.TryAddScoped<StripeCustomers>();
        services.TryAddScoped<OrderService>();
        services.TryAddScoped<CheckoutService>();
        services.TryAddScoped<StripeEventIntake>();
        return services;
    }

    /// <summary>The jobs <c>billing.*</c> of the worker and their scheduled tasks; after <see cref="AddEshopGuardBilling"/>.</summary>
    public static IServiceCollection AddBillingJobs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<PriceListAdminService>();
        services.TryAddScoped<StripeCatalogSync>();
        services.AddJobHandler<PriceListAdminHandler>();
        services.AddJobHandler<SyncPriceListHandler>();
        services.AddJobHandler<ActivatePriceListHandler>();
        services.AddJobHandler<ArchiveUnusedPricesHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IScheduledTask, ArchiveUnusedPricesTask>());

        // Webhooks of Stripe and the nightly reconciliation (group 6), subscriptions and the card (group 7).
        services.AddNotifications();
        services.TryAddScoped<SubscriptionSync>();
        services.TryAddScoped<AccountCardService>();
        services.TryAddScoped<StripeEventProcessor>();
        services.AddJobHandler<ProcessStripeEventHandler>();
        services.AddJobHandler<ReconcileStripeHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IScheduledTask, ReconcileStripeTask>());
        return services;
    }
}
