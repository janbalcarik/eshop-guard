using EshopGuard.Data.Migrations;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace EshopGuard.Data;

/// <summary>Shared EF Core configuration of <see cref="EshopGuardDb"/> (runtime and design time).</summary>
public static class EshopGuardDbOptions
{
    /// <summary>PostgreSQL through a data source, snake_case names, migration history in <c>ops.__ef_migrations_history</c>.</summary>
    public static DbContextOptionsBuilder UseEshopGuardNpgsql(this DbContextOptionsBuilder options, NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(options);
        // EnableSensitiveDataLogging is never switched on: parameter values are customer texts.
        return Common(options.UseNpgsql(dataSource, Npgsql));
    }

    /// <summary>PostgreSQL through a connection string (design time), same conventions.</summary>
    public static DbContextOptionsBuilder UseEshopGuardNpgsql(this DbContextOptionsBuilder options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Common(options.UseNpgsql(connectionString, Npgsql));
    }

    /// <summary>
    /// Interceptors of tenant data: soft delete, tenant check (fills or refuses <c>TenantId</c>), timestamps, and
    /// <c>app.tenant_id</c> for every transaction. Order matters: a soft delete becomes an update before the checks.
    /// </summary>
    public static DbContextOptionsBuilder UseEshopGuardInterceptors(this DbContextOptionsBuilder options, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        return options.AddInterceptors(
            new SoftDeleteSaveChangesInterceptor(time),
            new TenantSaveChangesInterceptor(),
            new TimestampSaveChangesInterceptor(time),
            new TenantTransactionInterceptor());
    }

    private static DbContextOptionsBuilder Common(DbContextOptionsBuilder options) =>
        options.UseSnakeCaseNamingConvention()
            .ReplaceService<IMigrationsSqlGenerator, EshopGuardMigrationsSqlGenerator>()
            .ReplaceService<IHistoryRepository, EshopGuardHistoryRepository>();

    private static void Npgsql(NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable(EshopGuardDb.MigrationsHistoryTable, EshopGuardDb.OpsSchema);
}
