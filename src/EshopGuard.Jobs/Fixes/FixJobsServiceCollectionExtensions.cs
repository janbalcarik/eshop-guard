using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EshopGuard.Jobs.Fixes;

/// <summary>Registration of the jobs of the fixes (change 11) in the worker.</summary>
public static class FixJobsServiceCollectionExtensions
{
    /// <summary>The recheck of a text and the proposal after „Nie“. Requires the runs of change 8 (<c>IEshopGuard</c>, usage) and <c>IBlobStore</c>.</summary>
    public static IServiceCollection AddFixJobs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ExtractContextReader>();
        services.AddJobHandler<FixRecheckHandler>();
        return services;
    }
}
