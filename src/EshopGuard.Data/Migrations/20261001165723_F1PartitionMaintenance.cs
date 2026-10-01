using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EshopGuard.Data.Migrations
{
    /// <inheritdoc />
    public partial class F1PartitionMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ops.ensure_monthly_partitions and the monthly partitions for this month and the next three.
            migrationBuilder.Sql(SqlResource.Read("F1/06_partition_maintenance.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("F1/06_partition_maintenance_down.sql"));
        }
    }
}
