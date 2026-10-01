using EshopGuard.Core.Profiles;
using EshopGuard.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Data.Stores;

/// <summary>Registration of the PostgreSQL stores of the library; the CLI and the worker call the same method.</summary>
public static class PostgresStoresExtensions
{
    /// <summary>
    /// Registers <see cref="PgJevCache"/>, <see cref="PgRewriteCache"/> and <see cref="PgPageProfileStore"/> for the tenant
    /// <paramref name="tenant"/> returns. Call after <c>AddEshopGuardData</c> and before <c>AddEshopGuard</c>, whose defaults
    /// (no cache, profiles in memory) they replace. Without <paramref name="cacheAnswers"/> (<c>--no-cache</c>) only the
    /// profiles are registered: they are the shop's template, not a cached answer.
    /// </summary>
    public static IServiceCollection AddEshopGuardPostgresStores(this IServiceCollection services, Func<IServiceProvider, IStoreTenant> tenant, bool cacheAnswers = true)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(tenant);
        services.AddSingleton(tenant);
        if (cacheAnswers)
        {
            services.AddSingleton<IJevCache, PgJevCache>();
            services.AddSingleton<IRewriteCache, PgRewriteCache>();
        }

        services.AddSingleton<IPageProfileStore, PgPageProfileStore>();
        return services;
    }
}
