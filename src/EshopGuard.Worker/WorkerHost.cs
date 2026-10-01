using EshopGuard.Data;
using EshopGuard.Data.Connections;
using EshopGuard.Storage;
using Microsoft.Extensions.Options;

namespace EshopGuard.Worker;

/// <summary>Builds the worker host (shared by <c>Program</c> and the tests).</summary>
public static class WorkerHost
{
    /// <summary>
    /// Generic Host with the database as <c>eshopguard_worker</c> (fixed here, not in configuration), file storage,
    /// JSON logs and a shutdown timeout of <c>Worker:ShutdownSeconds</c>.
    /// </summary>
    public static HostApplicationBuilder CreateBuilder(HostApplicationBuilderSettings settings)
    {
        var builder = Host.CreateApplicationBuilder(settings);
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole();

        builder.Services.AddEshopGuardData(DatabaseRole.Worker);
        builder.Services.AddEshopGuardStorage();
        builder.Services.AddOptions<WorkerOptions>().BindConfiguration(WorkerOptions.SectionName);
        builder.Services.AddOptions<HostOptions>().Configure<IOptions<WorkerOptions>>((host, worker) =>
            host.ShutdownTimeout = TimeSpan.FromSeconds(worker.Value.ShutdownSeconds));
        builder.Services.AddHostedService<WorkerSkeletonService>();
        return builder;
    }
}
