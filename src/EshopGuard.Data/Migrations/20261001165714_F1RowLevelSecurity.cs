using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EshopGuard.Data.Migrations
{
    /// <inheritdoc />
    public partial class F1RowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RLS with FORCE and tenant_isolation on the 41 tenant tables, then the privileges of the application roles.
            migrationBuilder.Sql(SqlResource.Read("F1/04_rls.sql"));
            migrationBuilder.Sql(SqlResource.Read("F1/05_grants.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("F1/05_grants_down.sql"));
            migrationBuilder.Sql(SqlResource.Read("F1/04_rls_down.sql"));
        }
    }
}
