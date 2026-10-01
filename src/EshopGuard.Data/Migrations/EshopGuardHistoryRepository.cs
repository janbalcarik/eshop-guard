using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations.Internal;

namespace EshopGuard.Data.Migrations;

/// <summary>
/// Keeps the columns of <c>ops.__ef_migrations_history</c> as created by the migration <c>Initial</c>
/// (<c>MigrationId</c>, <c>ProductVersion</c>); the snake_case naming convention would otherwise rename them and EF
/// could no longer read the history of existing databases.
/// </summary>
#pragma warning disable EF1001 // NpgsqlHistoryRepository is provider-internal API; it is the documented extension point.
public sealed class EshopGuardHistoryRepository(HistoryRepositoryDependencies dependencies) : NpgsqlHistoryRepository(dependencies)
#pragma warning restore EF1001
{
    /// <inheritdoc />
    protected override void ConfigureTable(EntityTypeBuilder<HistoryRow> history)
    {
        base.ConfigureTable(history);
        history.Property(h => h.MigrationId).HasColumnName("MigrationId");
        history.Property(h => h.ProductVersion).HasColumnName("ProductVersion");
    }
}
