using EshopGuard.Data.Connections;
using EshopGuard.Tests.Shared;

namespace EshopGuard.Api.Tests;

[Trait("Category", "Db")]
public sealed class StartupGuardTests
{
    [Theory]
    [InlineData("Admin", DataErrorCodes.RoleBypassesRls)]
    [InlineData("Owner", DataErrorCodes.UnexpectedRole)]
    [InlineData("Worker", DataErrorCodes.UnexpectedRole)]
    [InlineData("Cms", DataErrorCodes.UnexpectedRole)]
    public async Task WrongRole_StopsStartupWithCode(string connection, string code)
    {
        await TestDatabase.EnsureMigratedAsync(TestContext.Current.CancellationToken);
        await using var factory = new ApiFactory(TestConfiguration.ConnectionString(connection));

        var ex = Assert.Throws<DatabaseStartupException>(() => factory.CreateClient());
        Assert.Equal(code, ex.Code);
        Assert.Contains(factory.Logs.Logs, l => l.Message.Contains(code, StringComparison.Ordinal));
        Assert.DoesNotContain(factory.Logs.Logs, l => l.Message.Contains("Now listening", StringComparison.Ordinal));
    }
}

public sealed class StartupGuardWithoutDatabaseTests
{
    [Fact]
    public async Task MissingConnectionString_StopsStartupWithKeyName()
    {
        await using var factory = new ApiFactory(appConnectionString: null);

        var ex = Assert.Throws<DatabaseStartupException>(() => factory.CreateClient());
        Assert.Equal(DataErrorCodes.ConnectionStringMissing, ex.Code);
        Assert.Contains(factory.Logs.Logs, l => l.Message.Contains("config.connection_string_missing ConnectionStrings:App", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingStorageRoot_StopsStartupWithKeyName()
    {
        // appsettings.json selects FileSystem but has no root: production must set Storage__FileSystem__Root explicitly.
        await using var factory = new ApiFactory(appConnectionString: null, withStorageRoot: false);

        var ex = Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() => factory.CreateClient());
        Assert.Equal(["config.storage_key_missing: Storage:FileSystem:Root"], ex.Failures);
    }
}
