using EshopGuard.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace EshopGuard.Cli;

/// <summary>
/// Builds the library container for one command. Commands build it themselves because
/// switches such as <c>--mock</c> and <c>--no-cache</c> change the registrations.
/// </summary>
internal static class CliHost
{
    /// <param name="register">Registrations before the library (e.g. recording or replay of the site), which replace its defaults.</param>
    public static ServiceProvider BuildServices(CliConfiguration configuration, string logFile, bool useMock, bool noCache,
        Action<IServiceCollection>? register = null)
    {
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("System.Net.Http.HttpClient", Serilog.Events.LogEventLevel.Warning)
            .WriteTo.File(
                logFile,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddSerilog(logger, dispose: true);
        });
        register?.Invoke(services);
        services.AddEshopGuard(options => configuration.Apply(options, useMock, noCache));
        return services.BuildServiceProvider();
    }
}
