using EshopGuard.Data.Connections;
using EshopGuard.Tests.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EshopGuard.Worker.Tests;

/// <summary>
/// Worker host in environment <c>Testing</c> (no user-secrets <c>eshopguard-worker</c>) with captured logs. All slots are 0 and
/// the scheduler is off unless a test sets them: the database <c>eshopguard_test</c> is shared with other test projects, whose
/// jobs this worker must not take.
/// </summary>
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
            ["Worker:Slots:Fetch"] = "0",
            ["Worker:Slots:Cpu"] = "0",
            ["Worker:Slots:Jev"] = "0",
            ["Worker:Slots:Llm"] = "0",
            ["Worker:Slots:Io"] = "0",
            ["Worker:Slots:System"] = "0",
            ["Scheduler:Enabled"] = "false",
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
