using System.Globalization;
using EshopGuard.Data;
using EshopGuard.Data.Connections;
using EshopGuard.Jobs.Processing;
using EshopGuard.Tests.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EshopGuard.Jobs.Tests;

/// <summary>One worker host of a test.</summary>
internal sealed class TestWorker(string id, IHost host, InMemoryLoggerProvider logs)
{
    private int _stopped;

    public string Id { get; } = id;

    public IHost Host { get; } = host;

    public InMemoryLoggerProvider Logs { get; } = logs;

    public JobProcessingService Processing => Host.Services.GetRequiredService<JobProcessingService>();

    /// <summary>Clean shutdown (draining, return of unfinished jobs, removal of the worker row).</summary>
    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 0)
        {
            await Host.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>Like a killed process: nothing more is written, then the host (scheduler, listener) stops too.</summary>
    public async Task CrashAsync()
    {
        await Processing.SimulateCrashAsync();
        await StopAsync();
    }
}

/// <summary>
/// Starts worker hosts in this process against <c>eshopguard_test_jobs</c>: the real <see cref="JobProcessingService"/>,
/// notification listener and scheduler with the test handlers, a short lease (2 s) and heartbeat (0.5 s). All slots are 0
/// unless the test sets them.
/// </summary>
internal sealed class WorkerHarness(JobsTestDatabase database, TestState state) : IAsyncDisposable
{
    private readonly List<TestWorker> _workers = [];

    public IReadOnlyList<TestWorker> All => _workers;

    public async Task<TestWorker> StartAsync(string name, IDictionary<string, string?>? settings = null, Action<IServiceCollection>? services = null)
    {
        var id = $"{name}-{Guid.NewGuid():N}"[..12];
        var configuration = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Worker"] = JobsTestDatabase.ConnectionString("Worker"),
            ["Worker:Id"] = id,
            ["Worker:LeaseSeconds"] = "2",
            ["Worker:HeartbeatSeconds"] = "0.5",
            ["Worker:ShutdownSeconds"] = "1",
            ["Worker:MinPollMilliseconds"] = "50",
            ["Worker:MaxPollSeconds"] = "0.5",
            ["Worker:RegistryHeartbeatSeconds"] = "0.5",
            ["Scheduler:Enabled"] = "false",
            ["Scheduler:TickSeconds"] = "0.5",
            ["Jobs:Retry:BaseSeconds"] = "0.2",
            ["Jobs:Retry:MaxSeconds"] = "1",
            ["Jobs:PausedClassesCacheSeconds"] = "0.2",
        };
        foreach (var resourceClass in new[] { "Fetch", "Cpu", "Jev", "Llm", "Io", "System" })
        {
            configuration["Worker:Slots:" + resourceClass] = "0";
            configuration["Worker:TenantCaps:" + resourceClass] = "0";
        }

        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            configuration[key] = value;
        }

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Testing", DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(configuration);
        var logs = new InMemoryLoggerProvider();
        builder.Logging.AddProvider(logs);
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        builder.Services.AddEshopGuardData(DatabaseRole.Worker);
        builder.Services.AddEshopGuardJobProcessing();
        builder.Services.AddSingleton(state);
        builder.Services.AddTestHandlers();
        builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(
            double.Parse(configuration["Worker:ShutdownSeconds"]!, CultureInfo.InvariantCulture) + 5));
        services?.Invoke(builder.Services);

        var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        var worker = new TestWorker(id, host, logs);
        _workers.Add(worker);
        return worker;
    }

    /// <summary>Starts <paramref name="count"/> workers with the same settings.</summary>
    public async Task<List<TestWorker>> StartManyAsync(int count, IDictionary<string, string?> settings)
    {
        var workers = new List<TestWorker>();
        for (var i = 0; i < count; i++)
        {
            workers.Add(await StartAsync("w" + i, settings));
        }

        return workers;
    }

    /// <summary>Waits until the condition holds; on timeout the message carries the state of the queue.</summary>
    public async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout, string what)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Timeout {timeout} waiting for: {what}\nops.jobs:\n{await database.DumpJobsAsync()}");
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Waits until no job is queued or running.</summary>
    public Task WaitForEmptyQueueAsync(TimeSpan timeout) => WaitUntilAsync(
        async () => await database.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE state IN ('queued', 'running')") == 0,
        timeout, "all jobs finished");

    public async ValueTask DisposeAsync()
    {
        state.Gate.TrySetResult();
        foreach (var worker in _workers)
        {
            try
            {
                await worker.StopAsync();
            }
            finally
            {
                worker.Host.Dispose();
            }
        }
    }
}
