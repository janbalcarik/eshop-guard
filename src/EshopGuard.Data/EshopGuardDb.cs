using System.Linq.Expressions;
using EshopGuard.Data.Entities.Common;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Content;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Entities.Ref;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Entities.Usage;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EshopGuard.Data;

/// <summary>
/// Database context of the web application. Tenant data are protected twice: the named EF filter <c>"Tenant"</c>
/// (<see cref="ITenantContext"/>) and Row-Level Security in PostgreSQL (<c>app.tenant_id</c> set per transaction).
/// Without a tenant both fail loudly instead of returning nothing.
/// </summary>
public sealed class EshopGuardDb : DbContext
{
    /// <summary>Schema of the migration history and operational tables.</summary>
    public const string OpsSchema = "ops";

    /// <summary>Table of the EF Core migration history (in <see cref="OpsSchema"/>, not in <c>public</c>).</summary>
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>Name of the EF query filter on tenant rows.</summary>
    public const string TenantFilter = "Tenant";

    /// <summary>Name of the EF query filter hiding soft-deleted rows.</summary>
    public const string SoftDeleteFilter = "SoftDelete";

    private static readonly DateTimeOffset SeedTime = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly HashSet<object> _hardDeletes = new(ReferenceEqualityComparer.Instance);

    /// <summary>Creates the context; every save runs in a transaction so that <c>app.tenant_id</c> is set.</summary>
    public EshopGuardDb(DbContextOptions<EshopGuardDb> options, ITenantContext tenantContext)
        : base(options)
    {
        TenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;
    }

    /// <summary>Tenant of this unit of work.</summary>
    public ITenantContext TenantContext { get; }

    /// <summary>Tenant used by the query filter; throws <see cref="TenantNotSetException"/> when not set (nothing is sent).</summary>
    internal Guid CurrentTenantId => TenantContext.RequireTenantId();

    /// <summary><c>iam.tenants</c>.</summary>
    public DbSet<Tenant> Tenants => Set<Tenant>();

    /// <summary><c>iam.users</c>.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary><c>iam.user_logins</c>.</summary>
    public DbSet<UserLogin> UserLogins => Set<UserLogin>();

    /// <summary><c>iam.user_tokens</c>.</summary>
    public DbSet<UserToken> UserTokens => Set<UserToken>();

    /// <summary><c>iam.memberships</c>.</summary>
    public DbSet<Membership> Memberships => Set<Membership>();

    /// <summary><c>iam.invitations</c>.</summary>
    public DbSet<Invitation> Invitations => Set<Invitation>();

    /// <summary><c>iam.notification_settings</c>.</summary>
    public DbSet<NotificationSetting> NotificationSettings => Set<NotificationSetting>();

    /// <summary><c>iam.notifications</c>.</summary>
    public DbSet<Notification> Notifications => Set<Notification>();

    /// <summary><c>shop.shops</c>.</summary>
    public DbSet<Shop> Shops => Set<Shop>();

    /// <summary><c>shop.shop_markets</c>.</summary>
    public DbSet<ShopMarket> ShopMarkets => Set<ShopMarket>();

    /// <summary><c>shop.shop_languages</c>.</summary>
    public DbSet<ShopLanguage> ShopLanguages => Set<ShopLanguage>();

    /// <summary><c>shop.shop_verifications</c>.</summary>
    public DbSet<ShopVerification> ShopVerifications => Set<ShopVerification>();

    /// <summary><c>shop.connectors</c>.</summary>
    public DbSet<Connector> Connectors => Set<Connector>();

    /// <summary><c>shop.connector_webhooks</c>.</summary>
    public DbSet<ConnectorWebhook> ConnectorWebhooks => Set<ConnectorWebhook>();

    /// <summary><c>shop.connector_events</c>.</summary>
    public DbSet<ConnectorEvent> ConnectorEvents => Set<ConnectorEvent>();

    /// <summary><c>shop.feeds</c>.</summary>
    public DbSet<Feed> Feeds => Set<Feed>();

    /// <summary><c>shop.page_profiles</c>.</summary>
    public DbSet<PageProfile> PageProfiles => Set<PageProfile>();

    /// <summary><c>shop.shop_facts</c>.</summary>
    public DbSet<ShopFact> ShopFacts => Set<ShopFact>();

    /// <summary><c>shop.free_sample_claims</c>.</summary>
    public DbSet<FreeSampleClaim> FreeSampleClaims => Set<FreeSampleClaim>();

    /// <summary><c>content.pages</c>.</summary>
    public DbSet<Page> Pages => Set<Page>();

    /// <summary><c>content.page_versions</c>.</summary>
    public DbSet<PageVersion> PageVersions => Set<PageVersion>();

    /// <summary><c>checks.rule_sets</c>.</summary>
    public DbSet<RuleSet> RuleSets => Set<RuleSet>();

