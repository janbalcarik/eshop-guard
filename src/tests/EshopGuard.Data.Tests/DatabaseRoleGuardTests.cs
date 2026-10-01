using EshopGuard.Data.Connections;

namespace EshopGuard.Data.Tests;

public sealed class DatabaseRoleGuardTests
{
    [Theory]
    [InlineData(DatabaseRole.App)]
    [InlineData(DatabaseRole.Worker)]
    [InlineData(DatabaseRole.Admin)]
    [InlineData(DatabaseRole.Owner)]
    public void Superuser_IsAlwaysRefused(DatabaseRole expected)
    {
        Assert.Equal(DataErrorCodes.RoleBypassesRls, DatabaseRoleGuard.Evaluate(new RoleInfo("postgres", true, true), expected));
        Assert.Equal(DataErrorCodes.RoleBypassesRls, DatabaseRoleGuard.Evaluate(new RoleInfo(expected.RoleName(), true, false), expected));
    }

    [Theory]
    [InlineData(DatabaseRole.App)]
    [InlineData(DatabaseRole.Worker)]
    [InlineData(DatabaseRole.Owner)]
    public void BypassRls_IsRefusedExceptForAdmin(DatabaseRole expected)
    {
        Assert.Equal(DataErrorCodes.RoleBypassesRls, DatabaseRoleGuard.Evaluate(new RoleInfo(expected.RoleName(), false, true), expected));
    }

    [Fact]
    public void BypassRls_IsAllowedForAdmin()
    {
        Assert.Null(DatabaseRoleGuard.Evaluate(new RoleInfo("eshopguard_admin", false, true), DatabaseRole.Admin));
    }

    [Theory]
    [InlineData("eshopguard_owner", DatabaseRole.App)]
    [InlineData("eshopguard_worker", DatabaseRole.App)]
    [InlineData("eshopguard_app", DatabaseRole.Worker)]
    [InlineData("eshopguard_cms", DatabaseRole.App)]
    [InlineData("ESHOPGUARD_APP", DatabaseRole.App)]
    public void OtherRole_IsRefused(string currentUser, DatabaseRole expected)
    {
        Assert.Equal(DataErrorCodes.UnexpectedRole, DatabaseRoleGuard.Evaluate(new RoleInfo(currentUser, false, false), expected));
    }

    [Theory]
    [InlineData(DatabaseRole.App)]
    [InlineData(DatabaseRole.Worker)]
    [InlineData(DatabaseRole.Owner)]
    public void ExpectedRole_Passes(DatabaseRole expected)
    {
        Assert.Null(DatabaseRoleGuard.Evaluate(new RoleInfo(expected.RoleName(), false, false), expected));
    }
}
