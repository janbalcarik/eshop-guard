using EshopGuard.Application.Shops;
using EshopGuard.Application.Shops.Onboarding;
using EshopGuard.Application.Shops.Ownership;
using EshopGuard.Application.Shops.Scope;
using EshopGuard.Application.Shops.Settings;
using EshopGuard.Core;
using EshopGuard.Core.Options;
using EshopGuard.Jobs.Runs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application;

/// <summary>Registration of the e-shops and the onboarding (change 10).</summary>
public static class ShopsServiceCollectionExtensions
{
    /// <summary>
    /// The policy of the verification of ownership for the API and the worker: <c>Shops:Ownership:RequiredBefore</c> is
    /// checked at start (no default, K rozhodnutí 1) and the policy replaces the denying default of change 8. Call before
    /// <c>AddRunService</c> / <c>AddAnalysisRuns</c>.
    /// </summary>
    public static IServiceCollection AddEshopGuardOwnershipPolicy(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<OwnershipPolicyOptions>().BindConfiguration(OwnershipPolicyOptions.SectionName).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OwnershipPolicyOptions>, ShopsOptionsValidator>());
        services.TryAddSingleton<ShopOwnershipPolicy>();
        services.TryAddSingleton<IShopOwnershipPolicy>(provider => provider.GetRequiredService<ShopOwnershipPolicy>());
        return services;
    }

    /// <summary>
    /// The services of the e-shops in the API: the catalog of markets and modules from the rules (section <c>EshopGuard</c>,
    /// paths under <c>EshopGuard:BaseDirectory</c> relative to the content root, as in the worker), the runs of change 8
    /// (<see cref="IRunService"/>) with the policy of ownership, the services of the e-shops, <c>Shops</c> and
    /// <c>Api:InteractiveWaitSeconds</c> checked at start. Requires <c>AddEshopGuardApplication</c> and the job queue.
    /// </summary>
    public static IServiceCollection AddEshopGuardShops(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddEshopGuardOwnershipPolicy();
        services.AddRunService();
        services.AddEshopGuardRules();
        services.AddOptions<EshopGuardOptions>().Configure<IConfiguration, IHostEnvironment>((options, configuration, environment) =>
        {
            configuration.GetSection("EshopGuard").Bind(options);
            var root = configuration["EshopGuard:BaseDirectory"] is { Length: > 0 } baseDirectory
                ? Path.GetFullPath(Path.Combine(environment.ContentRootPath, baseDirectory))
                : environment.ContentRootPath;
            options.Rules.ResolveUnder(root);
        });
        services.AddOptions<ShopsOptions>().BindConfiguration(ShopsOptions.SectionName).ValidateOnStart();
        services.AddOptions<InteractiveOptions>().BindConfiguration(InteractiveOptions.SectionName).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ShopsOptions>, ShopsOptionsValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<InteractiveOptions>, ShopsOptionsValidator>());

        services.TryAddSingleton<ShopCatalog>();
        services.TryAddSingleton<IConnectorCatalog, NoConnectorCatalog>();
        services.TryAddSingleton<IJobCompletionAwaiter, JobCompletionAwaiter>();
        services.TryAddScoped<ShopReader>();
        services.TryAddScoped<ShopService>();
        services.TryAddScoped<PlatformService>();
        services.TryAddScoped<SourceModeService>();
        services.TryAddScoped<SampleService>();
        services.TryAddScoped<SampleResultReader>();
        services.TryAddScoped<MarketService>();
        services.TryAddScoped<LanguageVersionService>();
        services.TryAddScoped<ScopeInputsLoader>();
        services.TryAddScoped<ScopeService>();
        services.TryAddScoped<ShopOrderReadiness>();
        services.TryAddScoped<IShopOrderReadiness>(provider => provider.GetRequiredService<ShopOrderReadiness>());
        services.TryAddScoped<OnboardingStateService>();
        services.TryAddScoped<OwnershipService>();
        services.TryAddScoped<ShopSettingsService>();
        return services;
    }
}