    /// <summary><c>checks.runs</c>.</summary>
    public DbSet<Run> Runs => Set<Run>();

    /// <summary><c>checks.run_events</c>.</summary>
    public DbSet<RunEvent> RunEvents => Set<RunEvent>();

    /// <summary><c>checks.run_urls</c>.</summary>
    public DbSet<RunUrl> RunUrls => Set<RunUrl>();

    /// <summary><c>checks.run_scopes</c>.</summary>
    public DbSet<RunScope> RunScopes => Set<RunScope>();

    /// <summary><c>checks.jev_answers</c>.</summary>
    public DbSet<JevAnswer> JevAnswers => Set<JevAnswer>();

    /// <summary><c>checks.sieve_answers</c>.</summary>
    public DbSet<SieveAnswer> SieveAnswers => Set<SieveAnswer>();

    /// <summary><c>checks.findings</c>.</summary>
    public DbSet<Finding> Findings => Set<Finding>();

    /// <summary><c>checks.finding_occurrences</c>.</summary>
    public DbSet<FindingOccurrence> FindingOccurrences => Set<FindingOccurrence>();

    /// <summary><c>checks.questions</c>.</summary>
    public DbSet<Question> Questions => Set<Question>();

    /// <summary><c>checks.page_changes</c>.</summary>
    public DbSet<PageChange> PageChanges => Set<PageChange>();

    /// <summary><c>fixes.fix_groups</c>.</summary>
    public DbSet<FixGroup> FixGroups => Set<FixGroup>();

    /// <summary><c>fixes.fix_proposals</c>.</summary>
    public DbSet<FixProposal> FixProposals => Set<FixProposal>();

    /// <summary><c>fixes.publications</c>.</summary>
    public DbSet<Publication> Publications => Set<Publication>();

    /// <summary><c>fixes.decision_memory</c>.</summary>
    public DbSet<DecisionMemory> DecisionMemory => Set<DecisionMemory>();

    /// <summary><c>fixes.evidence_items</c>.</summary>
    public DbSet<EvidenceItem> EvidenceItems => Set<EvidenceItem>();

    /// <summary><c>fixes.evidence_links</c>.</summary>
    public DbSet<EvidenceLink> EvidenceLinks => Set<EvidenceLink>();

    /// <summary><c>fixes.protocols</c>.</summary>
    public DbSet<Protocol> Protocols => Set<Protocol>();

    /// <summary><c>fixes.rewrite_cache</c>.</summary>
    public DbSet<RewriteCacheEntry> RewriteCache => Set<RewriteCacheEntry>();

    /// <summary><c>billing.price_lists</c>.</summary>
    public DbSet<PriceList> PriceLists => Set<PriceList>();

    /// <summary><c>billing.price_tiers</c>.</summary>
    public DbSet<PriceTier> PriceTiers => Set<PriceTier>();

    /// <summary><c>billing.volume_discounts</c>.</summary>
    public DbSet<VolumeDiscount> VolumeDiscounts => Set<VolumeDiscount>();

    /// <summary><c>billing.promo_codes</c>.</summary>
    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();

    /// <summary><c>billing.payment_methods</c>.</summary>
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();

    /// <summary><c>billing.orders</c>.</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary><c>billing.subscriptions</c>.</summary>
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    /// <summary><c>billing.subscription_changes</c>.</summary>
    public DbSet<SubscriptionChange> SubscriptionChanges => Set<SubscriptionChange>();

    /// <summary><c>billing.payments</c>.</summary>
    public DbSet<Payment> Payments => Set<Payment>();

    /// <summary><c>billing.invoices</c>.</summary>
    public DbSet<Invoice> Invoices => Set<Invoice>();

    /// <summary><c>billing.stripe_events</c>.</summary>
    public DbSet<StripeEvent> StripeEvents => Set<StripeEvent>();

    /// <summary><c>usage.usage_records</c>.</summary>
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();

    /// <summary><c>usage.usage_daily</c>.</summary>
    public DbSet<UsageDaily> UsageDaily => Set<UsageDaily>();

    /// <summary><c>ops.jobs</c>.</summary>
    public DbSet<Job> Jobs => Set<Job>();

    /// <summary><c>ops.workers</c>.</summary>
    public DbSet<WorkerNode> Workers => Set<WorkerNode>();

    /// <summary><c>ops.domains</c>.</summary>
    public DbSet<CrawlDomain> Domains => Set<CrawlDomain>();

    /// <summary><c>ops.rate_limit_buckets</c>.</summary>
    public DbSet<RateLimitBucket> RateLimitBuckets => Set<RateLimitBucket>();

    /// <summary><c>ops.schedules</c>.</summary>
    public DbSet<Schedule> Schedules => Set<Schedule>();

