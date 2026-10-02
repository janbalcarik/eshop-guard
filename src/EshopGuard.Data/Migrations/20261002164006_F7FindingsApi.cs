using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EshopGuard.Data.Migrations
{
    /// <summary>
    /// Change 11: the state and error code of a protocol (rendered by the worker), what the recheck of a proposal or a group
    /// found, how a group is fixed (mode, own wording, where the fix fits), the language-neutral target of a notification; the
    /// order of findings by their strictest verdict, search by trigrams, questions by code and the triggers <c>pg_notify</c> of
    /// the live progress (only ids). No new table of a tenant, so RLS, <c>TableNames</c> and <c>TenantDataSeeder</c> stay.
    /// The unique number of a protocol in a tenant exists since F1.
    /// </summary>
    public partial class F7FindingsApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "error_code",
                schema: "fixes",
                table: "protocols",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "fixes",
                table: "protocols",
                type: "text",
                nullable: false,
                defaultValue: "rendering");

            migrationBuilder.AddColumn<JsonDocument>(
                name: "route",
                schema: "iam",
                table: "notifications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "recheck_result",
                schema: "fixes",
                table: "fix_proposals",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "custom_text",
                schema: "fixes",
                table: "fix_groups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "fit",
                schema: "fixes",
                table: "fix_groups",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mode",
                schema: "fixes",
                table: "fix_groups",
                type: "text",
                nullable: false,
                defaultValue: "replace");

            migrationBuilder.AddColumn<JsonDocument>(
                name: "recheck_result",
                schema: "fixes",
                table: "fix_groups",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recheck_status",
                schema: "fixes",
                table: "fix_groups",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_protocols_status",
                schema: "fixes",
                table: "protocols",
                sql: "status IN ('rendering', 'ready', 'failed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fix_groups_mode",
                schema: "fixes",
                table: "fix_groups",
                sql: "mode IN ('replace', 'remove', 'custom')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fix_groups_recheck_status",
                schema: "fixes",
                table: "fix_groups",
                sql: "recheck_status IN ('ok', 'still_finding', 'pending')");

            migrationBuilder.Sql(SqlResource.Read("F7/01_findings_api.sql"));
            migrationBuilder.Sql(SqlResource.Read("F7/02_notify_triggers.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("F7/02_notify_triggers_down.sql"));
            migrationBuilder.Sql(SqlResource.Read("F7/01_findings_api_down.sql"));

            migrationBuilder.DropCheckConstraint(
                name: "ck_protocols_status",
                schema: "fixes",
                table: "protocols");

            migrationBuilder.DropCheckConstraint(
                name: "ck_fix_groups_mode",
                schema: "fixes",
                table: "fix_groups");

            migrationBuilder.DropCheckConstraint(
                name: "ck_fix_groups_recheck_status",
                schema: "fixes",
                table: "fix_groups");

            migrationBuilder.DropColumn(
                name: "error_code",
                schema: "fixes",
                table: "protocols");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "fixes",
                table: "protocols");

            migrationBuilder.DropColumn(
                name: "route",
                schema: "iam",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "recheck_result",
                schema: "fixes",
                table: "fix_proposals");

            migrationBuilder.DropColumn(
                name: "custom_text",
                schema: "fixes",
                table: "fix_groups");

            migrationBuilder.DropColumn(
                name: "fit",
                schema: "fixes",
                table: "fix_groups");

            migrationBuilder.DropColumn(
                name: "mode",
                schema: "fixes",
                table: "fix_groups");

            migrationBuilder.DropColumn(
                name: "recheck_result",
                schema: "fixes",
                table: "fix_groups");

            migrationBuilder.DropColumn(
                name: "recheck_status",
                schema: "fixes",
                table: "fix_groups");
        }
    }
}
