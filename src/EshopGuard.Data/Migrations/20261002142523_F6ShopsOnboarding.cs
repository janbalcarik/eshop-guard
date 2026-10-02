using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EshopGuard.Data.Migrations
{
    /// <summary>
    /// Change 10: the last recognition of the platform (<c>shop.shops.detection</c>), who decided the state of a language
    /// version by hand (<c>shop.shop_languages.decided_by</c>, <c>decided_at</c>; the analysis keeps such a state), the code of a
    /// failed check of ownership and the states of a check (<c>pending</c>, <c>verified</c>, <c>failed</c>). The unique index of
    /// the not deleted e-shops exists since F1 (<c>ix_shops_tenant_id_domain_base_path … WHERE deleted_at IS NULL</c>);
    /// <c>shop.shops.xmin</c> is the system column (the version of an e-shop for optimistic concurrency), nothing is added for it.
    /// </summary>
    public partial class F6ShopsOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonDocument>(
                name: "detection",
                schema: "shop",
                table: "shops",
                type: "jsonb",
                nullable: true);


            migrationBuilder.AddColumn<string>(
                name: "failure_code",
                schema: "shop",
                table: "shop_verifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "decided_at",
                schema: "shop",
                table: "shop_languages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "decided_by",
                schema: "shop",
                table: "shop_languages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_shop_verifications_status",
                schema: "shop",
                table: "shop_verifications",
                sql: "status IN ('pending', 'verified', 'failed')");

            migrationBuilder.CreateIndex(
                name: "ix_shop_languages_decided_by",
                schema: "shop",
                table: "shop_languages",
                column: "decided_by");

            // The column is new and empty. Validating existing rows would run under the RLS of the owner (FORCE ROW LEVEL
            // SECURITY) without a tenant and fail, so the constraint is added NOT VALID: every new or changed row is checked.
            migrationBuilder.Sql(
                "ALTER TABLE shop.shop_languages ADD CONSTRAINT fk_shop_languages_users_decided_by FOREIGN KEY (decided_by) " +
                "REFERENCES iam.users (id) ON DELETE RESTRICT NOT VALID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_shop_languages_users_decided_by",
                schema: "shop",
                table: "shop_languages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shop_verifications_status",
                schema: "shop",
                table: "shop_verifications");

            migrationBuilder.DropIndex(
                name: "ix_shop_languages_decided_by",
                schema: "shop",
                table: "shop_languages");

            migrationBuilder.DropColumn(
                name: "detection",
                schema: "shop",
                table: "shops");


            migrationBuilder.DropColumn(
                name: "failure_code",
                schema: "shop",
                table: "shop_verifications");

            migrationBuilder.DropColumn(
                name: "decided_at",
                schema: "shop",
                table: "shop_languages");

            migrationBuilder.DropColumn(
                name: "decided_by",
                schema: "shop",
                table: "shop_languages");
        }
    }
}