    /// <summary><c>ops.outbox</c>.</summary>
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    /// <summary><c>ops.audit_log</c>.</summary>
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    /// <summary><c>ops.system_settings</c>.</summary>
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    /// <summary><c>ref.markets</c>.</summary>
    public DbSet<Market> Markets => Set<Market>();

    /// <summary><c>ref.locales</c>.</summary>
    public DbSet<Locale> Locales => Set<Locale>();

    internal void MarkHardDelete(object entity) => _hardDeletes.Add(entity);

    internal bool IsHardDelete(object entity) => _hardDeletes.Contains(entity);

    /// <inheritdoc />
    /// <summary>
    /// <c>checks.strictness_rank(verdicts)</c> (change 11) for queries: how strict the strictest verdict of a finding is,
    /// smaller is stricter. Only translatable to SQL.
    /// </summary>
    [DbFunction("strictness_rank", Schema = "checks")]
    public static short StrictnessRank(System.Text.Json.JsonDocument verdicts) =>
        throw new NotSupportedException("checks.strictness_rank runs only in SQL.");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EshopGuardDb).Assembly);
        ApplyColumnDefaults(modelBuilder);
        ApplyQueryFilters(modelBuilder);
        SeedReferenceData(modelBuilder);
    }

    /// <summary>Database defaults shared by all tables: <c>uuidv7()</c>, <c>now()</c>, <c>false</c>, empty arrays.</summary>
    private static void ApplyColumnDefaults(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.Name == "Id" && property.ClrType == typeof(Guid))
                {
                    property.ValueGenerated = ValueGenerated.Never;
                    property.SetDefaultValueSql("uuidv7()");
                }
                else if (property.Name is nameof(IHasTimestamps.CreatedAt) or nameof(IHasTimestamps.UpdatedAt))
                {
                    property.SetDefaultValueSql("now()");
                }
                else if (property.ClrType == typeof(bool))
                {
                    property.SetDefaultValue(false);
                }
                else if (property.ClrType.IsArray && property.ClrType != typeof(byte[]))
                {
                    property.SetDefaultValueSql("'{}'");
                }
            }
        }
    }

    private void ApplyQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (typeof(ITenantOwned).IsAssignableFrom(clrType))
            {
                var tenantProperty = entityType.FindProperty("TenantId")
                    ?? throw new InvalidOperationException($"{clrType.Name} implements ITenantOwned but has no TenantId.");
                var e = Expression.Parameter(clrType, "e");
                var column = Expression.Call(typeof(EF), nameof(EF.Property), [tenantProperty.ClrType], e, Expression.Constant("TenantId"));
                Expression current = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
                if (tenantProperty.ClrType != typeof(Guid))
                {
                    current = Expression.Convert(current, tenantProperty.ClrType);
                }

                modelBuilder.Entity(clrType).HasQueryFilter(TenantFilter, Expression.Lambda(Expression.Equal(column, current), e));
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                var e = Expression.Parameter(clrType, "e");
                var deletedAt = Expression.Call(typeof(EF), nameof(EF.Property), [typeof(DateTimeOffset?)], e, Expression.Constant(nameof(ISoftDeletable.DeletedAt)));
                modelBuilder.Entity(clrType).HasQueryFilter(SoftDeleteFilter,
                    Expression.Lambda(Expression.Equal(deletedAt, Expression.Constant(null, typeof(DateTimeOffset?))), e));
            }
        }
    }

    /// <summary>Base rows of <c>ref.locales</c> and <c>ref.markets</c> (decided 1. 10. 2026: SK in EUR, CZ in CZK).</summary>
    private static void SeedReferenceData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Locale>().HasData(
            new Locale { Code = "sk", Name = "Slovenčina", Enabled = false, CreatedAt = SeedTime, UpdatedAt = SeedTime },
            new Locale { Code = "cs", Name = "Čeština", Enabled = false, CreatedAt = SeedTime, UpdatedAt = SeedTime });
        modelBuilder.Entity<Market>().HasData(
            new Market
            {
                Code = "sk", CountryCode = "SK", DefaultLocale = "sk-SK", UiLocales = ["sk"], Jurisdiction = "sk", Currency = "EUR",
                WebStatus = MarketWebStatus.Hidden, ChecksStatus = MarketChecksStatus.Full, CreatedAt = SeedTime, UpdatedAt = SeedTime,
            },
            new Market
            {
                Code = "cz", CountryCode = "CZ", DefaultLocale = "cs-CZ", UiLocales = ["cs"], Jurisdiction = "cz", Currency = "CZK",
                WebStatus = MarketWebStatus.Hidden, ChecksStatus = MarketChecksStatus.Limited, CreatedAt = SeedTime, UpdatedAt = SeedTime,
            });
    }
}
