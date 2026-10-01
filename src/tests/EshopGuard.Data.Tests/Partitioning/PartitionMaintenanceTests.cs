using EshopGuard.Data.Connections;
using EshopGuard.Data.Maintenance;
using EshopGuard.Tests.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace EshopGuard.Data.Tests.Partitioning;

[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class PartitionMaintenanceTests(PostgresTestDatabase database)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static PartitionMaintainer Maintainer(string connection) => new(
        new EshopGuardDataSource(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Worker"] = TestConfiguration.ConnectionString(connection),
        }).Build(), DatabaseRole.Worker),
        NullLogger<PartitionMaintainer>.Instance);

    private static IEnumerable<string> FuturePartitions(int fromMonth, int toMonth)
    {
        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        foreach (var table in PartitionMaintainer.MonthlyTables)
        {
            for (var m = fromMonth; m <= toMonth; m++)
            {
                yield return $"{table}_y{month.AddMonths(m):yyyy}m{month.AddMonths(m):MM}";
            }
        }
    }

    [Fact]
    public async Task EnsureSixMonths_CreatesTheMissingTwelve_ThenNothing()
    {
        // Start from the state after the migration: this month and three more. Months 4–6 are empty in the test database.
        foreach (var name in FuturePartitions(4, 24))
        {
            await using var drop = database.For("Owner").CreateCommand($"DROP TABLE IF EXISTS {name}");
            await drop.ExecuteNonQueryAsync(Ct);
        }

        var maintainer = Maintainer("Worker");
        var first = await maintainer.EnsureMonthlyPartitionsAsync(6, Ct);
        var second = await maintainer.EnsureMonthlyPartitionsAsync(6, Ct);

        Assert.Equal(FuturePartitions(4, 6).Order(StringComparer.Ordinal), first.Order(StringComparer.Ordinal));
        Assert.Empty(second);
        var horizon = await maintainer.GetMonthlyHorizonAsync(Ct);
        var expected = DateOnly.FromDateTime(new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(6));
        Assert.All(PartitionMaintainer.MonthlyTables, t => Assert.Equal(expected, horizon[t]));
    }

    [Fact]
    public async Task TwoWorkersAtOnce_DoNotCollide()
    {
        await Task.WhenAll(Maintainer("Worker").EnsureMonthlyPartitionsAsync(7, Ct), Maintainer("Worker").EnsureMonthlyPartitionsAsync(7, Ct));
        Assert.Empty(await Maintainer("Worker").EnsureMonthlyPartitionsAsync(7, Ct));
    }

    [Fact]
    public async Task AppRole_CannotCreatePartitions()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() => Maintainer("App").EnsureMonthlyPartitionsAsync(3, Ct));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
    }

    [Fact]
    public async Task RangeOutsideZeroToTwentyFour_IsRefused()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() => Maintainer("Worker").EnsureMonthlyPartitionsAsync(100, Ct));
        Assert.Equal(PostgresErrorCodes.InvalidParameterValue, ex.SqlState);
    }
}
