using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EshopGuard.Data.Migrations
{
    /// <inheritdoc />
    public partial class F3CacheInPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // checks.sieve_answers takes the shape of checks.jev_answers (legacy cache key, whole answer as jsonb). Rows of the
            // old shape (probabilities without question ids, written only by the isolation tests) cannot be converted; they are
            // a cache, so they are emptied (TRUNCATE is not subject to RLS) and asked again when needed.
            migrationBuilder.Sql("TRUNCATE checks.sieve_answers;");
            migrationBuilder.DropPrimaryKey(
                name: "pk_sieve_answers",
                schema: "checks",
                table: "sieve_answers");

            migrationBuilder.DropColumn(
                name: "chunk_hash",
                schema: "checks",
                table: "sieve_answers");

            migrationBuilder.DropColumn(
                name: "probabilities",
                schema: "checks",
                table: "sieve_answers");

            migrationBuilder.AlterColumn<byte[]>(
                name: "question_set_hash",
                schema: "checks",
                table: "sieve_answers",
                type: "bytea",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "bytea");

            migrationBuilder.AddColumn<string>(
                name: "cache_key",
                schema: "checks",
                table: "sieve_answers",
                type: "text",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "model",
                schema: "checks",
                table: "sieve_answers",
                type: "text",
                nullable: false);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "response",
                schema: "checks",
                table: "sieve_answers",
                type: "jsonb",
                nullable: false);

            migrationBuilder.AddPrimaryKey(
                name: "pk_sieve_answers",
                schema: "checks",
                table: "sieve_answers",
                columns: new[] { "tenant_id", "cache_key" });

            migrationBuilder.Sql(SqlResource.Read("F3/01_cli_tenant.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("F3/01_cli_tenant_down.sql"));

            migrationBuilder.DropPrimaryKey(
                name: "pk_sieve_answers",
                schema: "checks",
                table: "sieve_answers");

            migrationBuilder.DropColumn(
                name: "cache_key",
                schema: "checks",
                table: "sieve_answers");

            migrationBuilder.DropColumn(
                name: "model",
                schema: "checks",
                table: "sieve_answers");

            migrationBuilder.DropColumn(
                name: "response",
                schema: "checks",
                table: "sieve_answers");

            migrationBuilder.AlterColumn<byte[]>(
                name: "question_set_hash",
                schema: "checks",
                table: "sieve_answers",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldNullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "chunk_hash",
                schema: "checks",
                table: "sieve_answers",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<float[]>(
                name: "probabilities",
                schema: "checks",
                table: "sieve_answers",
                type: "real[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sieve_answers",
                schema: "checks",
                table: "sieve_answers",
                columns: new[] { "tenant_id", "question_set_hash", "chunk_hash" });
        }
    }
}
