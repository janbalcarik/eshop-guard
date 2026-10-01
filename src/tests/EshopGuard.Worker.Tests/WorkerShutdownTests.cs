using System.Diagnostics;
using EshopGuard.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EshopGuard.Worker.Tests;

/// <summary>Shutdown of the skeleton, without the database (startup guard removed).</summary>
public sealed class WorkerShutdownTests
{
    [Fact]
    public async Task Stop_AfterTwoSeconds_FinishesWithinShutdownSeconds()
    {
        var logs = new InMemoryLoggerProvider();
        using var host = WorkerTestHost.Build(logs, new Dictionary<string, string?>
        {
            ["Worker:Id"] = "test-worker-2",
            ["Worker:ShutdownSeconds"] = "5",
        }, withStartupGuard: false);

        Assert.Equal(TimeSpan.FromSeconds(5), host.Services.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout);
        await host.StartAsync(TestContext.Current.CancellationToken);
        await logs.WaitForAsync("worker.started test-worker-2", TimeSpan.FromSeconds(10));
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var stopwatch = Stopwatch.StartNew();
        await host.StopAsync(TestContext.Current.CancellationToken);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Shutdown took {stopwatch.Elapsed}.");
        Assert.True(logs.Contains("worker.stopped test-worker-2"));
    }

    [Fact]
    public void DefaultIdentity_IsMachineAndProcess()
    {
        Assert.Equal($"{Environment.MachineName}:{Environment.ProcessId}", new WorkerOptions().EffectiveId);
        Assert.Equal(90, new WorkerOptions().ShutdownSeconds);
    }
}
