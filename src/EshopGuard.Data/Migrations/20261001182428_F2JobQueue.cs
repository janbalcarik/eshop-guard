using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EshopGuard.Data.Migrations
{
    /// <inheritdoc />
    public partial class F2JobQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_jobs_resource_class_priority_not_before_id",
                schema: "ops",
                table: "jobs");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_finished",
                schema: "ops",
                table: "jobs",
                column: "finished_at",
                filter: "state IN ('succeeded', 'canceled', 'failed')");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_queued_key_head",
                schema: "ops",
                table: "jobs",
                columns: new[] { "concurrency_key", "priority", "id" },
                filter: "state = 'queued' AND concurrency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_queued_keyed",
                schema: "ops",
                table: "jobs",
                columns: new[] { "resource_class", "priority", "id" },
                filter: "state = 'queued' AND concurrency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_queued_unkeyed",
                schema: "ops",
                table: "jobs",
                columns: new[] { "resource_class", "priority", "id" },
                filter: "state = 'queued' AND concurrency_key IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_running_tenant",
                schema: "ops",
                table: "jobs",
                columns: new[] { "tenant_id", "resource_class" },
                filter: "state = 'running'");

            migrationBuilder.CreateIndex(
                name: "ux_jobs_concurrency_running",
                schema: "ops",
                table: "jobs",
                column: "concurrency_key",
                unique: true,
                filter: "state = 'running' AND concurrency_key IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_jobs_attempts",
                schema: "ops",
                table: "jobs",
                sql: "attempts >= 0 AND max_attempts >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_jobs_lease",
                schema: "ops",
                table: "jobs",
                sql: "(state = 'running') = (lease_owner IS NOT NULL AND lease_until IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_domains_lease_job_id",
                schema: "ops",
                table: "domains",
                column: "lease_job_id",
                filter: "lease_job_id IS NOT NULL");

            // jobs.paused_classes and the rate limit buckets jev, openai, openai:tokens.
            migrationBuilder.Sql(SqlResource.Read("F2/01_job_queue.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("F2/01_job_queue_down.sql"));

            migrationBuilder.DropIndex(
                name: "ix_jobs_finished",
                schema: "ops",
                table: "jobs");

            migrationBuilder.DropIndex(
                name: "ix_jobs_queued_key_head",
                schema: "ops",
                table: "jobs");

            migrationBuilder.DropIndex(
                name: "ix_jobs_queued_keyed",
                schema: "ops",
                table: "jobs");

            migrationBuilder.DropIndex(
                name: "ix_jobs_queued_unkeyed",
                schema: "ops",
                table: "jobs");

            migrationBuilder.DropIndex(
                name: "ix_jobs_running_tenant",
                schema: "ops",
                table: "jobs");

            migrationBuilder.DropIndex(
                name: "ux_jobs_concurrency_running",
                schema: "ops",
                table: "jobs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_jobs_attempts",
                schema: "ops",
                table: "jobs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_jobs_lease",
                schema: "ops",
                table: "jobs");

            migrationBuilder.DropIndex(
                name: "ix_domains_lease_job_id",
                schema: "ops",
                table: "domains");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_resource_class_priority_not_before_id",
                schema: "ops",
                table: "jobs",
                columns: new[] { "resource_class", "priority", "not_before", "id" },
                filter: "state = 'queued'");
        }
    }
}
