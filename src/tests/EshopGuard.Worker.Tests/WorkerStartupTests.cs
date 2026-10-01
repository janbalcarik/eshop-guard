using EshopGuard.Data.Connections;
using EshopGuard.Tests.Shared;
using Microsoft.Extensions.Hosting;

namespace EshopGuard.Worker.Tests;

[Trait("Category", "Db")]
public sealed class WorkerStartupTests
{
    [Fact]
    public async Task WorkerRole_Starts_AndReportsItsIdentity()
    {
        await TestDatabase.EnsureMigratedAsync(TestContext.Current.CancellationToken);
        var logs = new InMemoryLoggerProvider();
        using var host = WorkerTestHost.Build(logs, new Dictionary<string, string?>
        {
            ["ConnectionStrings:Worker"] = TestConfiguration.ConnectionString("Worker"),
            ["Worker:Id"] = "test-worker-1",
        });

        await host.StartAsync(TestContext.Current.CancellationToken);
        await logs.WaitForAsync("worker.started test-worker-1", TimeSpan.FromSeconds(10));
        await host.StopAsync(TestContext.Current.CancellationToken);
        Assert.True(logs.Contains("worker.stopped test-worker-1"));
    }

    [Theory]
    [InlineData("App", DataErrorCodes.UnexpectedRole)]
    [InlineData("Owner", DataErrorCodes.UnexpectedRole)]
    [InlineData("Admin", DataErrorCodes.RoleBypassesRls)]
    public async Task OtherRole_IsRefusedBeforeAnyWork(string connection, string code)
    {
        await TestDatabase.EnsureMigratedAsync(TestContext.Current.CancellationToken);
        var logs = new InMemoryLoggerProvider();
        using var host = WorkerTestHost.Build(logs, new Dictionary<string, string?>
        {
            ["ConnectionStrings:Worker"] = TestConfiguration.ConnectionString(connection),
        });

        var ex = await Assert.ThrowsAsync<DatabaseStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        Assert.Equal(code, ex.Code);
        Assert.True(logs.Contains(code));
        Assert.False(logs.Contains("worker.started"));
    }
}

public sealed class WorkerWithoutConnectionTests
{
    [Fact]
    public async Task MissingConnectionString_IsRefusedWithKeyName()
    {
        var logs = new InMemoryLoggerProvider();
        using var host = WorkerTestHost.Build(logs, new Dictionary<string, string?> { ["ConnectionStrings:Worker"] = "" });

        var ex = await Assert.ThrowsAsync<DatabaseStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        Assert.Equal(DataErrorCodes.ConnectionStringMissing, ex.Code);
        Assert.True(logs.Contains("config.connection_string_missing ConnectionStrings:Worker"));
        Assert.False(logs.Contains("worker.started"));
    }
}
