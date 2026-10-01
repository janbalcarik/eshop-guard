using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EshopGuard.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Schema ops and its history table are created by EF before this migration runs (as eshopguard_owner).
            migrationBuilder.EnsureSchema(EshopGuardDb.OpsSchema);
            migrationBuilder.Sql("GRANT USAGE ON SCHEMA ops TO eshopguard_app, eshopguard_worker, eshopguard_admin;");
            migrationBuilder.Sql("GRANT SELECT ON ops.__ef_migrations_history TO eshopguard_app, eshopguard_worker, eshopguard_admin;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("REVOKE SELECT ON ops.__ef_migrations_history FROM eshopguard_app, eshopguard_worker, eshopguard_admin;");
            migrationBuilder.Sql("REVOKE USAGE ON SCHEMA ops FROM eshopguard_app, eshopguard_worker, eshopguard_admin;");
        }
    }
}
