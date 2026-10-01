using EshopGuard.Data.Connections;

namespace EshopGuard.Api.Tests;

public sealed class LogRedactionTests
{
    [Fact]
    public async Task FailedStartup_NeverLogsThePassword()
    {
        var sentinel = "SENTINEL-" + Guid.NewGuid().ToString("N");
        await using var factory = new ApiFactory($"Host=127.0.0.1;Port=1;Database=eshopguard_test;Username=eshopguard_app;Password={sentinel};Timeout=2");

        var ex = Assert.Throws<DatabaseStartupException>(() => factory.CreateClient());

        Assert.Equal(DataErrorCodes.Unreachable, ex.Code);
        Assert.DoesNotContain(sentinel, ex.ToString(), StringComparison.Ordinal);
        Assert.NotEmpty(factory.Logs.Logs);
        Assert.All(factory.Logs.Logs, l => Assert.DoesNotContain("SENTINEL-", l.AllText, StringComparison.Ordinal));
        Assert.Contains(factory.Logs.Logs, l => l.Message.Contains(DataErrorCodes.Unreachable, StringComparison.Ordinal));
    }
}
