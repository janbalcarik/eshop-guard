using EshopGuard.Data;
using EshopGuard.Data.Connections;
using EshopGuard.Jobs;
using EshopGuard.Jobs.Processing;
using EshopGuard.Storage;

namespace EshopGuard.Worker;

/// <summary>Builds the worker host (shared by <c>Program</c> and the tests).</summary>
public static class WorkerHost
{
    /// <summary>
    /// Generic Host with the database as <c>eshopguard_worker</c> (fixed here, not in configuration), file storage,
    /// job processing with the scheduler, JSON logs and a shutdown timeout of <c>Worker:ShutdownSeconds</c>.
    /// </summary>
    public static HostApplicationBuilder CreateBuilder(HostApplicationBuilderSettings settings)
    {
        var builder = Host.CreateApplicationBuilder(settings);
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole();

        builder.Services.AddEshopGuardData(DatabaseRole.Worker);
        builder.Services.AddEshopGuardStorage();
        builder.Services.AddEshopGuardJobProcessing();
        // Read without the validators: those report invalid settings when the host starts, not while it is built.
        builder.Services.AddOptions<HostOptions>().Configure<IConfiguration>((host, configuration) =>
        {
            var worker = new WorkerOptions();
            configuration.GetSection(WorkerOptions.SectionName).Bind(worker);
            host.ShutdownTimeout = TimeSpan.FromSeconds(worker.ShutdownSeconds);
        });
        return builder;
    }
}
