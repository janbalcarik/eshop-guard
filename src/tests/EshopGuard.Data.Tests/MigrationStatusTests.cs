using EshopGuard.Data.Connections;

namespace EshopGuard.Data.Tests;

public sealed class MigrationStatusTests
{
    [Fact]
    public void AllApplied_HasNothingPendingOrAhead()
    {
        var status = MigrationStatus.Evaluate(["1_Initial", "2_Tenants"], ["1_Initial", "2_Tenants"]);
        Assert.False(status.HasPending);
        Assert.Empty(status.Ahead);
    }

    [Fact]
    public void MissingInDatabase_IsPending()
    {
        var status = MigrationStatus.Evaluate(["1_Initial"], ["1_Initial", "2_Tenants"]);
        Assert.True(status.HasPending);
        Assert.Equal(["2_Tenants"], status.Pending);
    }

    [Fact]
    public void EmptyDatabase_HasEverythingPending()
    {
        var status = MigrationStatus.Evaluate([], ["1_Initial"]);
        Assert.Equal(["1_Initial"], status.Pending);
    }

    [Fact]
    public void UnknownInDatabase_IsAheadButNotPending()
    {
        var status = MigrationStatus.Evaluate(["1_Initial", "2_Tenants"], ["1_Initial"]);
        Assert.False(status.HasPending);
        Assert.Equal(["2_Tenants"], status.Ahead);
    }
}
