using EshopGuard.Data.Connections;
using EshopGuard.Worker;
using Microsoft.Extensions.Options;

using var host = WorkerHost.CreateBuilder(new HostApplicationBuilderSettings { Args = args }).Build();
try
{
    await host.RunAsync();
    return 0;
}
catch (DatabaseStartupException)
{
    // Already logged by DatabaseStartupGuard with its code.
    return 1;
}
catch (OptionsValidationException ex)
{
    host.Services.GetRequiredService<ILogger<WorkerOptions>>().LogCritical("Startup configuration invalid: {Failures}", string.Join("; ", ex.Failures));
    return 1;
}
