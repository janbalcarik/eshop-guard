using EshopGuard.Data.Connections;
using EshopGuard.Tests.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EshopGuard.Worker.Tests;

/// <summary>Worker host in environment <c>Testing</c> (no user-secrets <c>eshopguard-worker</c>) with captured logs.</summary>
internal static class WorkerTestHost
{
    public static IHost Build(InMemoryLoggerProvider logs, IDictionary<string, string?> settings, bool withStartupGuard = true)
    {
        var builder = WorkerHost.CreateBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Testing", Args = [] });
        var root = Directory.CreateTempSubdirectory("eshopguard-worker-").FullName;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "FileSystem",
            ["Storage:FileSystem:Root"] = root,
        });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        if (!withStartupGuard)
        {
            var guard = builder.Services.Single(d => d.ImplementationType == typeof(DatabaseStartupGuard));
            builder.Services.Remove(guard);
        }

        return builder.Build();
    }
}
