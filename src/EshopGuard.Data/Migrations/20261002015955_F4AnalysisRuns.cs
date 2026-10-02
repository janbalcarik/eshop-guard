using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EshopGuard.Data.Migrations
{
    /// <inheritdoc />
    public partial class F4AnalysisRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_usage_records_operation",
                schema: "usage",
                table: "usage_records");

            migrationBuilder.DropCheckConstraint(
                name: "ck_usage_daily_operation",
                schema: "usage",
                table: "usage_daily");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shop_languages_source",
                schema: "shop",
                table: "shop_languages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shop_languages_status",
                schema: "shop",
                table: "shop_languages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shop_languages_switch_method",
                schema: "shop",
                table: "shop_languages");

            migrationBuilder.AddColumn<JsonDocument>(
                name: "crawl_scope",
                schema: "shop",
                table: "shop_languages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "run_scopes",
                schema: "checks",
                columns: table => new
                {
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_key = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_url = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<string>(type: "text", nullable: true),
                    scope = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    robots = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    frontier = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    pace = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    exhausted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    batches = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_run_scopes", x => new { x.run_id, x.scope_key });
                    table.ForeignKey(
                        name: "fk_run_scopes_runs_tenant_id_run_id",
                        columns: x => new { x.tenant_id, x.run_id },
                        principalSchema: "checks",
                        principalTable: "runs",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "run_urls",
                schema: "checks",
                columns: table => new
                {
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_key = table.Column<string>(type: "text", nullable: false),
                    url_hash = table.Column<long>(type: "bigint", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<string>(type: "text", nullable: true),
                    state = table.Column<string>(type: "text", nullable: false),
                    queue = table.Column<string>(type: "text", nullable: true),
                    seq = table.Column<int>(type: "integer", nullable: true),
                    batch_no = table.Column<int>(type: "integer", nullable: true),
                    attempts = table.Column<short>(type: "smallint", nullable: false),
                    http_status = table.Column<short>(type: "smallint", nullable: true),
                    error_code = table.Column<string>(type: "text", nullable: true),
                    page_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_run_urls", x => new { x.run_id, x.scope_key, x.url_hash });
                    table.CheckConstraint("ck_run_urls_queue", "queue IS NULL OR queue IN ('home', 'legal', 'product', 'other', 'sample_pair', 'sample_mandatory', 'sample_random')");
                    table.CheckConstraint("ck_run_urls_state", "state IN ('pending', 'fetched', 'extracted', 'failed', 'robots_blocked', 'excluded', 'over_limit', 'not_loaded', 'extract_timeout', 'too_large', 'offsite_redirect', 'ssrf_blocked', 'gone', 'not_html', 'not_modified')");
                    table.ForeignKey(
                        name: "fk_run_urls_runs_tenant_id_run_id",
                        columns: x => new { x.tenant_id, x.run_id },
                        principalSchema: "checks",
                        principalTable: "runs",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_usage_records_operation",
                schema: "usage",
                table: "usage_records",
                sql: "operation IN ('sentence_eval', 'sieve', 'profile', 'rewrite', 'recheck', 'fetch', 'market_analysis', 'version_language')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_usage_daily_operation",
                schema: "usage",
                table: "usage_daily",
                sql: "operation IN ('sentence_eval', 'sieve', 'profile', 'rewrite', 'recheck', 'fetch', 'market_analysis', 'version_language')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shop_languages_source",
                schema: "shop",
                table: "shop_languages",
                sql: "source IN ('main', 'hreflang', 'switcher', 'connector', 'llm', 'user')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shop_languages_status",
                schema: "shop",
                table: "shop_languages",
                sql: "status IN ('active', 'excluded', 'needs_confirmation', 'unsupported', 'needs_browser', 'mismatch')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shop_languages_switch_method",
                schema: "shop",
                table: "shop_languages",
                sql: "switch_method IN ('path', 'subdomain', 'domain', 'query', 'cookie', 'accept_language', 'script', 'browser_translation')");

            migrationBuilder.CreateIndex(
                name: "ix_page_versions_shop_id_page_id_run_id",
                schema: "content",
                table: "page_versions",
                columns: new[] { "shop_id", "page_id", "run_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fix_proposals_shop_id_created_run_id_page_id_field_block_in",
                schema: "fixes",
                table: "fix_proposals",
                columns: new[] { "shop_id", "created_run_id", "page_id", "field", "block_index" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_findings_shop_id_rule_id",
                schema: "checks",
                table: "findings",
                columns: new[] { "shop_id", "rule_id" },
                unique: true,
                filter: "scope = 'site'");

            migrationBuilder.CreateIndex(
                name: "ix_findings_shop_id_rule_id_page_id",
                schema: "checks",
                table: "findings",
                columns: new[] { "shop_id", "rule_id", "page_id" },
                unique: true,
                filter: "scope = 'page'");

            migrationBuilder.CreateIndex(
                name: "ix_run_scopes_tenant_id_run_id",
                schema: "checks",
                table: "run_scopes",
                columns: new[] { "tenant_id", "run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_run_urls_run_id_state",
                schema: "checks",
                table: "run_urls",
                columns: new[] { "run_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_run_urls_tenant_id_run_id",
                schema: "checks",
                table: "run_urls",
                columns: new[] { "tenant_id", "run_id" });

            migrationBuilder.Sql(SqlResource.Read("F4/01_analysis_runs.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("F4/01_analysis_runs_down.sql"));

            migrationBuilder.DropTable(
                name: "run_scopes",
                schema: "checks");

            migrationBuilder.DropTable(
                name: "run_urls",
                schema: "checks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_usage_records_operation",
                schema: "usage",
                table: "usage_records");

            migrationBuilder.DropCheckConstraint(
                name: "ck_usage_daily_operation",
                schema: "usage",
                table: "usage_daily");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shop_languages_source",
                schema: "shop",
                table: "shop_languages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shop_languages_status",
                schema: "shop",
                table: "shop_languages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shop_languages_switch_method",
                schema: "shop",
                table: "shop_languages");

            migrationBuilder.DropIndex(
                name: "ix_page_versions_shop_id_page_id_run_id",
                schema: "content",
                table: "page_versions");

            migrationBuilder.DropIndex(
                name: "ix_fix_proposals_shop_id_created_run_id_page_id_field_block_in",
                schema: "fixes",
                table: "fix_proposals");

            migrationBuilder.DropIndex(
                name: "ix_findings_shop_id_rule_id",
                schema: "checks",
                table: "findings");

            migrationBuilder.DropIndex(
                name: "ix_findings_shop_id_rule_id_page_id",
                schema: "checks",
                table: "findings");

            migrationBuilder.DropColumn(
                name: "crawl_scope",
                schema: "shop",
                table: "shop_languages");

            migrationBuilder.AddCheckConstraint(
                name: "ck_usage_records_operation",
                schema: "usage",
                table: "usage_records",
                sql: "operation IN ('sentence_eval', 'sieve', 'profile', 'rewrite', 'recheck', 'fetch')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_usage_daily_operation",
                schema: "usage",
                table: "usage_daily",
                sql: "operation IN ('sentence_eval', 'sieve', 'profile', 'rewrite', 'recheck', 'fetch')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shop_languages_source",
                schema: "shop",
                table: "shop_languages",
                sql: "source IN ('hreflang', 'switcher', 'connector', 'llm', 'user')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shop_languages_status",
                schema: "shop",
                table: "shop_languages",
                sql: "status IN ('active', 'excluded', 'needs_confirmation', 'unsupported')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shop_languages_switch_method",
                schema: "shop",
                table: "shop_languages",
                sql: "switch_method IN ('path', 'subdomain', 'domain', 'query', 'cookie')");
        }
    }
}
