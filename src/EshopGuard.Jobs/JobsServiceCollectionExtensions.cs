using EshopGuard.Jobs.Handlers;
using EshopGuard.Jobs.Maintenance;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Scheduling;
using EshopGuard.Jobs.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EshopGuard.Jobs;

/// <summary>Registration of the job queue (API) and of job processing (worker). Requires <c>AddEshopGuardData</c>.</summary>
public static class JobsServiceCollectionExtensions
{
    /// <summary>
    /// <see cref="IJobQueue"/> for enqueueing and canceling (the API). Binds <c>Jobs</c> and <c>Worker</c> (lease and
    /// tenant caps are used only by a worker).
    /// </summary>
    public static IServiceCollection AddEshopGuardJobQueue(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<JobsOptions>().BindConfiguration(JobsOptions.SectionName).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<JobsOptions>, JobsOptionsValidator>());
        services.AddOptions<WorkerOptions>().BindConfiguration(WorkerOptions.SectionName);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IJobQueue, PgJobQueue>();
        return services;
    }

    /// <summary>
    /// Job processing of the worker: queue, worker store, handler registry with the maintenance handlers,
    /// <see cref="JobProcessingService"/>, <see cref="JobNotificationListener"/> and <see cref="SchedulerService"/> with its
    /// tasks; <c>Worker</c> and <c>Scheduler</c> are validated at start (<c>config.worker_invalid</c>). The host sets its
    /// shutdown timeout to <c>Worker:ShutdownSeconds</c>.
    /// </summary>
    public static IServiceCollection AddEshopGuardJobProcessing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddEshopGuardJobQueue();
        services.AddOptions<WorkerOptions>().ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<WorkerOptions>, WorkerOptionsValidator>());
        services.AddOptions<SchedulerOptions>().BindConfiguration(SchedulerOptions.SectionName).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SchedulerOptions>, SchedulerOptionsValidator>());

        services.TryAddSingleton<IWorkerStore, PgWorkerStore>();
        services.TryAddSingleton<JobHandlerRegistry>();
        services.TryAddSingleton<JobNotificationHub>();
        services.AddJobHandler<EnsurePartitionsHandler>();
        services.AddJobHandler<CleanupJobsHandler>();
        services.AddJobHandler<AuthCleanupHandler>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IScheduledTask, LeaseReaperTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IScheduledTask, PartitionMaintenanceTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IScheduledTask, JobCleanupTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IScheduledTask, AuthCleanupTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IScheduledTask, WorkerRegistryCleanupTask>());

        // Stopped in reverse order: scheduler, listener, then processing (which drains the running jobs).
        services.TryAddSingleton<JobProcessingService>();
        services.AddHostedService(sp => sp.GetRequiredService<JobProcessingService>());
        services.AddHostedService<JobNotificationListener>();
        services.AddHostedService<SchedulerService>();
        return services;
    }

    /// <summary>Registers a handler (scoped, so it may use the job's <c>EshopGuardDb</c> and tenant).</summary>
    public static IServiceCollection AddJobHandler<THandler>(this IServiceCollection services)
        where THandler : class, IJobHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IJobHandler, THandler>());
        return services;
    }
}
