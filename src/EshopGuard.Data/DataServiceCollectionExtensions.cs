using EshopGuard.Data.Connections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EshopGuard.Data;

/// <summary>Registration of the database layer.</summary>
public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the connection pool for <paramref name="role"/> (read lazily from <c>ConnectionStrings</c>),
    /// <see cref="EshopGuardDb"/>, <see cref="DatabaseInspector"/> and <see cref="DatabaseStartupGuard"/> as the first hosted service.
    /// The role comes from the host's code, never from configuration.
    /// </summary>
    public static IServiceCollection AddEshopGuardData(this IServiceCollection services, DatabaseRole role)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(sp => new EshopGuardDataSource(sp.GetRequiredService<IConfiguration>(), role));
        services.AddDbContext<EshopGuardDb>((sp, options) =>
            options.UseEshopGuardNpgsql(sp.GetRequiredService<EshopGuardDataSource>().Source));
        services.TryAddSingleton<DatabaseInspector>();
        services.Insert(0, ServiceDescriptor.Singleton<IHostedService, DatabaseStartupGuard>());
        return services;
    }
}
