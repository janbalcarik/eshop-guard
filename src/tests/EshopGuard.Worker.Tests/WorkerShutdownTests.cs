using System.Diagnostics;
using EshopGuard.Jobs;
using EshopGuard.Jobs.Processing;
using EshopGuard.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EshopGuard.Worker.Tests;

/// <summary>Shutdown of the worker host with job processing (registration in ops.workers, logs, time limit).</summary>
[Trait("Category", "Db")]
public sealed class WorkerShutdownTests
{
    [Fact]
    public async Task Stop_AfterTwoSeconds_FinishesWithinShutdownSeconds_AndUnregisters()
    {
        await TestDatabase.EnsureMigratedAsync(TestContext.Current.CancellationToken);
        var logs = new InMemoryLoggerProvider();
        using var host = WorkerTestHost.Build(logs, new Dictionary<string, string?>
        {
            ["ConnectionStrings:Worker"] = TestConfiguration.ConnectionString("Worker"),
            ["Worker:Id"] = "test-worker-2",
            ["Worker:ShutdownSeconds"] = "5",
        });

        Assert.Equal(TimeSpan.FromSeconds(5), host.Services.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout);
        await host.StartAsync(TestContext.Current.CancellationToken);
        await logs.WaitForAsync("worker.started test-worker-2", TimeSpan.FromSeconds(10));
        Assert.Equal(1L, await WorkerRowsAsync("test-worker-2"));
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var stopwatch = Stopwatch.StartNew();
        await host.StopAsync(TestContext.Current.CancellationToken);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Shutdown took {stopwatch.Elapsed}.");
        Assert.True(logs.Contains("worker.stopped test-worker-2"));
        Assert.Equal(0L, await WorkerRowsAsync("test-worker-2"));
    }

    [Fact]
    public void DefaultIdentity_IsMachineAndProcess()
    {
        Assert.Equal($"{Environment.MachineName}:{Environment.ProcessId}", new WorkerOptions().EffectiveId);
        Assert.Equal(90, new WorkerOptions().ShutdownSeconds);
        Assert.Equal(Environment.ProcessorCount, new WorkerOptions().Slots.Cpu);
    }

    private static async Task<long> WorkerRowsAsync(string id)
    {
        await using var connection = new NpgsqlConnection(TestConfiguration.ConnectionString("Worker"));
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM ops.workers WHERE id = $1", connection) { Parameters = { new() { Value = id } } };
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}

public sealed class WorkerConfigurationTests
{
    [Fact]
    public async Task HeartbeatNotBelowAThirdOfLease_FailsStartWithCode()
    {
        var logs = new InMemoryLoggerProvider();
        using var host = WorkerTestHost.Build(logs, new Dictionary<string, string?>
        {
            ["Worker:HeartbeatSeconds"] = "60",
            ["Worker:LeaseSeconds"] = "120",
        }, withStartupGuard: false);

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        var failure = Assert.Single(ex.Failures);
        Assert.StartsWith(JobErrorCodes.WorkerConfigInvalid + ": Worker:HeartbeatSeconds", failure, StringComparison.Ordinal);
        Assert.False(logs.Contains("worker.started"));
    }

    [Fact]
    public async Task ShutdownNotBelowLease_FailsStartWithCode()
    {
        var logs = new InMemoryLoggerProvider();
        using var host = WorkerTestHost.Build(logs, new Dictionary<string, string?>
        {
            ["Worker:ShutdownSeconds"] = "120",
            ["Worker:LeaseSeconds"] = "120",
        }, withStartupGuard: false);

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        Assert.Contains(ex.Failures, f => f.StartsWith(JobErrorCodes.WorkerConfigInvalid + ": Worker:ShutdownSeconds", StringComparison.Ordinal));
    }

    [Fact]
    public void Defaults_AreValid_AndMatchTheDesign()
    {
        using var host = WorkerTestHost.Build(new InMemoryLoggerProvider(), new Dictionary<string, string?>(), withStartupGuard: false);
        _ = host.Services.GetRequiredService<IOptions<WorkerOptions>>().Value; // runs the validators

        var options = new WorkerOptions();
        Assert.Equal(100, options.Slots.Fetch);
        Assert.Equal(8, options.Slots.Jev);
        Assert.Equal(20, options.TenantCaps.Fetch);
        Assert.Equal(0, options.TenantCaps.System);
        Assert.Equal(TimeSpan.FromSeconds(85), options.DrainTimeout);
    }
}
