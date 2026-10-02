using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EshopGuard.Jobs.Shops;

/// <summary>Registration of the jobs of an e-shop (change 10) in the worker.</summary>
public static class ShopJobsServiceCollectionExtensions
{
    /// <summary>
    /// The handlers of <c>shop.detect_platform</c> and <c>shop.verify_ownership</c>. Requires the library with its fetcher
    /// (<c>AddAnalysisRuns</c> or <c>AddEshopGuard</c>) and <c>AddEshopGuardJobProcessing</c>.
    /// </summary>
    public static IServiceCollection AddShopJobs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<ShopJobsOptions>().BindConfiguration(ShopJobsOptions.SectionName);
        services.TryAddSingleton<PlatformSignatureSource>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddJobHandler<ShopDetectPlatformHandler>();
        return services;
    }
}
