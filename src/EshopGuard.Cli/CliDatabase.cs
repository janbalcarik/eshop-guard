using EshopGuard.Data;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Stores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EshopGuard.Cli;

/// <summary>
/// The cache of the CLI in PostgreSQL: the same stores as the worker (<see cref="PostgresStoresExtensions"/>), role
/// <c>eshopguard_worker</c>, tenant <c>cli</c>. A run that may pay checks the database before it downloads anything.
/// </summary>
internal static class CliDatabase
{
    /// <summary><c>Application Name</c> of the connection when the connection string has none.</summary>
    public const string ApplicationName = "eshopguard-cli";

    /// <summary>
    /// Registers the connection (as the worker's <c>ConnectionStrings:Worker</c>) and the stores; without
    /// <paramref name="cacheAnswers"/> (<c>--no-cache</c>) only the profiles of page templates.
    /// </summary>
    /// <exception cref="InvalidOperationException">The connection string is not valid (its value is never shown).</exception>
    public static void Register(IServiceCollection services, string connectionString, bool cacheAnswers)
    {
        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("Připojení k databázi cache (ConnectionStrings:Cli) není platný připojovací řetězec.");
        }

        if (string.IsNullOrEmpty(builder.ApplicationName))
        {
            builder.ApplicationName = ApplicationName;
        }

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DatabaseRole.Worker.ConnectionStringKey()] = builder.ConnectionString })
            .Build());
        services.AddEshopGuardData(DatabaseRole.Worker);
        services.AddEshopGuardPostgresStores(_ => new FixedStoreTenant(CliTenant.Id), cacheAnswers);
    }

    /// <summary>
    /// The database is reachable, the role is subject to RLS, the migrations are applied and (unless <paramref name="tenant"/>
    /// is false) the tenant <c>cli</c> exists. Returns the error for the user, or null. Without a registered database
    /// (<c>--mock</c>) there is nothing to check.
    /// </summary>
    public static async Task<string?> CheckAsync(IServiceProvider services, CancellationToken ct, bool tenant = true)
    {
        if (services.GetService<DatabaseInspector>() is not { } inspector)
        {
            return null;
        }

        var connection = await inspector.CheckConnectionAsync(ct);
        if (!connection.Ok)
        {
            return $"Databáze cache není dostupná ({connection.Code}). Zkontrolujte ConnectionStrings:Cli a že PostgreSQL běží; nic se nestahovalo ani neplatilo.";
        }

        var role = await inspector.CheckRoleAsync(ct);
        if (!role.Ok)
        {
            return $"Připojení k databázi cache používá nevhodnou roli ({role.Code}); CLI se připojuje jako eshopguard_worker, aby platila izolace tenantů.";
        }

        var migrations = await inspector.CheckMigrationsAsync(ct);
        if (!migrations.Ok)
        {
            return $"Databáze cache nemá aktuální migrace ({migrations.Code}); spusťte dotnet ef database update --project src/EshopGuard.Data.";
        }

        if (tenant && !await CliTenant.ExistsAsync(services.GetRequiredService<EshopGuardDataSource>(), ct))
        {
            return "V databázi cache chybí tenant cli; spusťte jednou eshopguard cache init.";
        }

        return null;
    }
}
