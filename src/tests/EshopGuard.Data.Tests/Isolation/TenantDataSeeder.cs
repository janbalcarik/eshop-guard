using System.Net;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Content;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using static EshopGuard.Data.Tests.Isolation.TestTenants;

namespace EshopGuard.Data.Tests.Isolation;

/// <summary>Ids of the rows seeded for one tenant.</summary>
internal sealed record SeededTenant(
    TestTenant Tenant,
    Guid ShopId,
    Guid RunId,
    Guid PageId,
    Guid PageVersionId,
    Guid FindingId,
    Guid FixGroupId,
    Guid FixProposalId,
    Guid EvidenceId,
    Guid SubscriptionId,
    IReadOnlyCollection<string> Tables);

/// <summary>
/// One valid row in each of the 43 tenant tables for one tenant (e-shop <c>vegis.sk</c>, base path <c>/</c>), references
/// only inside the tenant, written through EF as <c>eshopguard_app</c> with the tenant set.
/// </summary>
internal static class TenantDataSeeder
{
    public static async Task<SeededTenant> SeedAsync(TestTenant tenant, Guid ruleSetId, Guid priceListId, CancellationToken ct)
    {
        await using var db = TestDatabase.CreateDb("App", tenant.TenantId);
        var tables = new HashSet<string>(StringComparer.Ordinal);
        var now = DateTimeOffset.UtcNow;

        // Stage 1: e-shop and things that hang only on it.
        var shop = new Shop { Domain = "vegis.sk", BaseUrl = "https://vegis.sk/", BasePath = "/", HomeCountry = "sk", Platform = ShopPlatform.Shoptet, SourceMode = ShopSourceMode.Web, Status = ShopStatus.Active, Modules = ["eco", "ucp"] };
        var evidence = new EvidenceItem { ClaimText = "Bio", SubjectKind = EvidenceSubjectKind.Brand, Kind = EvidenceKind.Certificate, Source = EvidenceSource.Upload, Status = EvidenceStatus.Valid, CreatedBy = tenant.UserId };
        db.AddRange(shop, evidence);
        db.Add(new Membership { UserId = tenant.UserId, Role = MembershipRole.Owner });
        db.Add(new Invitation { Email = $"invite-{Guid.NewGuid():N}@example.invalid", Role = MembershipRole.Editor, TokenHash = Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray(), ExpiresAt = now.AddDays(7), InvitedBy = tenant.UserId });
        db.Add(new PaymentMethod { StripePaymentMethodId = "pm_" + Guid.NewGuid().ToString("N"), Brand = "visa", Last4 = "4242", ExpMonth = 8, ExpYear = 2028, IsDefault = true });
        db.Add(new JevAnswer { CacheKey = "sha256:" + Guid.NewGuid().ToString("N"), Response = Json("{\"probabilities\":[0.1]}"), Model = "jev-test" });
        db.Add(new SieveAnswer { CacheKey = "sha256:" + Guid.NewGuid().ToString("N"), Response = Json("{\"answers\":{}}"), Model = "jev-test" });
        db.Add(new RewriteCacheEntry { Key = "rw:" + Guid.NewGuid().ToString("N"), Answer = Json("{}"), Model = "gpt-test" });
        db.Add(new OutboxMessage { Kind = OutboxKind.Email, Payload = Json("{}") });
        db.Add(new AuditLogEntry { At = now, ActorUserId = tenant.UserId, ActorKind = AuditActorKind.User, Action = "test.seed", Ip = IPAddress.Loopback });
        await db.ExecuteInTenantTransactionAsync(() => db.SaveChangesAsync(ct), ct);

        // Stage 2: run, profile, connector, order.
        var run = new Run { ShopId = shop.Id, Kind = RunKind.FullAnalysis, Trigger = RunTrigger.User, Status = RunStatus.Finished, Priority = 2, Jurisdictions = ["sk"], Modules = ["eco"] };
        var profile = new PageProfile { ShopId = shop.Id, Number = 1, PromptVersion = "profile-test", Model = "gpt-test", Regions = Json("[]"), SampleUrls = Json("[]") };
        var connector = new Connector { ShopId = shop.Id, Platform = ShopPlatform.Shoptet, Status = ConnectorStatus.Connected, Access = ConnectorAccess.ReadWrite, Scopes = ["products"] };
        var fixGroup = new FixGroup { ShopId = shop.Id, Kind = FixGroupKind.RepeatedText, Status = FixGroupStatus.Draft, PageCount = 1 };
        var subscription = new Subscription { ShopId = shop.Id, Status = SubscriptionStatus.Active, Interval = BillingInterval.Month, PriceListId = priceListId, TierCode = "s", UnitPrice = 29m };
        db.AddRange(run, profile, connector, fixGroup, subscription);
        db.Add(new ShopVerification { ShopId = shop.Id, Method = VerificationMethod.Meta, Token = "token", Status = ShopVerificationStatus.Verified });
        db.Add(new Feed { ShopId = shop.Id, Url = "https://vegis.sk/feed.xml", Format = FeedFormat.Heureka });
        db.Add(new ShopFact { ShopId = shop.Id, Topic = "packaging", Text = "Recyklovateľný obal", CreatedBy = tenant.UserId });
        db.Add(new NotificationSetting { UserId = tenant.UserId, ShopId = shop.Id, EmailNewViolation = true });
        db.Add(new Notification { UserId = tenant.UserId, ShopId = shop.Id, Kind = "run.finished" });
        db.Add(new Schedule { ShopId = shop.Id, Kind = ScheduleKind.Nightly, NextRunAt = now.AddDays(1) });
        await db.ExecuteInTenantTransactionAsync(() => db.SaveChangesAsync(ct), ct);

        // Stage 3: page and its version, run-dependent rows, order.
        var page = new Page { ShopId = shop.Id, Url = "https://vegis.sk/sampon", UrlHash = Random.Shared.NextInt64(), Source = PageSource.Crawl, Status = PageStatus.Active, FirstSeenAt = now, ProfileId = profile.Id, RotationBucket = 3 };
        var order = new Order { ShopId = shop.Id, Kind = OrderKind.AnalysisWithTrial, PriceListId = priceListId, AmountNet = 100m, VatRate = 23m, VatAmount = 23m, AmountGross = 123m, Currency = "EUR", Status = OrderStatus.Paid, RunId = run.Id };
        db.AddRange(page, order);
        db.Add(new ShopMarket { ShopId = shop.Id, CountryCode = "sk", IsHome = true, Status = ShopMarketStatus.Active, Source = MarketSource.Detected, DetectionRunId = run.Id });
        db.Add(new ShopLanguage { ShopId = shop.Id, Language = "sk", BaseUrl = "https://vegis.sk/", Source = LanguageSource.Hreflang, Status = ShopLanguageStatus.Active, TranslatedShare = 1f, SampleRunId = run.Id });
        db.Add(new ConnectorWebhook { ConnectorId = connector.Id, Event = "product:update", Status = "active" });
        db.Add(new ConnectorEvent { ConnectorId = connector.Id, ShopId = shop.Id, DedupeKey = Guid.NewGuid().ToString("N"), EventType = "product:update", Payload = Json("{}"), ReceivedAt = now, Status = "received" });
        db.Add(new RunEvent { RunId = run.Id, At = now, Level = "info", Code = "run.started" });
        db.Add(new RunScope { RunId = run.Id, ScopeKey = "https://vegis.sk/", BaseUrl = "https://vegis.sk/", Language = "sk", Robots = Json("{}"), Frontier = Json("{}"), Pace = Json("{}") });
        db.Add(new RunUrl { RunId = run.Id, ScopeKey = "https://vegis.sk/", UrlHash = 42, Url = "https://vegis.sk/sampon", Language = "sk", State = RunUrlState.Extracted, Queue = "product", Seq = 1 });
        db.Add(new SubscriptionChange { SubscriptionId = subscription.Id, Kind = SubscriptionChangeKind.Tier, EffectiveAt = now.AddMonths(1), Status = "scheduled" });
        await db.ExecuteInTenantTransactionAsync(() => db.SaveChangesAsync(ct), ct);

        var version = new PageVersion { ShopId = shop.Id, PageId = page.Id, RunId = run.Id, FetchedAt = now, SegmentHashes = [11, 22, 33], IsCurrent = true };
        var payment = new Payment { OrderId = order.Id, SubscriptionId = subscription.Id, AmountGross = 123m, Currency = "EUR", Status = PaymentStatus.Succeeded, PaidAt = now };
        db.AddRange(version, payment);
        await db.ExecuteInTenantTransactionAsync(() => db.SaveChangesAsync(ct), ct);

        // Stage 4: findings and fixes.
        var finding = new Finding
        {
            ShopId = shop.Id, RuleId = "eco.generic_claim", RuleSetId = ruleSetId, Module = "eco", Checkability = Checkability.Text, Severity = "high",
            Band = FindingBand.High, Scope = FindingScope.Segment, SegmentHash = 22, PageId = page.Id, Verdicts = Json("{}"), Status = FindingStatus.Open,
            Occurrences = 1, FirstRunId = run.Id, LastSeenRunId = run.Id,
        };
        var proposal = new FixProposal
        {
            ShopId = shop.Id, PageId = page.Id, PageVersionId = version.Id, GroupId = fixGroup.Id, Field = FixField.Description, OriginalText = "Ekologický šampón",
            ProposedText = "Šampón", RecheckStatus = RecheckStatus.Pending, Status = FixProposalStatus.Proposed, CreatedRunId = run.Id,
        };
        db.AddRange(finding, proposal);
        db.Add(new Publication { ShopId = shop.Id, ConnectorId = connector.Id, PageId = page.Id, Field = "description", NewValue = "Šampón", IdempotencyKey = Guid.NewGuid().ToString("N"), Status = PublicationStatus.Queued });
        db.Add(new Protocol { ShopId = shop.Id, Number = $"EG-{Guid.NewGuid():N}", PeriodFrom = new DateOnly(2026, 9, 1), PeriodTo = new DateOnly(2026, 9, 30), Locale = "sk" });
        db.Add(new Invoice { ShopId = shop.Id, PaymentId = payment.Id, Kind = InvoiceKind.Invoice, Buyer = Json("{}"), Items = Json("[]"), AmountNet = 100m, VatAmount = 23m, AmountGross = 123m, Currency = "EUR", EinvoiceStatus = EinvoiceStatus.NotRequired, Status = "issued", IssuedAt = now });
        db.Add(new PageChange { ShopId = shop.Id, PageId = page.Id, DetectedAt = now, Source = PageChangeSource.Crawl, ChangeKind = PageChangeKind.TextChanged, RunId = run.Id });
        await db.ExecuteInTenantTransactionAsync(() => db.SaveChangesAsync(ct), ct);

        db.Add(new FindingOccurrence { FindingId = finding.Id, PageId = page.Id, ShopId = shop.Id, BlockIndex = 0 });
        db.Add(new Question { ShopId = shop.Id, FindingId = finding.Id, Scope = QuestionScope.Finding, Code = "evidence", Status = QuestionStatus.Open, EvidenceId = evidence.Id });
        db.Add(new DecisionMemory { ShopId = shop.Id, SegmentHash = 22, NormalizedText = "ekologicky sampon", Decision = Decision.Replace, EvidenceId = evidence.Id, SourceProposalId = proposal.Id });
        db.Add(new EvidenceLink { EvidenceId = evidence.Id, ShopId = shop.Id, PageId = page.Id, FindingId = finding.Id });
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            CollectTables(db, tables);
            await db.SaveChangesAsync(ct);
        }, ct);

        return new SeededTenant(tenant, shop.Id, run.Id, page.Id, version.Id, finding.Id, fixGroup.Id, proposal.Id, evidence.Id, subscription.Id, tables);
    }

    /// <summary>Tables of every tenant entity the context has tracked (added in any stage).</summary>
    private static void CollectTables(DbContext db, HashSet<string> tables)
    {
        foreach (var entry in db.ChangeTracker.Entries())
        {
            tables.Add($"{entry.Metadata.GetSchema()}.{entry.Metadata.GetTableName()}");
        }
    }
}
