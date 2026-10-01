using System;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EshopGuard.Data.Migrations
{
    /// <inheritdoc />
    public partial class F1DataModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ops");

            migrationBuilder.EnsureSchema(
                name: "shop");

            migrationBuilder.EnsureSchema(
                name: "fixes");

            migrationBuilder.EnsureSchema(
                name: "checks");

            migrationBuilder.EnsureSchema(
                name: "iam");

            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.EnsureSchema(
                name: "ref");

            migrationBuilder.EnsureSchema(
                name: "content");

            migrationBuilder.EnsureSchema(
                name: "usage");

            // ops.current_tenant_id() for the RLS policies (F1RowLevelSecurity).
            migrationBuilder.Sql(SqlResource.Read("F1/01_functions.sql"));

            migrationBuilder.CreateTable(
                name: "audit_log",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_kind = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    entity_type = table.Column<string>(type: "text", nullable: true),
                    entity_id = table.Column<string>(type: "text", nullable: true),
                    data = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    ip = table.Column<IPAddress>(type: "inet", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log", x => new { x.id, x.at });
                    table.CheckConstraint("ck_audit_log_actor_kind", "actor_kind IN ('user', 'system', 'admin')");
                });

            migrationBuilder.CreateTable(
                name: "domains",
                schema: "ops",
                columns: table => new
                {
                    domain = table.Column<string>(type: "text", nullable: false),
                    robots_txt = table.Column<string>(type: "text", nullable: true),
                    robots_fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    crawl_delay_ms = table.Column<int>(type: "integer", nullable: true),
                    sitemaps = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    lease_job_id = table.Column<long>(type: "bigint", nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_request_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rate = table.Column<double>(type: "double precision", nullable: true),
                    consecutive_errors = table.Column<int>(type: "integer", nullable: false),
                    blocked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_domains", x => x.domain);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    resource_class = table.Column<string>(type: "text", nullable: false),
                    priority = table.Column<short>(type: "smallint", nullable: false),
                    payload = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    dedupe_key = table.Column<string>(type: "text", nullable: true),
                    concurrency_key = table.Column<string>(type: "text", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    not_before = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    lease_owner = table.Column<string>(type: "text", nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    heartbeat_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jobs", x => x.id);
                    table.CheckConstraint("ck_jobs_priority", "priority BETWEEN 0 AND 4");
                    table.CheckConstraint("ck_jobs_resource_class", "resource_class IN ('fetch', 'cpu', 'jev', 'llm', 'io', 'system')");
                    table.CheckConstraint("ck_jobs_state", "state IN ('queued', 'running', 'succeeded', 'failed', 'canceled')");
                });

            migrationBuilder.CreateTable(
                name: "locales",
                schema: "ref",
                columns: table => new
                {
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    fallback_code = table.Column<string>(type: "text", nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("ak_locales_code", x => x.code);
                    table.ForeignKey(
                        name: "fk_locales_locales_fallback_code",
                        column: x => x.fallback_code,
                        principalSchema: "ref",
                        principalTable: "locales",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rate_limit_buckets",
                schema: "ops",
                columns: table => new
                {
                    key = table.Column<string>(type: "text", nullable: false),
                    capacity = table.Column<double>(type: "double precision", nullable: false),
                    tokens = table.Column<double>(type: "double precision", nullable: false),
                    refill_per_sec = table.Column<double>(type: "double precision", nullable: false),
                    reserved = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rate_limit_buckets", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "rule_sets",
                schema: "checks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    module = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<string>(type: "text", nullable: false),
                    jurisdictions = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    question_language = table.Column<string>(type: "text", nullable: false),
                    question_set_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    definition = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    texts = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    source_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rule_sets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stripe_events",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stripe_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                schema: "ops",
                columns: table => new
                {
                    key = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_settings", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "usage_daily",
                schema: "usage",
                columns: table => new
                {
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    operation = table.Column<string>(type: "text", nullable: false),
                    calls = table.Column<long>(type: "bigint", nullable: false),
                    cache_hits = table.Column<long>(type: "bigint", nullable: false),
                    input_tokens = table.Column<long>(type: "bigint", nullable: false),
                    cached_tokens = table.Column<long>(type: "bigint", nullable: false),
                    output_tokens = table.Column<long>(type: "bigint", nullable: false),
                    bytes_in = table.Column<long>(type: "bigint", nullable: false),
                    cost_usd = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usage_daily", x => new { x.day, x.tenant_id, x.shop_id, x.provider, x.operation });
                    table.CheckConstraint("ck_usage_daily_operation", "operation IN ('sentence_eval', 'sieve', 'profile', 'rewrite', 'recheck', 'fetch')");
                    table.CheckConstraint("ck_usage_daily_provider", "provider IN ('jev', 'openai', 'crawl')");
                });

            migrationBuilder.CreateTable(
                name: "usage_records",
                schema: "usage",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    job_id = table.Column<long>(type: "bigint", nullable: true),
                    provider = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "text", nullable: true),
                    operation = table.Column<string>(type: "text", nullable: false),
                    calls = table.Column<int>(type: "integer", nullable: false),
                    cache_hits = table.Column<int>(type: "integer", nullable: false),
                    input_tokens = table.Column<long>(type: "bigint", nullable: false),
                    cached_tokens = table.Column<long>(type: "bigint", nullable: false),
                    output_tokens = table.Column<long>(type: "bigint", nullable: false),
                    bytes_in = table.Column<long>(type: "bigint", nullable: false),
                    cost_usd = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usage_records", x => new { x.id, x.occurred_at });
                    table.CheckConstraint("ck_usage_records_operation", "operation IN ('sentence_eval', 'sieve', 'profile', 'rewrite', 'recheck', 'fetch')");
                    table.CheckConstraint("ck_usage_records_provider", "provider IN ('jev', 'openai', 'crawl')");
                });

            migrationBuilder.CreateTable(
                name: "workers",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<string>(type: "text", nullable: true),
                    slots = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    heartbeat_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    draining = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "iam",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    email = table.Column<string>(type: "text", nullable: false),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    locale = table.Column<string>(type: "text", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_locales_locale",
                        column: x => x.locale,
                        principalSchema: "ref",
                        principalTable: "locales",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evidence_items",
                schema: "fixes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    claim_text = table.Column<string>(type: "text", nullable: false),
                    subject_kind = table.Column<string>(type: "text", nullable: false),
                    subject_label = table.Column<string>(type: "text", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: true),
                    file_blob_key = table.Column<string>(type: "text", nullable: true),
                    file_name = table.Column<string>(type: "text", nullable: true),
                    source = table.Column<string>(type: "text", nullable: false),
                    registry_ref = table.Column<string>(type: "text", nullable: true),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valid_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    reminder_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidence_items", x => x.id);
                    table.UniqueConstraint("ak_evidence_items_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_evidence_items_kind", "kind IN ('certificate', 'license', 'test_report', 'statement', 'answer')");
                    table.CheckConstraint("ck_evidence_items_source", "source IN ('upload', 'registry', 'answer')");
                    table.CheckConstraint("ck_evidence_items_status", "status IN ('valid', 'expiring', 'expired', 'awaiting_answer', 'claim_removed')");
                    table.CheckConstraint("ck_evidence_items_subject_kind", "subject_kind IN ('brand', 'product', 'group')");
                    table.ForeignKey(
                        name: "fk_evidence_items_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_logins",
                schema: "iam",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_logins", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_logins_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_tokens",
                schema: "iam",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    email = table.Column<string>(type: "text", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    requested_ip_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_tokens", x => x.id);
                    table.CheckConstraint("ck_user_tokens_purpose", "purpose IN ('reset', 'confirm', 'magic_link')");
                    table.ForeignKey(
                        name: "fk_user_tokens_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "connector_events",
                schema: "shop",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connector_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dedupe_key = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    resource_type = table.Column<string>(type: "text", nullable: true),
                    resource_external_id = table.Column<string>(type: "text", nullable: true),
                    payload = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_connector_events", x => new { x.id, x.received_at });
                });

            migrationBuilder.CreateTable(
                name: "connector_webhooks",
                schema: "shop",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    connector_id = table.Column<Guid>(type: "uuid", nullable: false),
                    @event = table.Column<string>(name: "event", type: "text", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    last_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_connector_webhooks", x => x.id);
                    table.UniqueConstraint("ak_connector_webhooks_tenant_id_id", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "connectors",
                schema: "shop",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    external_shop_id = table.Column<string>(type: "text", nullable: true),
                    access = table.Column<string>(type: "text", nullable: false),
                    credentials_enc = table.Column<byte[]>(type: "bytea", nullable: true),
                    credentials_key_id = table.Column<string>(type: "text", nullable: true),
                    token_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    scopes = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    sync_cursor = table.Column<string>(type: "text", nullable: true),
                    last_reconcile_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_webhook_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    webhook_secret_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    health = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_connectors", x => x.id);
                    table.UniqueConstraint("ak_connectors_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_connectors_access", "access IN ('read', 'read_write')");
                    table.CheckConstraint("ck_connectors_platform", "platform IN ('shoptet', 'upgates', 'biznisweb', 'woocommerce', 'shopify', 'other', 'unknown')");
                    table.CheckConstraint("ck_connectors_status", "status IN ('connected', 'error', 'revoked', 'paused')");
                });

            migrationBuilder.CreateTable(
                name: "decision_memory",
                schema: "fixes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    segment_hash = table.Column<long>(type: "bigint", nullable: false),
                    normalized_text = table.Column<string>(type: "text", nullable: false),
                    decision = table.Column<string>(type: "text", nullable: false),
                    replacement_text = table.Column<string>(type: "text", nullable: true),
                    evidence_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_proposal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    auto_publish = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decision_memory", x => x.id);
                    table.UniqueConstraint("ak_decision_memory_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_decision_memory_decision", "decision IN ('replace', 'keep', 'keep_with_evidence', 'remove')");
                    table.ForeignKey(
                        name: "fk_decision_memory_evidence_items_tenant_id_evidence_id",
                        columns: x => new { x.tenant_id, x.evidence_id },
                        principalSchema: "fixes",
                        principalTable: "evidence_items",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_decision_memory_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evidence_links",
                schema: "fixes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_id = table.Column<Guid>(type: "uuid", nullable: true),
                    external_id = table.Column<string>(type: "text", nullable: true),
                    finding_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidence_links", x => x.id);
                    table.UniqueConstraint("ak_evidence_links_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_evidence_links_evidence_items_tenant_id_evidence_id",
                        columns: x => new { x.tenant_id, x.evidence_id },
                        principalSchema: "fixes",
                        principalTable: "evidence_items",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "feeds",
                schema: "shop",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    format = table.Column<string>(type: "text", nullable: false),
                    etag = table.Column<string>(type: "text", nullable: true),
                    last_fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feeds", x => x.id);
                    table.UniqueConstraint("ak_feeds_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_feeds_format", "format IN ('heureka', 'google')");
                });

            migrationBuilder.CreateTable(
                name: "finding_occurrences",
                schema: "checks",
                columns: table => new
                {
                    finding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    block_index = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_finding_occurrences", x => new { x.finding_id, x.page_id });
                });

            migrationBuilder.CreateTable(
                name: "findings",
                schema: "checks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_id = table.Column<string>(type: "text", nullable: false),
                    rule_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false),
                    checkability = table.Column<string>(type: "text", nullable: false),
                    severity = table.Column<string>(type: "text", nullable: false),
                    band = table.Column<string>(type: "text", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: false),
                    segment_hash = table.Column<long>(type: "bigint", nullable: true),
                    page_id = table.Column<Guid>(type: "uuid", nullable: true),
                    text = table.Column<string>(type: "text", nullable: true),
                    score = table.Column<float>(type: "real", nullable: true),
                    verdicts = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    legal_refs = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    @params = table.Column<JsonDocument>(name: "params", type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    occurrences = table.Column<int>(type: "integer", nullable: false),
                    first_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_seen_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_findings", x => x.id);
                    table.UniqueConstraint("ak_findings_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_findings_band", "band IN ('high', 'review')");
                    table.CheckConstraint("ck_findings_checkability", "checkability IN ('text', 'assess', 'verify')");
                    table.CheckConstraint("ck_findings_scope", "scope IN ('segment', 'page', 'site')");
                    table.CheckConstraint("ck_findings_status", "status IN ('open', 'needs_answer', 'proposed', 'approved', 'published', 'kept', 'kept_with_evidence', 'dismissed', 'resolved')");
                    table.ForeignKey(
                        name: "fk_findings_rule_sets_rule_set_id",
                        column: x => x.rule_set_id,
                        principalSchema: "checks",
                        principalTable: "rule_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fix_groups",
                schema: "fixes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    segment_hash = table.Column<long>(type: "bigint", nullable: true),
                    original_text = table.Column<string>(type: "text", nullable: true),
                    replacement_template = table.Column<string>(type: "text", nullable: true),
                    placeholders = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    filled_values = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    page_count = table.Column<int>(type: "integer", nullable: false),
                    excluded_page_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false, defaultValueSql: "'{}'"),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fix_groups", x => x.id);
                    table.UniqueConstraint("ak_fix_groups_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_fix_groups_kind", "kind IN ('repeated_text', 'template', 'site_obligation')");
                    table.CheckConstraint("ck_fix_groups_status", "status IN ('draft', 'needs_value', 'approved', 'published', 'partially_published', 'rejected')");
                    table.ForeignKey(
                        name: "fk_fix_groups_users_approved_by",
                        column: x => x.approved_by,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fix_proposals",
                schema: "fixes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    finding_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false, defaultValueSql: "'{}'"),
                    field = table.Column<string>(type: "text", nullable: false),
                    block_index = table.Column<int>(type: "integer", nullable: true),
                    original_text = table.Column<string>(type: "text", nullable: false),
                    proposed_text = table.Column<string>(type: "text", nullable: false),
                    alternatives = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    selected_alternative = table.Column<string>(type: "text", nullable: true),
                    edited_text = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    placeholders = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    recheck_status = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "text", nullable: true),
                    prompt_version = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fix_proposals", x => x.id);
                    table.UniqueConstraint("ak_fix_proposals_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_fix_proposals_field", "field IN ('description', 'short_description', 'name', 'block')");
                    table.CheckConstraint("ck_fix_proposals_recheck_status", "recheck_status IN ('ok', 'still_finding', 'pending')");
                    table.CheckConstraint("ck_fix_proposals_status", "status IN ('proposed', 'accepted', 'rejected', 'edited', 'published', 'conflict', 'superseded')");
                    table.ForeignKey(
                        name: "fk_fix_proposals_fix_groups_tenant_id_group_id",
                        columns: x => new { x.tenant_id, x.group_id },
                        principalSchema: "fixes",
                        principalTable: "fix_groups",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fix_proposals_users_decided_by",
                        column: x => x.decided_by,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "free_sample_claims",
                schema: "shop",
                columns: table => new
                {
                    domain = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_free_sample_claims", x => x.domain);
                });

            migrationBuilder.CreateTable(
                name: "invitations",
                schema: "iam",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    email = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    invited_by = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invitations", x => x.id);
                    table.UniqueConstraint("ak_invitations_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_invitations_role", "role IN ('owner', 'admin', 'editor', 'viewer')");
                    table.ForeignKey(
                        name: "fk_invitations_users_invited_by",
                        column: x => x.invited_by,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    number = table.Column<string>(type: "text", nullable: true),
                    superfaktura_id = table.Column<string>(type: "text", nullable: true),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    taxable_supply_date = table.Column<DateOnly>(type: "date", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    buyer = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    items = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    amount_net = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    amount_gross = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", nullable: false),
                    reverse_charge = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    einvoice_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    einvoice_status = table.Column<string>(type: "text", nullable: false),
                    einvoice_message_id = table.Column<string>(type: "text", nullable: true),
                    pdf_blob_key = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    credit_note_for = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoices", x => x.id);
                    table.UniqueConstraint("ak_invoices_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_invoices_einvoice_status", "einvoice_status IN ('not_required', 'queued', 'sent', 'delivered', 'failed')");
                    table.CheckConstraint("ck_invoices_kind", "kind IN ('invoice', 'credit_note', 'proforma')");
                    table.ForeignKey(
                        name: "fk_invoices_invoices_tenant_id_credit_note_for",
                        columns: x => new { x.tenant_id, x.credit_note_for },
                        principalSchema: "billing",
                        principalTable: "invoices",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "jev_answers",
                schema: "checks",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cache_key = table.Column<string>(type: "text", nullable: false),
                    response = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    question_set_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jev_answers", x => new { x.tenant_id, x.cache_key });
                });

            migrationBuilder.CreateTable(
                name: "markets",
                schema: "ref",
                columns: table => new
                {
                    code = table.Column<string>(type: "text", nullable: false),
                    country_code = table.Column<string>(type: "text", nullable: false),
                    default_locale = table.Column<string>(type: "text", nullable: false),
                    ui_locales = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    jurisdiction = table.Column<string>(type: "text", nullable: false),
                    currency = table.Column<string>(type: "character(3)", nullable: false),
                    price_list_id = table.Column<Guid>(type: "uuid", nullable: true),
                    web_status = table.Column<string>(type: "text", nullable: false),
                    checks_status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("ak_markets_code", x => x.code);
                    table.CheckConstraint("ck_markets_checks_status", "checks_status IN ('none', 'limited', 'full')");
                    table.CheckConstraint("ck_markets_web_status", "web_status IN ('hidden', 'preview', 'live')");
                });

            migrationBuilder.CreateTable(
                name: "price_lists",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    name = table.Column<string>(type: "text", nullable: false),
                    market_code = table.Column<string>(type: "text", nullable: false),
                    currency = table.Column<string>(type: "character(3)", nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    notice_days = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_lists", x => x.id);
                    table.CheckConstraint("ck_price_lists_status", "status IN ('draft', 'published', 'retired')");
                    table.ForeignKey(
                        name: "fk_price_lists_markets_market_code",
                        column: x => x.market_code,
                        principalSchema: "ref",
                        principalTable: "markets",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "iam",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    name = table.Column<string>(type: "text", nullable: false),
                    legal_name = table.Column<string>(type: "text", nullable: true),
                    ico = table.Column<string>(type: "text", nullable: true),
                    dic = table.Column<string>(type: "text", nullable: true),
                    ic_dph = table.Column<string>(type: "text", nullable: true),
                    street = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: true),
                    postal_code = table.Column<string>(type: "text", nullable: true),
                    country_code = table.Column<string>(type: "text", nullable: false),
                    billing_email = table.Column<string>(type: "text", nullable: true),
                    locale = table.Column<string>(type: "text", nullable: false),
                    currency = table.Column<string>(type: "character(3)", nullable: true),
                    market_code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    stripe_customer_id = table.Column<string>(type: "text", nullable: true),
                    superfaktura_client_id = table.Column<string>(type: "text", nullable: true),
                    partner_kind = table.Column<string>(type: "text", nullable: true),
                    founder_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                    table.CheckConstraint("ck_tenants_partner_kind", "partner_kind IN ('agency', 'lawyer', 'certifier')");
                    table.CheckConstraint("ck_tenants_status", "status IN ('active', 'suspended', 'deleted')");
                    table.ForeignKey(
                        name: "fk_tenants_locales_locale",
                        column: x => x.locale,
                        principalSchema: "ref",
                        principalTable: "locales",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenants_markets_market_code",
                        column: x => x.market_code,
                        principalSchema: "ref",
                        principalTable: "markets",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "price_tiers",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    price_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    min_products = table.Column<int>(type: "integer", nullable: false),
                    max_products = table.Column<int>(type: "integer", nullable: true),
                    analysis_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    monitoring_monthly = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    monitoring_yearly = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    stripe_price_analysis = table.Column<string>(type: "text", nullable: true),
                    stripe_price_monthly = table.Column<string>(type: "text", nullable: true),
                    stripe_price_yearly = table.Column<string>(type: "text", nullable: true),
                    lookup_key = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_tiers", x => x.id);
                    table.ForeignKey(
                        name: "fk_price_tiers_price_lists_price_list_id",
                        column: x => x.price_list_id,
                        principalSchema: "billing",
                        principalTable: "price_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "volume_discounts",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    price_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_shop_number = table.Column<int>(type: "integer", nullable: false),
                    percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    stripe_coupon_id = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_volume_discounts", x => x.id);
                    table.ForeignKey(
                        name: "fk_volume_discounts_price_lists_price_list_id",
                        column: x => x.price_list_id,
                        principalSchema: "billing",
                        principalTable: "price_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "memberships",
                schema: "iam",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    invited_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_memberships", x => new { x.tenant_id, x.user_id });
                    table.CheckConstraint("ck_memberships_role", "role IN ('owner', 'admin', 'editor', 'viewer')");
                    table.ForeignKey(
                        name: "fk_memberships_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "iam",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_memberships_users_invited_by",
                        column: x => x.invited_by,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_memberships_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "outbox",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox", x => x.id);
                    table.CheckConstraint("ck_outbox_kind", "kind IN ('email', 'invoice', 'einvoice')");
                    table.ForeignKey(
                        name: "fk_outbox_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "iam",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_methods",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    stripe_payment_method_id = table.Column<string>(type: "text", nullable: false),
                    brand = table.Column<string>(type: "text", nullable: true),
                    last4 = table.Column<string>(type: "text", nullable: true),
                    exp_month = table.Column<int>(type: "integer", nullable: true),
                    exp_year = table.Column<int>(type: "integer", nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    detached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_methods", x => x.id);
                    table.UniqueConstraint("ak_payment_methods_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_payment_methods_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "iam",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "promo_codes",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    code = table.Column<string>(type: "text", nullable: false),
                    percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    duration_months = table.Column<int>(type: "integer", nullable: true),
                    valid_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    max_redemptions = table.Column<int>(type: "integer", nullable: true),
                    stripe_promotion_code_id = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promo_codes", x => x.id);
                    table.ForeignKey(
                        name: "fk_promo_codes_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "iam",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rewrite_cache",
                schema: "fixes",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    answer = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rewrite_cache", x => new { x.tenant_id, x.key });
                    table.ForeignKey(
                        name: "fk_rewrite_cache_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "iam",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sieve_answers",
                schema: "checks",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_set_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    chunk_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    probabilities = table.Column<float[]>(type: "real[]", nullable: false, defaultValueSql: "'{}'"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sieve_answers", x => new { x.tenant_id, x.question_set_hash, x.chunk_hash });
                    table.ForeignKey(
                        name: "fk_sieve_answers_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "iam",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_settings",
                schema: "iam",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    email_new_violation = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    email_weekly_summary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    email_run_finished = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_settings_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "iam",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    @params = table.Column<JsonDocument>(name: "params", type: "jsonb", nullable: true),
                    link = table.Column<string>(type: "text", nullable: true),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    price_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier_code = table.Column<string>(type: "text", nullable: true),
                    amount_net = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    amount_gross = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    stripe_checkout_session_id = table.Column<string>(type: "text", nullable: true),
                    stripe_payment_intent_id = table.Column<string>(type: "text", nullable: true),
                    run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.id);
                    table.UniqueConstraint("ak_orders_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_orders_kind", "kind IN ('analysis_with_trial', 'custom')");
                    table.CheckConstraint("ck_orders_status", "status IN ('created', 'checkout_open', 'paid', 'expired', 'canceled', 'refunded')");
                    table.ForeignKey(
                        name: "fk_orders_price_lists_price_list_id",
                        column: x => x.price_list_id,
                        principalSchema: "billing",
                        principalTable: "price_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orders_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "page_changes",
                schema: "checks",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_id = table.Column<Guid>(type: "uuid", nullable: false),
                    detected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    change_kind = table.Column<string>(type: "text", nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    result = table.Column<string>(type: "text", nullable: true),
                    finding_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false, defaultValueSql: "'{}'"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_page_changes", x => x.id);
                    table.CheckConstraint("ck_page_changes_change_kind", "change_kind IN ('new', 'text_changed', 'removed', 'hidden_saved', 'fix_published')");
                    table.CheckConstraint("ck_page_changes_result", "result IN ('new_violation', 'new_assess', 'fix_confirmed', 'ok')");
                    table.CheckConstraint("ck_page_changes_source", "source IN ('webhook', 'crawl', 'feed', 'save_hidden')");
                });

            migrationBuilder.CreateTable(
                name: "page_profiles",
                schema: "shop",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    prompt_version = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    regions = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    sample_urls = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    created_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_page_profiles", x => x.id);
                    table.UniqueConstraint("ak_page_profiles_tenant_id_id", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "page_versions",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    http_status = table.Column<int>(type: "integer", nullable: true),
                    html_blob_key = table.Column<string>(type: "text", nullable: true),
                    extract_blob_key = table.Column<string>(type: "text", nullable: true),
                    text_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    visible_chars = table.Column<int>(type: "integer", nullable: true),
                    checked_chars = table.Column<int>(type: "integer", nullable: true),
                    navigation_chars = table.Column<int>(type: "integer", nullable: true),
                    listing_chars = table.Column<int>(type: "integer", nullable: true),
                    profile_skipped_chars = table.Column<int>(type: "integer", nullable: true),
                    extraction_method = table.Column<string>(type: "text", nullable: true),
                    script_app = table.Column<string>(type: "text", nullable: true),
                    text_not_loaded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    segment_hashes = table.Column<long[]>(type: "bigint[]", nullable: false, defaultValueSql: "'{}'"),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_page_versions", x => new { x.shop_id, x.id });
                    table.UniqueConstraint("ak_page_versions_tenant_id_shop_id_id", x => new { x.tenant_id, x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "pages",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    url_hash = table.Column<long>(type: "bigint", nullable: false),
                    path = table.Column<string>(type: "text", nullable: true),
                    language = table.Column<string>(type: "text", nullable: true),
                    hreflang_group = table.Column<string>(type: "text", nullable: true),
                    page_type = table.Column<string>(type: "text", nullable: true),
                    source = table.Column<string>(type: "text", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    is_hidden_in_shop = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    http_etag = table.Column<string>(type: "text", nullable: true),
                    http_last_modified = table.Column<string>(type: "text", nullable: true),
                    current_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    profile_unknown_share = table.Column<float>(type: "real", nullable: true),
                    rotation_bucket = table.Column<short>(type: "smallint", nullable: false),
                    next_check_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pages", x => new { x.shop_id, x.id });
                    table.UniqueConstraint("ak_pages_tenant_id_shop_id_id", x => new { x.tenant_id, x.shop_id, x.id });
                    table.CheckConstraint("ck_pages_page_type", "page_type IN ('home', 'product', 'category', 'legal', 'content')");
                    table.CheckConstraint("ck_pages_rotation_bucket", "rotation_bucket BETWEEN 0 AND 6");
                    table.CheckConstraint("ck_pages_source", "source IN ('crawl', 'connector', 'feed')");
                    table.CheckConstraint("ck_pages_status", "status IN ('active', 'gone', 'robots_blocked', 'not_loaded', 'error')");
                    table.ForeignKey(
                        name: "fk_pages_page_profiles_tenant_id_profile_id",
                        columns: x => new { x.tenant_id, x.profile_id },
                        principalSchema: "shop",
                        principalTable: "page_profiles",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pages_page_versions_tenant_id_shop_id_current_version_id",
                        columns: x => new { x.tenant_id, x.shop_id, x.current_version_id },
                        principalSchema: "content",
                        principalTable: "page_versions",
                        principalColumns: new[] { "tenant_id", "shop_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stripe_invoice_id = table.Column<string>(type: "text", nullable: true),
                    stripe_payment_intent_id = table.Column<string>(type: "text", nullable: true),
                    amount_gross = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    failure_code = table.Column<string>(type: "text", nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    refunded_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    card_brand = table.Column<string>(type: "text", nullable: true),
                    card_last4 = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.UniqueConstraint("ak_payments_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_payments_status", "status IN ('succeeded', 'failed', 'refunded', 'partially_refunded')");
                    table.ForeignKey(
                        name: "fk_payments_orders_tenant_id_order_id",
                        columns: x => new { x.tenant_id, x.order_id },
                        principalSchema: "billing",
                        principalTable: "orders",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "protocols",
                schema: "fixes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "text", nullable: false),
                    period_from = table.Column<DateOnly>(type: "date", nullable: false),
                    period_to = table.Column<DateOnly>(type: "date", nullable: false),
                    locale = table.Column<string>(type: "text", nullable: false),
                    generated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    pdf_blob_key = table.Column<string>(type: "text", nullable: true),
                    summary = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    rule_set_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false, defaultValueSql: "'{}'"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_protocols", x => x.id);
                    table.UniqueConstraint("ak_protocols_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_protocols_users_generated_by",
                        column: x => x.generated_by,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "publications",
                schema: "fixes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connector_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: true),
                    field = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<string>(type: "text", nullable: true),
                    old_value = table.Column<string>(type: "text", nullable: true),
                    old_value_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: false),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rolled_back_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fix_proposal_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false, defaultValueSql: "'{}'"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_publications", x => x.id);
                    table.UniqueConstraint("ak_publications_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_publications_status", "status IN ('queued', 'published', 'failed', 'conflict', 'rolled_back')");
                    table.ForeignKey(
                        name: "fk_publications_connectors_tenant_id_connector_id",
                        columns: x => new { x.tenant_id, x.connector_id },
                        principalSchema: "shop",
                        principalTable: "connectors",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_publications_pages_tenant_id_shop_id_page_id",
                        columns: x => new { x.tenant_id, x.shop_id, x.page_id },
                        principalSchema: "content",
                        principalTable: "pages",
                        principalColumns: new[] { "tenant_id", "shop_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_publications_users_requested_by",
                        column: x => x.requested_by,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                schema: "checks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finding_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scope = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    @params = table.Column<JsonDocument>(name: "params", type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    answer = table.Column<string>(type: "text", nullable: true),
                    answered_by = table.Column<Guid>(type: "uuid", nullable: true),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    evidence_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_questions", x => x.id);
                    table.UniqueConstraint("ak_questions_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_questions_scope", "scope IN ('finding', 'site')");
                    table.CheckConstraint("ck_questions_status", "status IN ('open', 'answered')");
                    table.ForeignKey(
                        name: "fk_questions_evidence_items_tenant_id_evidence_id",
                        columns: x => new { x.tenant_id, x.evidence_id },
                        principalSchema: "fixes",
                        principalTable: "evidence_items",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_questions_findings_tenant_id_finding_id",
                        columns: x => new { x.tenant_id, x.finding_id },
                        principalSchema: "checks",
                        principalTable: "findings",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_questions_users_answered_by",
                        column: x => x.answered_by,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "run_events",
                schema: "checks",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: true),
                    data = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_run_events", x => new { x.id, x.at });
                });

            migrationBuilder.CreateTable(
                name: "runs",
                schema: "checks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    trigger = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    priority = table.Column<short>(type: "smallint", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: true),
                    jurisdictions = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    modules = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    rule_set_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false, defaultValueSql: "'{}'"),
                    estimate = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    progress = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    stats = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    cancel_requested = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_runs", x => x.id);
                    table.UniqueConstraint("ak_runs_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_runs_kind", "kind IN ('free_sample', 'full_analysis', 'monitoring', 'connector_check', 'recheck', 'rule_update', 'profile_refresh')");
                    table.CheckConstraint("ck_runs_status", "status IN ('queued', 'discovering', 'awaiting_payment', 'crawling', 'profiling', 'segmenting', 'evaluating', 'ruling', 'rewriting', 'finished', 'partial', 'failed', 'canceled')");
                    table.CheckConstraint("ck_runs_trigger", "trigger IN ('user', 'schedule', 'webhook', 'system')");
                    table.ForeignKey(
                        name: "fk_runs_orders_tenant_id_order_id",
                        columns: x => new { x.tenant_id, x.order_id },
                        principalSchema: "billing",
                        principalTable: "orders",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_runs_users_requested_by",
                        column: x => x.requested_by,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shops",
                schema: "shop",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    domain = table.Column<string>(type: "text", nullable: false),
                    base_url = table.Column<string>(type: "text", nullable: false),
                    base_path = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: true),
                    home_country = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<string>(type: "text", nullable: true),
                    platform = table.Column<string>(type: "text", nullable: false),
                    source_mode = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    product_count = table.Column<int>(type: "integer", nullable: true),
                    page_count = table.Column<int>(type: "integer", nullable: true),
                    tier_code = table.Column<string>(type: "text", nullable: true),
                    modules = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    check_hidden_on_save = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ownership_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verification_method = table.Column<string>(type: "text", nullable: true),
                    monitor_slot_minute = table.Column<int>(type: "integer", nullable: true),
                    last_full_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shops", x => x.id);
                    table.UniqueConstraint("ak_shops_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_shops_platform", "platform IN ('shoptet', 'upgates', 'biznisweb', 'woocommerce', 'shopify', 'other', 'unknown')");
                    table.CheckConstraint("ck_shops_source_mode", "source_mode IN ('connector', 'feed', 'web')");
                    table.CheckConstraint("ck_shops_status", "status IN ('draft', 'sample', 'awaiting_payment', 'analyzing', 'active', 'paused', 'canceled')");
                    table.CheckConstraint("ck_shops_verification_method", "verification_method IN ('meta', 'dns', 'connector')");
                    table.ForeignKey(
                        name: "fk_shops_runs_tenant_id_last_full_run_id",
                        columns: x => new { x.tenant_id, x.last_full_run_id },
                        principalSchema: "checks",
                        principalTable: "runs",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_shops_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "iam",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "schedules",
                schema: "ops",
                columns: table => new
                {
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    next_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schedules", x => new { x.shop_id, x.kind });
                    table.CheckConstraint("ck_schedules_kind", "kind IN ('nightly', 'weekly_web', 'reconcile')");
                    table.ForeignKey(
                        name: "fk_schedules_shops_tenant_id_shop_id",
                        columns: x => new { x.tenant_id, x.shop_id },
                        principalSchema: "shop",
                        principalTable: "shops",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shop_facts",
                schema: "shop",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    topic = table.Column<string>(type: "text", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_facts", x => x.id);
                    table.UniqueConstraint("ak_shop_facts_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_shop_facts_shops_tenant_id_shop_id",
                        columns: x => new { x.tenant_id, x.shop_id },
                        principalSchema: "shop",
                        principalTable: "shops",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_shop_facts_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shop_languages",
                schema: "shop",
                columns: table => new
                {
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_url = table.Column<string>(type: "text", nullable: false),
                    switch_method = table.Column<string>(type: "text", nullable: true),
                    source = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    own_text_share = table.Column<float>(type: "real", nullable: true),
                    language_share = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    comparison = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    sample_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    counted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    product_count = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_languages", x => new { x.shop_id, x.language });
                    table.CheckConstraint("ck_shop_languages_source", "source IN ('hreflang', 'switcher', 'connector', 'llm', 'user')");
                    table.CheckConstraint("ck_shop_languages_status", "status IN ('active', 'excluded', 'needs_confirmation', 'unsupported')");
                    table.CheckConstraint("ck_shop_languages_switch_method", "switch_method IN ('path', 'subdomain', 'domain', 'query', 'cookie')");
                    table.ForeignKey(
                        name: "fk_shop_languages_runs_tenant_id_sample_run_id",
                        columns: x => new { x.tenant_id, x.sample_run_id },
                        principalSchema: "checks",
                        principalTable: "runs",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_shop_languages_shops_tenant_id_shop_id",
                        columns: x => new { x.tenant_id, x.shop_id },
                        principalSchema: "shop",
                        principalTable: "shops",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shop_markets",
                schema: "shop",
                columns: table => new
                {
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_home = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    evidence_level = table.Column<string>(type: "text", nullable: true),
                    source = table.Column<string>(type: "text", nullable: false),
                    evidence = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    detection_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_markets", x => new { x.shop_id, x.country_code });
                    table.CheckConstraint("ck_shop_markets_evidence_level", "evidence_level IN ('strong', 'delivery', 'generic')");
                    table.CheckConstraint("ck_shop_markets_source", "source IN ('detected', 'user')");
                    table.CheckConstraint("ck_shop_markets_status", "status IN ('suggested', 'active', 'declined', 'unsupported')");
                    table.ForeignKey(
                        name: "fk_shop_markets_runs_tenant_id_detection_run_id",
                        columns: x => new { x.tenant_id, x.detection_run_id },
                        principalSchema: "checks",
                        principalTable: "runs",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_shop_markets_shops_tenant_id_shop_id",
                        columns: x => new { x.tenant_id, x.shop_id },
                        principalSchema: "shop",
                        principalTable: "shops",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_shop_markets_users_confirmed_by",
                        column: x => x.confirmed_by,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shop_verifications",
                schema: "shop",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<string>(type: "text", nullable: false),
                    token = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_verifications", x => x.id);
                    table.UniqueConstraint("ak_shop_verifications_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_shop_verifications_method", "method IN ('meta', 'dns', 'connector')");
                    table.ForeignKey(
                        name: "fk_shop_verifications_shops_tenant_id_shop_id",
                        columns: x => new { x.tenant_id, x.shop_id },
                        principalSchema: "shop",
                        principalTable: "shops",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stripe_subscription_id = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    interval = table.Column<string>(type: "text", nullable: false),
                    price_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier_code = table.Column<string>(type: "text", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    discount_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    trial_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    current_period_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    current_period_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancel_at_period_end = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    canceled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscriptions", x => x.id);
                    table.UniqueConstraint("ak_subscriptions_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_subscriptions_interval", "interval IN ('month', 'year')");
                    table.CheckConstraint("ck_subscriptions_status", "status IN ('trialing', 'active', 'past_due', 'canceled', 'incomplete', 'paused')");
                    table.ForeignKey(
                        name: "fk_subscriptions_price_lists_price_list_id",
                        column: x => x.price_list_id,
                        principalSchema: "billing",
                        principalTable: "price_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subscriptions_shops_tenant_id_shop_id",
                        columns: x => new { x.tenant_id, x.shop_id },
                        principalSchema: "shop",
                        principalTable: "shops",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscription_changes",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    from = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    to = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    effective_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    notified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_changes", x => x.id);
                    table.UniqueConstraint("ak_subscription_changes_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_subscription_changes_kind", "kind IN ('price_list', 'tier', 'discount', 'interval', 'cancel')");
                    table.ForeignKey(
                        name: "fk_subscription_changes_subscriptions_tenant_id_subscription_id",
                        columns: x => new { x.tenant_id, x.subscription_id },
                        principalSchema: "billing",
                        principalTable: "subscriptions",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "ref",
                table: "locales",
                columns: new[] { "code", "created_at", "fallback_code", "name", "updated_at" },
                values: new object[,]
                {
                    { "cs", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Čeština", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "sk", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Slovenčina", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.InsertData(
                schema: "ref",
                table: "markets",
                columns: new[] { "code", "checks_status", "country_code", "created_at", "currency", "default_locale", "jurisdiction", "price_list_id", "ui_locales", "updated_at", "web_status" },
                values: new object[,]
                {
                    { "cz", "limited", "CZ", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "CZK", "cs-CZ", "cz", null, new[] { "cs" }, new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "hidden" },
                    { "sk", "full", "SK", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "EUR", "sk-SK", "sk", null, new[] { "sk" }, new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "hidden" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_tenant_id_at",
                schema: "ops",
                table: "audit_log",
                columns: new[] { "tenant_id", "at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_connector_events_connector_id_dedupe_key",
                schema: "shop",
                table: "connector_events",
                columns: new[] { "connector_id", "dedupe_key" });

            migrationBuilder.CreateIndex(
                name: "ix_connector_events_tenant_id_connector_id",
                schema: "shop",
                table: "connector_events",
                columns: new[] { "tenant_id", "connector_id" });

            migrationBuilder.CreateIndex(
                name: "ix_connector_events_tenant_id_shop_id",
                schema: "shop",
                table: "connector_events",
                columns: new[] { "tenant_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_connector_webhooks_tenant_id_connector_id",
                schema: "shop",
                table: "connector_webhooks",
                columns: new[] { "tenant_id", "connector_id" });

            migrationBuilder.CreateIndex(
                name: "ix_connectors_platform_external_shop_id",
                schema: "shop",
                table: "connectors",
                columns: new[] { "platform", "external_shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_connectors_tenant_id_shop_id",
                schema: "shop",
                table: "connectors",
                columns: new[] { "tenant_id", "shop_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_decision_memory_created_by",
                schema: "fixes",
                table: "decision_memory",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_decision_memory_tenant_id_evidence_id",
                schema: "fixes",
                table: "decision_memory",
                columns: new[] { "tenant_id", "evidence_id" });

            migrationBuilder.CreateIndex(
                name: "ix_decision_memory_tenant_id_shop_id_segment_hash",
                schema: "fixes",
                table: "decision_memory",
                columns: new[] { "tenant_id", "shop_id", "segment_hash" },
                filter: "superseded_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_decision_memory_tenant_id_source_proposal_id",
                schema: "fixes",
                table: "decision_memory",
                columns: new[] { "tenant_id", "source_proposal_id" });

            migrationBuilder.CreateIndex(
                name: "ix_evidence_items_created_by",
                schema: "fixes",
                table: "evidence_items",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_items_tenant_id_status",
                schema: "fixes",
                table: "evidence_items",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_evidence_links_tenant_id_evidence_id",
                schema: "fixes",
                table: "evidence_links",
                columns: new[] { "tenant_id", "evidence_id" });

            migrationBuilder.CreateIndex(
                name: "ix_evidence_links_tenant_id_finding_id",
                schema: "fixes",
                table: "evidence_links",
                columns: new[] { "tenant_id", "finding_id" });

            migrationBuilder.CreateIndex(
                name: "ix_evidence_links_tenant_id_shop_id_page_id",
                schema: "fixes",
                table: "evidence_links",
                columns: new[] { "tenant_id", "shop_id", "page_id" });

            migrationBuilder.CreateIndex(
                name: "ix_feeds_tenant_id_shop_id",
                schema: "shop",
                table: "feeds",
                columns: new[] { "tenant_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_finding_occurrences_shop_id_page_id",
                schema: "checks",
                table: "finding_occurrences",
                columns: new[] { "shop_id", "page_id" });

            migrationBuilder.CreateIndex(
                name: "ix_finding_occurrences_tenant_id_finding_id",
                schema: "checks",
                table: "finding_occurrences",
                columns: new[] { "tenant_id", "finding_id" });

            migrationBuilder.CreateIndex(
                name: "ix_finding_occurrences_tenant_id_shop_id_page_id",
                schema: "checks",
                table: "finding_occurrences",
                columns: new[] { "tenant_id", "shop_id", "page_id" });

            migrationBuilder.CreateIndex(
                name: "ix_findings_rule_set_id",
                schema: "checks",
                table: "findings",
                column: "rule_set_id");

            migrationBuilder.CreateIndex(
                name: "ix_findings_shop_id_rule_id_segment_hash",
                schema: "checks",
                table: "findings",
                columns: new[] { "shop_id", "rule_id", "segment_hash" },
                unique: true,
                filter: "scope = 'segment'");

            migrationBuilder.CreateIndex(
                name: "ix_findings_tenant_id_first_run_id",
                schema: "checks",
                table: "findings",
                columns: new[] { "tenant_id", "first_run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_findings_tenant_id_last_seen_run_id",
                schema: "checks",
                table: "findings",
                columns: new[] { "tenant_id", "last_seen_run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_findings_tenant_id_resolved_run_id",
                schema: "checks",
                table: "findings",
                columns: new[] { "tenant_id", "resolved_run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_findings_tenant_id_shop_id_page_id",
                schema: "checks",
                table: "findings",
                columns: new[] { "tenant_id", "shop_id", "page_id" });

            migrationBuilder.CreateIndex(
                name: "ix_findings_tenant_id_shop_id_status",
                schema: "checks",
                table: "findings",
                columns: new[] { "tenant_id", "shop_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_fix_groups_approved_by",
                schema: "fixes",
                table: "fix_groups",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "ix_fix_groups_tenant_id_shop_id_status",
                schema: "fixes",
                table: "fix_groups",
                columns: new[] { "tenant_id", "shop_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_fix_proposals_decided_by",
                schema: "fixes",
                table: "fix_proposals",
                column: "decided_by");

            migrationBuilder.CreateIndex(
                name: "ix_fix_proposals_tenant_id_created_run_id",
                schema: "fixes",
                table: "fix_proposals",
                columns: new[] { "tenant_id", "created_run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_fix_proposals_tenant_id_group_id",
                schema: "fixes",
                table: "fix_proposals",
                columns: new[] { "tenant_id", "group_id" });

            migrationBuilder.CreateIndex(
                name: "ix_fix_proposals_tenant_id_shop_id_page_id",
                schema: "fixes",
                table: "fix_proposals",
                columns: new[] { "tenant_id", "shop_id", "page_id" });

            migrationBuilder.CreateIndex(
                name: "ix_fix_proposals_tenant_id_shop_id_page_version_id",
                schema: "fixes",
                table: "fix_proposals",
                columns: new[] { "tenant_id", "shop_id", "page_version_id" });

            migrationBuilder.CreateIndex(
                name: "ix_fix_proposals_tenant_id_shop_id_status",
                schema: "fixes",
                table: "fix_proposals",
                columns: new[] { "tenant_id", "shop_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_free_sample_claims_tenant_id_shop_id",
                schema: "shop",
                table: "free_sample_claims",
                columns: new[] { "tenant_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_invitations_invited_by",
                schema: "iam",
                table: "invitations",
                column: "invited_by");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_tenant_id_email",
                schema: "iam",
                table: "invitations",
                columns: new[] { "tenant_id", "email" });

            migrationBuilder.CreateIndex(
                name: "ix_invitations_token_hash",
                schema: "iam",
                table: "invitations",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invoices_tenant_id_credit_note_for",
                schema: "billing",
                table: "invoices",
                columns: new[] { "tenant_id", "credit_note_for" });

            migrationBuilder.CreateIndex(
                name: "ix_invoices_tenant_id_issued_at",
                schema: "billing",
                table: "invoices",
                columns: new[] { "tenant_id", "issued_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_invoices_tenant_id_payment_id",
                schema: "billing",
                table: "invoices",
                columns: new[] { "tenant_id", "payment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_invoices_tenant_id_shop_id",
                schema: "billing",
                table: "invoices",
                columns: new[] { "tenant_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_dedupe_key",
                schema: "ops",
                table: "jobs",
                column: "dedupe_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_jobs_lease_until",
                schema: "ops",
                table: "jobs",
                column: "lease_until",
                filter: "state = 'running'");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_resource_class_priority_not_before_id",
                schema: "ops",
                table: "jobs",
                columns: new[] { "resource_class", "priority", "not_before", "id" },
                filter: "state = 'queued'");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_run_id",
                schema: "ops",
                table: "jobs",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ix_locales_fallback_code",
                schema: "ref",
                table: "locales",
                column: "fallback_code");

            migrationBuilder.CreateIndex(
                name: "ix_markets_price_list_id",
                schema: "ref",
                table: "markets",
                column: "price_list_id");

            migrationBuilder.CreateIndex(
                name: "ix_memberships_invited_by",
                schema: "iam",
                table: "memberships",
                column: "invited_by");

            migrationBuilder.CreateIndex(
                name: "ix_memberships_user_id",
                schema: "iam",
                table: "memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_settings_tenant_id_shop_id",
                schema: "iam",
                table: "notification_settings",
                columns: new[] { "tenant_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_settings_tenant_id_user_id_shop_id",
                schema: "iam",
                table: "notification_settings",
                columns: new[] { "tenant_id", "user_id", "shop_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_notification_settings_user_id",
                schema: "iam",
                table: "notification_settings",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_tenant_id_shop_id",
                schema: "iam",
                table: "notifications",
                columns: new[] { "tenant_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_tenant_id_user_id_read_at",
                schema: "iam",
                table: "notifications",
                columns: new[] { "tenant_id", "user_id", "read_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id",
                schema: "iam",
                table: "notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_created_by",
                schema: "billing",
                table: "orders",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_orders_price_list_id",
                schema: "billing",
                table: "orders",
                column: "price_list_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_stripe_checkout_session_id",
                schema: "billing",
                table: "orders",
                column: "stripe_checkout_session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_run_id",
                schema: "billing",
                table: "orders",
                columns: new[] { "tenant_id", "run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_shop_id",
                schema: "billing",
                table: "orders",
                columns: new[] { "tenant_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_created_at",
                schema: "ops",
                table: "outbox",
                column: "created_at",
                filter: "sent_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_tenant_id",
                schema: "ops",
                table: "outbox",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_page_changes_tenant_id_run_id",
                schema: "checks",
                table: "page_changes",
                columns: new[] { "tenant_id", "run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_page_changes_tenant_id_shop_id_detected_at",
                schema: "checks",
                table: "page_changes",
                columns: new[] { "tenant_id", "shop_id", "detected_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_page_changes_tenant_id_shop_id_page_id",
                schema: "checks",
                table: "page_changes",
                columns: new[] { "tenant_id", "shop_id", "page_id" });

            migrationBuilder.CreateIndex(
                name: "ix_page_profiles_shop_id_number",
                schema: "shop",
                table: "page_profiles",
                columns: new[] { "shop_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_page_profiles_tenant_id_created_run_id",
                schema: "shop",
                table: "page_profiles",
                columns: new[] { "tenant_id", "created_run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_page_profiles_tenant_id_shop_id",
                schema: "shop",
                table: "page_profiles",
                columns: new[] { "tenant_id", "shop_id" });


            migrationBuilder.CreateIndex(
                name: "ix_page_versions_shop_id_page_id",
                schema: "content",
                table: "page_versions",
                columns: new[] { "shop_id", "page_id" },
                unique: true,
                filter: "is_current");

            migrationBuilder.CreateIndex(
                name: "ix_page_versions_shop_id_page_id_fetched_at",
                schema: "content",
                table: "page_versions",
                columns: new[] { "shop_id", "page_id", "fetched_at" });

            migrationBuilder.CreateIndex(
                name: "ix_page_versions_tenant_id_run_id",
                schema: "content",
                table: "page_versions",
                columns: new[] { "tenant_id", "run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_page_versions_tenant_id_shop_id_page_id",
                schema: "content",
                table: "page_versions",
                columns: new[] { "tenant_id", "shop_id", "page_id" });

            migrationBuilder.CreateIndex(
                name: "ix_pages_shop_id_next_check_at",
                schema: "content",
                table: "pages",
                columns: new[] { "shop_id", "next_check_at" });

            migrationBuilder.CreateIndex(
                name: "ix_pages_shop_id_url_hash",
                schema: "content",
                table: "pages",
                columns: new[] { "shop_id", "url_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pages_tenant_id_profile_id",
                schema: "content",
                table: "pages",
                columns: new[] { "tenant_id", "profile_id" });

            migrationBuilder.CreateIndex(
                name: "ix_pages_tenant_id_shop_id_current_version_id",
                schema: "content",
                table: "pages",
                columns: new[] { "tenant_id", "shop_id", "current_version_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_methods_stripe_payment_method_id",
                schema: "billing",
                table: "payment_methods",
                column: "stripe_payment_method_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_methods_tenant_id",
                schema: "billing",
                table: "payment_methods",
                column: "tenant_id",
                unique: true,
                filter: "is_default AND detached_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_id_order_id",
                schema: "billing",
                table: "payments",
                columns: new[] { "tenant_id", "order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_id_subscription_id",
                schema: "billing",
                table: "payments",
                columns: new[] { "tenant_id", "subscription_id" });

            migrationBuilder.CreateIndex(
                name: "ix_price_lists_market_code_valid_from",
                schema: "billing",
                table: "price_lists",
                columns: new[] { "market_code", "valid_from" });

            migrationBuilder.CreateIndex(
                name: "ix_price_tiers_price_list_id_code",
                schema: "billing",
                table: "price_tiers",
                columns: new[] { "price_list_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_promo_codes_code",
                schema: "billing",
                table: "promo_codes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_promo_codes_tenant_id",
                schema: "billing",
                table: "promo_codes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_protocols_generated_by",
                schema: "fixes",
                table: "protocols",
                column: "generated_by");

            migrationBuilder.CreateIndex(
                name: "ix_protocols_tenant_id_number",
                schema: "fixes",
                table: "protocols",
                columns: new[] { "tenant_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_protocols_tenant_id_shop_id",
                schema: "fixes",
                table: "protocols",
                columns: new[] { "tenant_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_publications_idempotency_key",
                schema: "fixes",
                table: "publications",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_publications_requested_by",
                schema: "fixes",
                table: "publications",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "ix_publications_tenant_id_connector_id",
                schema: "fixes",
                table: "publications",
                columns: new[] { "tenant_id", "connector_id" });

            migrationBuilder.CreateIndex(
                name: "ix_publications_tenant_id_shop_id_page_id",
                schema: "fixes",
                table: "publications",
                columns: new[] { "tenant_id", "shop_id", "page_id" });

            migrationBuilder.CreateIndex(
                name: "ix_questions_answered_by",
                schema: "checks",
                table: "questions",
                column: "answered_by");

            migrationBuilder.CreateIndex(
                name: "ix_questions_tenant_id_evidence_id",
                schema: "checks",
                table: "questions",
                columns: new[] { "tenant_id", "evidence_id" });

            migrationBuilder.CreateIndex(
                name: "ix_questions_tenant_id_finding_id",
                schema: "checks",
                table: "questions",
                columns: new[] { "tenant_id", "finding_id" });

            migrationBuilder.CreateIndex(
                name: "ix_questions_tenant_id_shop_id_status",
                schema: "checks",
                table: "questions",
                columns: new[] { "tenant_id", "shop_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_rule_sets_module_version",
                schema: "checks",
                table: "rule_sets",
                columns: new[] { "module", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_run_events_run_id_id",
                schema: "checks",
                table: "run_events",
                columns: new[] { "run_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_run_events_tenant_id_run_id",
                schema: "checks",
                table: "run_events",
                columns: new[] { "tenant_id", "run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_runs_requested_by",
                schema: "checks",
                table: "runs",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "ix_runs_tenant_id_order_id",
                schema: "checks",
                table: "runs",
                columns: new[] { "tenant_id", "order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_runs_tenant_id_shop_id_created_at",
                schema: "checks",
                table: "runs",
                columns: new[] { "tenant_id", "shop_id", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_schedules_next_run_at",
                schema: "ops",
                table: "schedules",
                column: "next_run_at");

            migrationBuilder.CreateIndex(
                name: "ix_schedules_tenant_id_shop_id",
                schema: "ops",
                table: "schedules",
                columns: new[] { "tenant_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_shop_facts_created_by",
                schema: "shop",
                table: "shop_facts",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_shop_facts_tenant_id_shop_id",
                schema: "shop",
                table: "shop_facts",
                columns: new[] { "tenant_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_shop_languages_tenant_id_sample_run_id",
                schema: "shop",
                table: "shop_languages",
                columns: new[] { "tenant_id", "sample_run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_shop_languages_tenant_id_shop_id",
                schema: "shop",
                table: "shop_languages",
                columns: new[] { "tenant_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_shop_markets_confirmed_by",
                schema: "shop",
                table: "shop_markets",
                column: "confirmed_by");

            migrationBuilder.CreateIndex(
                name: "ix_shop_markets_tenant_id_detection_run_id",
                schema: "shop",
                table: "shop_markets",
                columns: new[] { "tenant_id", "detection_run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_shop_markets_tenant_id_shop_id",
                schema: "shop",
                table: "shop_markets",
                columns: new[] { "tenant_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_shop_verifications_tenant_id_shop_id",
                schema: "shop",
                table: "shop_verifications",
                columns: new[] { "tenant_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_shops_tenant_id_domain_base_path",
                schema: "shop",
                table: "shops",
                columns: new[] { "tenant_id", "domain", "base_path" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_shops_tenant_id_last_full_run_id",
                schema: "shop",
                table: "shops",
                columns: new[] { "tenant_id", "last_full_run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_stripe_events_tenant_id",
                schema: "billing",
                table: "stripe_events",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_changes_tenant_id_subscription_id",
                schema: "billing",
                table: "subscription_changes",
                columns: new[] { "tenant_id", "subscription_id" });

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_price_list_id",
                schema: "billing",
                table: "subscriptions",
                column: "price_list_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_shop_id",
                schema: "billing",
                table: "subscriptions",
                column: "shop_id",
                unique: true,
                filter: "status <> 'canceled'");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_stripe_subscription_id",
                schema: "billing",
                table: "subscriptions",
                column: "stripe_subscription_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_tenant_id_shop_id",
                schema: "billing",
                table: "subscriptions",
                columns: new[] { "tenant_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_tenants_locale",
                schema: "iam",
                table: "tenants",
                column: "locale");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_market_code",
                schema: "iam",
                table: "tenants",
                column: "market_code");

            migrationBuilder.CreateIndex(
                name: "ix_usage_records_run_id",
                schema: "usage",
                table: "usage_records",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ix_usage_records_tenant_id_occurred_at",
                schema: "usage",
                table: "usage_records",
                columns: new[] { "tenant_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_user_logins_provider_provider_key",
                schema: "iam",
                table: "user_logins",
                columns: new[] { "provider", "provider_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_logins_user_id",
                schema: "iam",
                table: "user_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_tokens_email_purpose",
                schema: "iam",
                table: "user_tokens",
                columns: new[] { "email", "purpose" },
                filter: "used_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_user_tokens_token_hash",
                schema: "iam",
                table: "user_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_tokens_user_id",
                schema: "iam",
                table: "user_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_locale",
                schema: "iam",
                table: "users",
                column: "locale");

            migrationBuilder.CreateIndex(
                name: "ix_volume_discounts_price_list_id_from_shop_number",
                schema: "billing",
                table: "volume_discounts",
                columns: new[] { "price_list_id", "from_shop_number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_connector_events_connectors_tenant_id_connector_id",
                schema: "shop",
                table: "connector_events",
                columns: new[] { "tenant_id", "connector_id" },
                principalSchema: "shop",
                principalTable: "connectors",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_connector_events_shops_tenant_id_shop_id",
                schema: "shop",
                table: "connector_events",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_connector_webhooks_connectors_tenant_id_connector_id",
                schema: "shop",
                table: "connector_webhooks",
                columns: new[] { "tenant_id", "connector_id" },
                principalSchema: "shop",
                principalTable: "connectors",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_connectors_shops_tenant_id_shop_id",
                schema: "shop",
                table: "connectors",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_decision_memory_fix_proposals_tenant_id_source_proposal_id",
                schema: "fixes",
                table: "decision_memory",
                columns: new[] { "tenant_id", "source_proposal_id" },
                principalSchema: "fixes",
                principalTable: "fix_proposals",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_decision_memory_shops_tenant_id_shop_id",
                schema: "fixes",
                table: "decision_memory",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_evidence_links_findings_tenant_id_finding_id",
                schema: "fixes",
                table: "evidence_links",
                columns: new[] { "tenant_id", "finding_id" },
                principalSchema: "checks",
                principalTable: "findings",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_evidence_links_pages_tenant_id_shop_id_page_id",
                schema: "fixes",
                table: "evidence_links",
                columns: new[] { "tenant_id", "shop_id", "page_id" },
                principalSchema: "content",
                principalTable: "pages",
                principalColumns: new[] { "tenant_id", "shop_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_evidence_links_shops_tenant_id_shop_id",
                schema: "fixes",
                table: "evidence_links",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_feeds_shops_tenant_id_shop_id",
                schema: "shop",
                table: "feeds",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_finding_occurrences_findings_tenant_id_finding_id",
                schema: "checks",
                table: "finding_occurrences",
                columns: new[] { "tenant_id", "finding_id" },
                principalSchema: "checks",
                principalTable: "findings",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_finding_occurrences_pages_tenant_id_shop_id_page_id",
                schema: "checks",
                table: "finding_occurrences",
                columns: new[] { "tenant_id", "shop_id", "page_id" },
                principalSchema: "content",
                principalTable: "pages",
                principalColumns: new[] { "tenant_id", "shop_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_findings_pages_tenant_id_shop_id_page_id",
                schema: "checks",
                table: "findings",
                columns: new[] { "tenant_id", "shop_id", "page_id" },
                principalSchema: "content",
                principalTable: "pages",
                principalColumns: new[] { "tenant_id", "shop_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_findings_runs_tenant_id_first_run_id",
                schema: "checks",
                table: "findings",
                columns: new[] { "tenant_id", "first_run_id" },
                principalSchema: "checks",
                principalTable: "runs",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_findings_runs_tenant_id_last_seen_run_id",
                schema: "checks",
                table: "findings",
                columns: new[] { "tenant_id", "last_seen_run_id" },
                principalSchema: "checks",
                principalTable: "runs",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_findings_runs_tenant_id_resolved_run_id",
                schema: "checks",
                table: "findings",
                columns: new[] { "tenant_id", "resolved_run_id" },
                principalSchema: "checks",
                principalTable: "runs",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_findings_shops_tenant_id_shop_id",
                schema: "checks",
                table: "findings",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_fix_groups_shops_tenant_id_shop_id",
                schema: "fixes",
                table: "fix_groups",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_fix_proposals_page_versions_tenant_id_shop_id_page_version_",
                schema: "fixes",
                table: "fix_proposals",
                columns: new[] { "tenant_id", "shop_id", "page_version_id" },
                principalSchema: "content",
                principalTable: "page_versions",
                principalColumns: new[] { "tenant_id", "shop_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_fix_proposals_pages_tenant_id_shop_id_page_id",
                schema: "fixes",
                table: "fix_proposals",
                columns: new[] { "tenant_id", "shop_id", "page_id" },
                principalSchema: "content",
                principalTable: "pages",
                principalColumns: new[] { "tenant_id", "shop_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_fix_proposals_runs_tenant_id_created_run_id",
                schema: "fixes",
                table: "fix_proposals",
                columns: new[] { "tenant_id", "created_run_id" },
                principalSchema: "checks",
                principalTable: "runs",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_fix_proposals_shops_tenant_id_shop_id",
                schema: "fixes",
                table: "fix_proposals",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_free_sample_claims_shops_tenant_id_shop_id",
                schema: "shop",
                table: "free_sample_claims",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_free_sample_claims_tenants_tenant_id",
                schema: "shop",
                table: "free_sample_claims",
                column: "tenant_id",
                principalSchema: "iam",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_invitations_tenants_tenant_id",
                schema: "iam",
                table: "invitations",
                column: "tenant_id",
                principalSchema: "iam",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_invoices_payments_tenant_id_payment_id",
                schema: "billing",
                table: "invoices",
                columns: new[] { "tenant_id", "payment_id" },
                principalSchema: "billing",
                principalTable: "payments",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_invoices_shops_tenant_id_shop_id",
                schema: "billing",
                table: "invoices",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_jev_answers_tenants_tenant_id",
                schema: "checks",
                table: "jev_answers",
                column: "tenant_id",
                principalSchema: "iam",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_markets_price_lists_price_list_id",
                schema: "ref",
                table: "markets",
                column: "price_list_id",
                principalSchema: "billing",
                principalTable: "price_lists",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_notification_settings_shops_tenant_id_shop_id",
                schema: "iam",
                table: "notification_settings",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_shops_tenant_id_shop_id",
                schema: "iam",
                table: "notifications",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_orders_runs_tenant_id_run_id",
                schema: "billing",
                table: "orders",
                columns: new[] { "tenant_id", "run_id" },
                principalSchema: "checks",
                principalTable: "runs",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_orders_shops_tenant_id_shop_id",
                schema: "billing",
                table: "orders",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_page_changes_pages_tenant_id_shop_id_page_id",
                schema: "checks",
                table: "page_changes",
                columns: new[] { "tenant_id", "shop_id", "page_id" },
                principalSchema: "content",
                principalTable: "pages",
                principalColumns: new[] { "tenant_id", "shop_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_page_changes_runs_tenant_id_run_id",
                schema: "checks",
                table: "page_changes",
                columns: new[] { "tenant_id", "run_id" },
                principalSchema: "checks",
                principalTable: "runs",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_page_changes_shops_tenant_id_shop_id",
                schema: "checks",
                table: "page_changes",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_page_profiles_runs_tenant_id_created_run_id",
                schema: "shop",
                table: "page_profiles",
                columns: new[] { "tenant_id", "created_run_id" },
                principalSchema: "checks",
                principalTable: "runs",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_page_profiles_shops_tenant_id_shop_id",
                schema: "shop",
                table: "page_profiles",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_page_versions_pages_tenant_id_shop_id_page_id",
                schema: "content",
                table: "page_versions",
                columns: new[] { "tenant_id", "shop_id", "page_id" },
                principalSchema: "content",
                principalTable: "pages",
                principalColumns: new[] { "tenant_id", "shop_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_page_versions_runs_tenant_id_run_id",
                schema: "content",
                table: "page_versions",
                columns: new[] { "tenant_id", "run_id" },
                principalSchema: "checks",
                principalTable: "runs",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pages_shops_tenant_id_shop_id",
                schema: "content",
                table: "pages",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_payments_subscriptions_tenant_id_subscription_id",
                schema: "billing",
                table: "payments",
                columns: new[] { "tenant_id", "subscription_id" },
                principalSchema: "billing",
                principalTable: "subscriptions",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_protocols_shops_tenant_id_shop_id",
                schema: "fixes",
                table: "protocols",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_publications_shops_tenant_id_shop_id",
                schema: "fixes",
                table: "publications",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_questions_shops_tenant_id_shop_id",
                schema: "checks",
                table: "questions",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_run_events_runs_tenant_id_run_id",
                schema: "checks",
                table: "run_events",
                columns: new[] { "tenant_id", "run_id" },
                principalSchema: "checks",
                principalTable: "runs",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_runs_shops_tenant_id_shop_id",
                schema: "checks",
                table: "runs",
                columns: new[] { "tenant_id", "shop_id" },
                principalSchema: "shop",
                principalTable: "shops",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            // Partitioned tables got PARTITION BY from EshopGuardMigrationsSqlGenerator; here their HASH partitions.
            migrationBuilder.Sql(SqlResource.Read("F1/02_partitions.sql"));
            migrationBuilder.Sql(SqlResource.Read("F1/03_expression_indexes.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_orders_shops_tenant_id_shop_id",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "fk_page_profiles_shops_tenant_id_shop_id",
                schema: "shop",
                table: "page_profiles");

            migrationBuilder.DropForeignKey(
                name: "fk_pages_shops_tenant_id_shop_id",
                schema: "content",
                table: "pages");

            migrationBuilder.DropForeignKey(
                name: "fk_runs_shops_tenant_id_shop_id",
                schema: "checks",
                table: "runs");

            migrationBuilder.DropForeignKey(
                name: "fk_orders_users_created_by",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "fk_runs_users_requested_by",
                schema: "checks",
                table: "runs");

            migrationBuilder.DropForeignKey(
                name: "fk_page_versions_pages_tenant_id_shop_id_page_id",
                schema: "content",
                table: "page_versions");

            migrationBuilder.DropForeignKey(
                name: "fk_orders_runs_tenant_id_run_id",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "fk_markets_price_lists_price_list_id",
                schema: "ref",
                table: "markets");

            migrationBuilder.DropTable(
                name: "audit_log",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "connector_events",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "connector_webhooks",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "decision_memory",
                schema: "fixes");

            migrationBuilder.DropTable(
                name: "domains",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "evidence_links",
                schema: "fixes");

            migrationBuilder.DropTable(
                name: "feeds",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "finding_occurrences",
                schema: "checks");

            migrationBuilder.DropTable(
                name: "free_sample_claims",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "invitations",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "invoices",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "jev_answers",
                schema: "checks");

            migrationBuilder.DropTable(
                name: "jobs",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "memberships",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "notification_settings",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "outbox",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "page_changes",
                schema: "checks");

            migrationBuilder.DropTable(
                name: "payment_methods",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "price_tiers",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "promo_codes",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "protocols",
                schema: "fixes");

            migrationBuilder.DropTable(
                name: "publications",
                schema: "fixes");

            migrationBuilder.DropTable(
                name: "questions",
                schema: "checks");

            migrationBuilder.DropTable(
                name: "rate_limit_buckets",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "rewrite_cache",
                schema: "fixes");

            migrationBuilder.DropTable(
                name: "run_events",
                schema: "checks");

            migrationBuilder.DropTable(
                name: "schedules",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "shop_facts",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "shop_languages",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "shop_markets",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "shop_verifications",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "sieve_answers",
                schema: "checks");

            migrationBuilder.DropTable(
                name: "stripe_events",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "subscription_changes",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "system_settings",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "usage_daily",
                schema: "usage");

            migrationBuilder.DropTable(
                name: "usage_records",
                schema: "usage");

            migrationBuilder.DropTable(
                name: "user_logins",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "user_tokens",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "volume_discounts",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "workers",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "fix_proposals",
                schema: "fixes");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "connectors",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "evidence_items",
                schema: "fixes");

            migrationBuilder.DropTable(
                name: "findings",
                schema: "checks");

            migrationBuilder.DropTable(
                name: "fix_groups",
                schema: "fixes");

            migrationBuilder.DropTable(
                name: "subscriptions",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "rule_sets",
                schema: "checks");

            migrationBuilder.DropTable(
                name: "shops",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "users",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "locales",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "pages",
                schema: "content");

            migrationBuilder.DropTable(
                name: "page_profiles",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "page_versions",
                schema: "content");

            migrationBuilder.DropTable(
                name: "runs",
                schema: "checks");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "price_lists",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "markets",
                schema: "ref");

            migrationBuilder.Sql(SqlResource.Read("F1/01_functions_down.sql"));
        }
    }
}
