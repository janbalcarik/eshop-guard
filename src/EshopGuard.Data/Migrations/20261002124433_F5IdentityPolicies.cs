using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EshopGuard.Data.Migrations
{
    /// <summary>
    /// Change 9: <c>iam.users.locale</c> may be empty (the automatic language), the policies by user and by invitation token,
    /// the sign-in audit without a tenant, the index of the pause between sign-in links and the grants of the cleanup.
    /// <c>iam.tenants.xmin</c> is the system column (the version of a tenant for optimistic concurrency), so nothing is added for it.
    /// </summary>
    public partial class F5IdentityPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "locale",
                schema: "iam",
                table: "users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.Sql(SqlResource.Read("F5/01_identity.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("F5/01_identity_down.sql"));
            migrationBuilder.Sql("UPDATE iam.users SET locale = 'sk' WHERE locale IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "locale",
                schema: "iam",
                table: "users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
