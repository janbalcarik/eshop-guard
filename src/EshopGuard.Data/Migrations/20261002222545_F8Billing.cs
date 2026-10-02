using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EshopGuard.Data.Migrations
{
    /// <summary>
    /// Change 12 (task 2.1, the state of the schema <c>billing</c> of change 3 compared with the design): change 3 already has
    /// every table, the RLS of the tables of tenants and the single default card per tenant. Missing were: the mode and the
    /// state of the synchronization of a price list with Stripe, the fair use, the active price list; three lookup keys per tier
    /// instead of one (a tier has three prices); prices of the tier <c>custom</c> may be empty; the snapshot of the quote in an
    /// order (prices of Stripe, coupon, <c>scope_hash</c>, terms, tax treatment, trial, attempt of Checkout) and one open order
    /// per e-shop; in a subscription the price, coupon, schedule, order of the e-shop, reminder and reason of a pause, one
    /// running subscription per e-shop by the states of Stripe (instead of <c>status &lt;&gt; 'canceled'</c>); the reason and the
    /// fingerprint of a change; the source of an invoice (unique, AD 8), its period, reason of review and delivery; the mode,
    /// attempts and object of an event; the verification of the VAT id of a tenant; one payment per invoice of Stripe; the
    /// states as CHECK. New: the table of a tenant <c>billing.price_quotes</c> (RLS), privileges of the worker (columns of
    /// <c>iam.tenants</c> from Stripe, events of the reconciliation, audit of price lists) and the drafts SK/EUR and CZ/CZK.
    /// </summary>
    public partial class F8Billing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_subscriptions_shop_id",
                schema: "billing",
                table: "subscriptions");

            // One lookup key per tier became three (one per price): the old column held nothing in use.
            migrationBuilder.DropColumn(
                name: "lookup_key",
                schema: "billing",
                table: "price_tiers");

            migrationBuilder.AddColumn<string>(
                name: "lookup_key_yearly",
                schema: "billing",
                table: "price_tiers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stripe_mode",
                schema: "billing",
                table: "volume_discounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_id_status",
                schema: "iam",
                table: "tenants",
                type: "text",
                nullable: false,
                defaultValueSql: "'none'");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "tax_id_verified_at",
                schema: "iam",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "order_id",
                schema: "billing",
                table: "subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pause_reason",
                schema: "billing",
                table: "subscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "schedule_hash",
                schema: "billing",
                table: "subscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "shop_ordinal",
                schema: "billing",
                table: "subscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stripe_coupon_id",
                schema: "billing",
                table: "subscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stripe_price_id",
                schema: "billing",
                table: "subscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stripe_schedule_id",
                schema: "billing",
                table: "subscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "trial_reminder_sent_at",
                schema: "billing",
                table: "subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reason_code",
                schema: "billing",
                table: "subscription_changes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stripe_schedule_phase_hash",
                schema: "billing",
                table: "subscription_changes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "attempts",
                schema: "billing",
                table: "stripe_events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "livemode",
                schema: "billing",
                table: "stripe_events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "object_id",
                schema: "billing",
                table: "stripe_events",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "monitoring_monthly",
                schema: "billing",
                table: "price_tiers",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "analysis_price",
                schema: "billing",
                table: "price_tiers",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "archived_at",
                schema: "billing",
                table: "price_tiers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lookup_key_analysis",
                schema: "billing",
                table: "price_tiers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lookup_key_monthly",
                schema: "billing",
                table: "price_tiers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "activated_at",
                schema: "billing",
                table: "price_lists",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fair_use_other_pages_factor",
                schema: "billing",
                table: "price_lists",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 2m);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "impact",
                schema: "billing",
                table: "price_lists",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "impact_at",
                schema: "billing",
                table: "price_lists",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stripe_mode",
                schema: "billing",
                table: "price_lists",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sync_error",
                schema: "billing",
                table: "price_lists",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sync_status",
                schema: "billing",
                table: "price_lists",
                type: "text",
                nullable: false,
                defaultValueSql: "'pending'");

            migrationBuilder.AddColumn<string>(
                name: "stripe_charge_id",
                schema: "billing",
                table: "payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "checkout_attempt",
                schema: "billing",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "checkout_expires_at",
                schema: "billing",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "monitoring_discount_percent",
                schema: "billing",
                table: "orders",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "monitoring_monthly",
                schema: "billing",
                table: "orders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "price_quote_id",
                schema: "billing",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scope_hash",
                schema: "billing",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stripe_coupon_id",
                schema: "billing",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stripe_price_analysis",
                schema: "billing",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stripe_price_monitoring",
                schema: "billing",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stripe_subscription_id",
                schema: "billing",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_treatment",
                schema: "billing",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "terms_version",
                schema: "billing",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "trial_end_planned",
                schema: "billing",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email_status",
                schema: "billing",
                table: "invoices",
                type: "text",
                nullable: false,
                defaultValueSql: "'not_required'");

            migrationBuilder.AddColumn<string>(
                name: "needs_review_reason",
                schema: "billing",
                table: "invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "period_from",
                schema: "billing",
                table: "invoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "period_to",
                schema: "billing",
                table: "invoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_key",
                schema: "billing",
                table: "invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_kind",
                schema: "billing",
                table: "invoices",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "price_quotes",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    basis_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scope_hash = table.Column<string>(type: "text", nullable: false),
                    price_list_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tier_code = table.Column<string>(type: "text", nullable: true),
                    price_unit = table.Column<string>(type: "text", nullable: true),
                    counted_products = table.Column<int>(type: "integer", nullable: true),
                    other_pages = table.Column<int>(type: "integer", nullable: true),
                    versions = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    markets = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    analysis_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    monitoring_monthly = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    discount_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    currency = table.Column<string>(type: "character(3)", nullable: false),
                    vat_preview = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    fair_use = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    reason_code = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_quotes", x => x.id);
                    table.UniqueConstraint("ak_price_quotes_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_price_quotes_status", "status IN ('offer', 'individual_offer', 'unavailable')");
                    table.ForeignKey(
                        name: "fk_price_quotes_price_lists_price_list_id",
                        column: x => x.price_list_id,
                        principalSchema: "billing",
                        principalTable: "price_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_price_quotes_runs_tenant_id_basis_run_id",
                        columns: x => new { x.tenant_id, x.basis_run_id },
                        principalSchema: "checks",
                        principalTable: "runs",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_price_quotes_shops_tenant_id_shop_id",
                        columns: x => new { x.tenant_id, x.shop_id },
                        principalSchema: "shop",
                        principalTable: "shops",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_price_quotes_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_volume_discounts_stripe_mode",
                schema: "billing",
                table: "volume_discounts",
                sql: "stripe_mode IN ('test', 'live')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenants_tax_id_status",
                schema: "iam",
                table: "tenants",
                sql: "tax_id_status IN ('none', 'pending', 'verified', 'unverified')");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_shop_id",
                schema: "billing",
                table: "subscriptions",
                column: "shop_id",
                unique: true,
                filter: "status IN ('trialing', 'active', 'past_due', 'incomplete')");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_tenant_id_order_id",
                schema: "billing",
                table: "subscriptions",
                columns: new[] { "tenant_id", "order_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscription_changes_status",
                schema: "billing",
                table: "subscription_changes",
                sql: "status IN ('scheduled', 'notified', 'applied', 'canceled')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_stripe_events_status",
                schema: "billing",
                table: "stripe_events",
                sql: "status IN ('received', 'processed', 'ignored', 'failed')");

            migrationBuilder.CreateIndex(
                name: "ux_price_lists_published",
                schema: "billing",
                table: "price_lists",
                columns: new[] { "market_code", "currency", "valid_from" },
                unique: true,
                filter: "status = 'published'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_price_lists_stripe_mode",
                schema: "billing",
                table: "price_lists",
                sql: "stripe_mode IN ('test', 'live')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_price_lists_sync_status",
                schema: "billing",
                table: "price_lists",
                sql: "sync_status IN ('pending', 'synced', 'failed')");

            migrationBuilder.CreateIndex(
                name: "ix_payments_stripe_charge_id",
                schema: "billing",
                table: "payments",
                column: "stripe_charge_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_stripe_invoice_id",
                schema: "billing",
                table: "payments",
                column: "stripe_invoice_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_price_quote_id",
                schema: "billing",
                table: "orders",
                columns: new[] { "tenant_id", "price_quote_id" });

            migrationBuilder.CreateIndex(
                name: "ux_orders_open_per_shop",
                schema: "billing",
                table: "orders",
                column: "shop_id",
                unique: true,
                filter: "status IN ('created', 'checkout_open')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_tax_treatment",
                schema: "billing",
                table: "orders",
                sql: "tax_treatment IN ('domestic_vat', 'reverse_charge', 'pending_verification', 'undetermined')");

            migrationBuilder.CreateIndex(
                name: "ix_invoices_source_key",
                schema: "billing",
                table: "invoices",
                column: "source_key",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoices_email_status",
                schema: "billing",
                table: "invoices",
                sql: "email_status IN ('not_required', 'pending', 'sent', 'failed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoices_source_kind",
                schema: "billing",
                table: "invoices",
                sql: "source_kind IN ('stripe_invoice', 'stripe_refund')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoices_status",
                schema: "billing",
                table: "invoices",
                sql: "status IN ('creating', 'issued', 'needs_review', 'failed')");

            migrationBuilder.CreateIndex(
                name: "ix_price_quotes_created_by",
                schema: "billing",
                table: "price_quotes",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_price_quotes_price_list_id",
                schema: "billing",
                table: "price_quotes",
                column: "price_list_id");

            migrationBuilder.CreateIndex(
                name: "ix_price_quotes_shop_id_scope_hash_price_list_id",
                schema: "billing",
                table: "price_quotes",
                columns: new[] { "shop_id", "scope_hash", "price_list_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_price_quotes_tenant_id_basis_run_id",
                schema: "billing",
                table: "price_quotes",
                columns: new[] { "tenant_id", "basis_run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_price_quotes_tenant_id_shop_id",
                schema: "billing",
                table: "price_quotes",
                columns: new[] { "tenant_id", "shop_id" });

            // The columns are new and empty. Validating existing rows would run under the RLS of the owner (FORCE ROW LEVEL
            // SECURITY) without a tenant and fail (as in F6), so the constraints are added NOT VALID: every new or changed row is checked.
            migrationBuilder.Sql(
                "ALTER TABLE billing.orders ADD CONSTRAINT fk_orders_price_quotes_tenant_id_price_quote_id FOREIGN KEY (tenant_id, price_quote_id) " +
                "REFERENCES billing.price_quotes (tenant_id, id) ON DELETE RESTRICT NOT VALID");
            migrationBuilder.Sql(
                "ALTER TABLE billing.subscriptions ADD CONSTRAINT fk_subscriptions_orders_tenant_id_order_id FOREIGN KEY (tenant_id, order_id) " +
                "REFERENCES billing.orders (tenant_id, id) ON DELETE RESTRICT NOT VALID");

            migrationBuilder.Sql(SqlResource.Read("F8/01_billing.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("F8/01_billing_down.sql"));

            migrationBuilder.DropForeignKey(
                name: "fk_orders_price_quotes_tenant_id_price_quote_id",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "fk_subscriptions_orders_tenant_id_order_id",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropTable(
                name: "price_quotes",
                schema: "billing");

            migrationBuilder.DropCheckConstraint(
                name: "ck_volume_discounts_stripe_mode",
                schema: "billing",
                table: "volume_discounts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenants_tax_id_status",
                schema: "iam",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "ix_subscriptions_shop_id",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropIndex(
                name: "ix_subscriptions_tenant_id_order_id",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_subscription_changes_status",
                schema: "billing",
                table: "subscription_changes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_stripe_events_status",
                schema: "billing",
                table: "stripe_events");

            migrationBuilder.DropIndex(
                name: "ux_price_lists_published",
                schema: "billing",
                table: "price_lists");

            migrationBuilder.DropCheckConstraint(
                name: "ck_price_lists_stripe_mode",
                schema: "billing",
                table: "price_lists");

            migrationBuilder.DropCheckConstraint(
                name: "ck_price_lists_sync_status",
                schema: "billing",
                table: "price_lists");

            migrationBuilder.DropIndex(
                name: "ix_payments_stripe_charge_id",
                schema: "billing",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ix_payments_stripe_invoice_id",
                schema: "billing",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ix_orders_tenant_id_price_quote_id",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ux_orders_open_per_shop",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_tax_treatment",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_invoices_source_key",
                schema: "billing",
                table: "invoices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoices_email_status",
                schema: "billing",
                table: "invoices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoices_source_kind",
                schema: "billing",
                table: "invoices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoices_status",
                schema: "billing",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "stripe_mode",
                schema: "billing",
                table: "volume_discounts");

            migrationBuilder.DropColumn(
                name: "tax_id_status",
                schema: "iam",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "tax_id_verified_at",
                schema: "iam",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "order_id",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "pause_reason",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "schedule_hash",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "shop_ordinal",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "stripe_coupon_id",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "stripe_price_id",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "stripe_schedule_id",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "trial_reminder_sent_at",
                schema: "billing",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "reason_code",
                schema: "billing",
                table: "subscription_changes");

            migrationBuilder.DropColumn(
                name: "stripe_schedule_phase_hash",
                schema: "billing",
                table: "subscription_changes");

            migrationBuilder.DropColumn(
                name: "attempts",
                schema: "billing",
                table: "stripe_events");

            migrationBuilder.DropColumn(
                name: "livemode",
                schema: "billing",
                table: "stripe_events");

            migrationBuilder.DropColumn(
                name: "object_id",
                schema: "billing",
                table: "stripe_events");

            migrationBuilder.DropColumn(
                name: "archived_at",
                schema: "billing",
                table: "price_tiers");

            migrationBuilder.DropColumn(
                name: "lookup_key_analysis",
                schema: "billing",
                table: "price_tiers");

            migrationBuilder.DropColumn(
                name: "lookup_key_monthly",
                schema: "billing",
                table: "price_tiers");

            migrationBuilder.DropColumn(
                name: "activated_at",
                schema: "billing",
                table: "price_lists");

            migrationBuilder.DropColumn(
                name: "fair_use_other_pages_factor",
                schema: "billing",
                table: "price_lists");

            migrationBuilder.DropColumn(
                name: "impact",
                schema: "billing",
                table: "price_lists");

            migrationBuilder.DropColumn(
                name: "impact_at",
                schema: "billing",
                table: "price_lists");

            migrationBuilder.DropColumn(
                name: "stripe_mode",
                schema: "billing",
                table: "price_lists");

            migrationBuilder.DropColumn(
                name: "sync_error",
                schema: "billing",
                table: "price_lists");

            migrationBuilder.DropColumn(
                name: "sync_status",
                schema: "billing",
                table: "price_lists");

            migrationBuilder.DropColumn(
                name: "stripe_charge_id",
                schema: "billing",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "checkout_attempt",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "checkout_expires_at",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "monitoring_discount_percent",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "monitoring_monthly",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "price_quote_id",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "scope_hash",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "stripe_coupon_id",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "stripe_price_analysis",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "stripe_price_monitoring",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "stripe_subscription_id",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "tax_treatment",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "terms_version",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "trial_end_planned",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "email_status",
                schema: "billing",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "needs_review_reason",
                schema: "billing",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "period_from",
                schema: "billing",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "period_to",
                schema: "billing",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "source_key",
                schema: "billing",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "source_kind",
                schema: "billing",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "lookup_key_yearly",
                schema: "billing",
                table: "price_tiers");

            migrationBuilder.AddColumn<string>(
                name: "lookup_key",
                schema: "billing",
                table: "price_tiers",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "monitoring_monthly",
                schema: "billing",
                table: "price_tiers",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "analysis_price",
                schema: "billing",
                table: "price_tiers",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_shop_id",
                schema: "billing",
                table: "subscriptions",
                column: "shop_id",
                unique: true,
                filter: "status <> 'canceled'");
        }
    }
}
