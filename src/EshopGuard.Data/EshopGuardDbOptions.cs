using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace EshopGuard.Data;

/// <summary>Shared EF Core configuration of <see cref="EshopGuardDb"/> (runtime and design time).</summary>
public static class EshopGuardDbOptions
{
    /// <summary>PostgreSQL through a data source, migration history in <c>ops.__ef_migrations_history</c>.</summary>
    public static DbContextOptionsBuilder UseEshopGuardNpgsql(this DbContextOptionsBuilder options, NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(options);
        // EnableSensitiveDataLogging is never switched on: parameter values are customer texts.
        return options.UseNpgsql(dataSource, Npgsql);
    }

    /// <summary>PostgreSQL through a connection string (design time), same history table.</summary>
    public static DbContextOptionsBuilder UseEshopGuardNpgsql(this DbContextOptionsBuilder options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.UseNpgsql(connectionString, Npgsql);
    }

    private static void Npgsql(NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable(EshopGuardDb.MigrationsHistoryTable, EshopGuardDb.OpsSchema);
}
