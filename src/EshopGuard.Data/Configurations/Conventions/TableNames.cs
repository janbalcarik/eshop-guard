namespace EshopGuard.Data.Configurations.Conventions;

/// <summary>Schemas of the data model.</summary>
public static class Schemas
{
    /// <summary>Schema <c>iam</c>.</summary>
    public const string Iam = "iam";

    /// <summary>Schema <c>shop</c>.</summary>
    public const string Shops = "shop";

    /// <summary>Schema <c>content</c>.</summary>
    public const string Content = "content";

    /// <summary>Schema <c>checks</c>.</summary>
    public const string Checks = "checks";

    /// <summary>Schema <c>fixes</c>.</summary>
    public const string Fixes = "fixes";

    /// <summary>Schema <c>billing</c>.</summary>
    public const string Billing = "billing";

    /// <summary>Schema <c>usage</c>.</summary>
    public const string Usage = "usage";

    /// <summary>Schema <c>ops</c>.</summary>
    public const string Ops = "ops";

    /// <summary>Schema <c>ref</c>.</summary>
    public const string Ref = "ref";
}

/// <summary>The single list of tenant and global tables (used by the migrations review and the catalogue tests).</summary>
public static class TableNames
{
    /// <summary>Tables of a tenant: RLS with FORCE and the policy <c>tenant_isolation</c>.</summary>
    public static IReadOnlyList<string> TenantTables { get; } =
    [
        "iam.memberships",
        "iam.invitations",
        "iam.notification_settings",
        "iam.notifications",
        "shop.shops",
        "shop.shop_markets",
        "shop.shop_languages",
        "shop.shop_verifications",
        "shop.connectors",
        "shop.connector_webhooks",
        "shop.connector_events",
        "shop.feeds",
        "shop.page_profiles",
        "shop.shop_facts",
        "content.pages",
        "content.page_versions",
        "checks.runs",
        "checks.run_events",
        "checks.run_urls",
        "checks.run_scopes",
        "checks.jev_answers",
        "checks.sieve_answers",
        "checks.findings",
        "checks.finding_occurrences",
        "checks.questions",
        "checks.page_changes",
        "fixes.fix_groups",
        "fixes.fix_proposals",
        "fixes.publications",
        "fixes.decision_memory",
        "fixes.evidence_items",
        "fixes.evidence_links",
        "fixes.protocols",
        "fixes.rewrite_cache",
        "billing.payment_methods",
        "billing.orders",
        "billing.subscriptions",
        "billing.subscription_changes",
        "billing.payments",
        "billing.invoices",
        "ops.schedules",
        "ops.outbox",
        "ops.audit_log",
    ];

    /// <summary>Global tables without RLS (no customer texts).</summary>
    public static IReadOnlyList<string> GlobalTables { get; } =
    [
        "iam.tenants",
        "iam.users",
        "iam.user_logins",
        "iam.user_tokens",
        "shop.free_sample_claims",
        "checks.rule_sets",
        "billing.price_lists",
        "billing.price_tiers",
        "billing.volume_discounts",
        "billing.promo_codes",
        "billing.stripe_events",
        "usage.usage_records",
        "usage.usage_daily",
        "ops.jobs",
        "ops.workers",
        "ops.domains",
        "ops.rate_limit_buckets",
        "ops.system_settings",
        "ref.markets",
        "ref.locales",
    ];

    /// <summary>Partitioned tables and their partitioning (created by migration SQL, EF maps them as ordinary tables).</summary>
    public static IReadOnlyDictionary<string, string> PartitionedTables { get; } = new Dictionary<string, string>
    {
        ["shop.connector_events"] = "RANGE (received_at)",
        ["content.pages"] = "HASH (shop_id)",
        ["content.page_versions"] = "HASH (shop_id)",
        ["checks.run_events"] = "RANGE (at)",
        ["checks.jev_answers"] = "HASH (tenant_id)",
        ["checks.sieve_answers"] = "HASH (tenant_id)",
        ["usage.usage_records"] = "RANGE (occurred_at)",
        ["ops.audit_log"] = "RANGE (at)",
    };
}
