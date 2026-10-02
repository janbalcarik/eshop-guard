using EshopGuard.Core;
using EshopGuard.Core.Fix;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Options;
using EshopGuard.Core.Profiles;
using EshopGuard.Core.Storage;
using EshopGuard.Data.Stores;
using EshopGuard.Jobs.Runs.Handlers;
using EshopGuard.Jobs.Runs.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EshopGuard.Jobs.Runs;

/// <summary>Registration of the analysis runs (change 8).</summary>
public static class RunsServiceCollectionExtensions
{
    /// <summary>
    /// <see cref="IRunService"/> with its policies, <see cref="IRunReadModel"/> and <see cref="IRunQueueEstimator"/> (the API of
    /// change 10 and the payments of change 12 use them). Requires
    /// <c>AddEshopGuardData</c> and <c>AddEshopGuardJobQueue</c>. The ownership of an e-shop is denied until change 10
    /// registers its policy before this call (fail-closed).
    /// </summary>
    public static IServiceCollection AddRunService(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<RunsOptions>().BindConfiguration(RunsOptions.SectionName).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<RunsOptions>, RunsOptionsValidator>());
        services.TryAddSingleton<IShopOwnershipPolicy, DenyAllOwnershipPolicy>();
        services.TryAddSingleton<IRunScopeResolver, ShopMarketsScopeResolver>();
        services.TryAddSingleton<IRunPaymentGate, OrderTablePaymentGate>();
        services.TryAddScoped<IRunService, RunService>();
        services.TryAddScoped<IRunReadModel, RunReadModel>();
        services.TryAddScoped<IRunQueueEstimator, RunQueueEstimator>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }

    /// <summary>
    /// The handlers of the steps of runs for the worker, and the library with its stores over PostgreSQL and the file store
    /// for the tenant of the running job (<see cref="RunAmbient"/>): Jev answers and rewrites in the cache of the tenant (never
    /// the answers of a mock), profiles of the e-shop, HTML and extractions of the run, the limit of calls shared by all
    /// workers, usage of every paid call. Requires <c>AddEshopGuardData</c>, <c>AddEshopGuardStorage</c> and
    /// <c>AddEshopGuardJobProcessing</c>. Services the host registers before this call replace the defaults (tests).
    /// </summary>
    public static IServiceCollection AddAnalysisRuns(this IServiceCollection services, Action<EshopGuardOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.AddRunService();
        var probe = new EshopGuardOptions();
        configure(probe);

        services.TryAddSingleton<IStoreTenant, AmbientStoreTenant>();
        if (!probe.Jev.UseMock)
        {
            services.TryAddSingleton<IJevCache, PgJevCache>();
        }

        if (!probe.Rewrite.UseMock)
        {
            services.TryAddSingleton<IRewriteCache, PgRewriteCache>();
        }

        services.TryAddSingleton<IPageProfileStore, PgPageProfileStore>();
        services.TryAddSingleton<IPageContentStore, BlobPageContentStore>();
        services.TryAddSingleton<IRateLimiter, SharedRateLimiter>();
        services.AddEshopGuard(configure);
        Decorate<IJevClient>(services, (provider, inner) => new UsageRecordingJevClient(inner, provider.GetRequiredService<UsageRecorder>()));

        services.TryAddSingleton<UsageRecorder>();
        services.TryAddSingleton<RunHandlerContext>();
        services.AddJobHandler<DiscoverHandler>();
        services.AddJobHandler<MarketsHandler>();
        services.AddJobHandler<FetchBatchHandler>();
        services.AddJobHandler<ProfileHandler>();
        services.AddJobHandler<SegmentHandler>();
        services.AddJobHandler<SieveBatchHandler>();
        services.AddJobHandler<PlanEvaluateHandler>();
        services.AddJobHandler<EvaluateBatchHandler>();
        services.AddJobHandler<RulesHandler>();
        services.AddJobHandler<RewriteBatchHandler>();
        services.AddJobHandler<FinalizeHandler>();
        return services;
    }

    /// <summary>Wraps the registered singleton of <typeparamref name="TService"/>.</summary>
    private static void Decorate<TService>(IServiceCollection services, Func<IServiceProvider, TService, TService> decorate)
        where TService : class
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(TService))
            ?? throw new InvalidOperationException($"{typeof(TService).Name} is not registered.");
        services.Remove(descriptor);
        services.AddSingleton(provider =>
        {
            var inner = descriptor.ImplementationInstance as TService
                ?? (descriptor.ImplementationFactory is { } factory ? (TService)factory(provider)
                    : (TService)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!));
            return decorate(provider, inner);
        });
    }
}
