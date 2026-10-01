using EshopGuard.Data.Connections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace EshopGuard.Data.Design;

/// <summary>
/// Context for <c>dotnet ef</c>. Reads only <c>ConnectionStrings:Migrations</c> (role <c>eshopguard_owner</c>) from
/// user-secrets <c>eshopguard-data</c> and environment variables (<c>ConnectionStrings__Migrations</c>); no default.
/// </summary>
public sealed class EshopGuardDbDesignTimeFactory : IDesignTimeDbContextFactory<EshopGuardDb>
{
    /// <inheritdoc />
    public EshopGuardDb CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(EshopGuardDbDesignTimeFactory).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = EshopGuardDataSource.BuildConnectionString(configuration, DatabaseRole.Owner);
        var options = new DbContextOptionsBuilder<EshopGuardDb>();
        options.UseEshopGuardNpgsql(connectionString);
        return new EshopGuardDb(options.Options);
    }
}
