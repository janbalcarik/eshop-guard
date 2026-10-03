using EshopGuard.Application.Shops.Scope;
using EshopGuard.Billing.Jobs;
using EshopGuard.Billing.Stripe;
using EshopGuard.Billing.Subscriptions;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Runs;
using Microsoft.Extensions.DependencyInjection;
using static EshopGuard.Billing.Tests.BillingTestHost;
using static EshopGuard.Billing.Tests.StripeScenario;

namespace EshopGuard.Billing.Tests;

/// <summary>
/// Changes of the tier, the volume discount and the price list of running subscriptions (change 12, group 8; requirements
/// „Změna pásma a slevy od dalšího období“ and „Změna ceníku u běžících předplatných“): the planned change with its date and
/// e-mail, the Subscription Schedule in <see cref="FakeStripeGateway"/>, and the change applied by the event of Stripe.
/// </summary>
[Trait("Category", "Db")]
public sealed class SubscriptionChangeTests
{
    private static readonly DateTimeOffset Old = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Renewal = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(20, 2026, 12)]
    [InlineData(28, 2027, 1)]
    public async Task OutgrownTier_TakesEffectFromTheFirstPeriodAfterTheNotice_WithAnEmail_AndAPhaseOfTheSchedule(int day, int year, int month)
    {
        await using var host = await CreateAsync(new DateTimeOffset(2026, 11, day, 8, 0, 0, TimeSpan.Zero));
        var (account, list) = await AccountAsync(host);
        var subscription = await SubscribedAsync(host, account, list, "t2000", 19m, Renewal);
        var effective = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        var date = $"{year}-{month:00}-01";
        host.Products.Set(subscription.ShopId, 2150);

        var results = await EvaluateAsync(host, account.TenantId, subscription.ShopId);

        var planned = Assert.Single(results, r => r.Outcome == PlanResult.Scheduled);
        Assert.Equal((effective, "tier_up"), (planned.EffectiveAt!.Value, planned.Code));
        Assert.Equal(new object?[] { "tier", "notified", effective.UtcDateTime, "tier_up", 19m, 29m, "t5000", null }, Assert.Single(await ChangesAsync(subscription.Id)));
        Assert.Equal("2150", await AdminScalarAsync<string>("""SELECT "to"->>'products' FROM billing.subscription_changes WHERE subscription_id = $1""", subscription.Id));
        Assert.Equal(new object?[] { "19.00", "29.00", date, "2150" }, Assert.Single(await AdminRowsAsync(
            "SELECT params->>'oldAmount', params->>'newAmount', params->>'date', params->>'products' FROM iam.notifications WHERE user_id = $1 AND kind = 'tier_change'",
            account.Owner)));
        Assert.Equal(1L, await EmailsAsync(account, "tier_change"));
        Assert.Equal(1L, await ComposeJobsAsync(subscription.Id));

        Assert.Equal(ComposeOutcome.Composed, (await ComposeAsync(host, account, subscription)).Outcome);
        Assert.Equal(
            new (DateTimeOffset, DateTimeOffset?, string, string?)[] { (subscription.PeriodStart, effective, Price(list, "t2000"), null), (effective, null, Price(list, "t5000"), null) },
            Phases(host, subscription));
        Assert.Equal(ComposeOutcome.Unchanged, (await ComposeAsync(host, account, subscription)).Outcome);
        Assert.Equal(1, host.Stripe.Count(nameof(IStripeGateway.CreateScheduleFromSubscriptionAsync)));
        Assert.Equal(1, host.Stripe.Count(nameof(IStripeGateway.UpdateScheduleAsync)));
        Assert.Equal(Price(list, "t2000"), host.Stripe.Subscriptions[subscription.StripeId].PriceId);
        Assert.Equal(host.Stripe.Subscriptions[subscription.StripeId].ScheduleId,
            await AdminScalarAsync<string>("SELECT stripe_schedule_id FROM billing.subscriptions WHERE id = $1", subscription.Id));
    }

    [Fact]
    public async Task ProductsBackBeforeTheDate_CancelTheChange_ReleaseTheSchedule_AndTellTheCustomer()
    {
        await using var host = await CreateAsync(new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero));
        var (account, list) = await AccountAsync(host);
        var subscription = await SubscribedAsync(host, account, list, "t2000", 19m, Renewal);
        host.Products.Set(subscription.ShopId, 2150);
        await EvaluateAsync(host, account.TenantId, subscription.ShopId);
        await ComposeAsync(host, account, subscription);
        var schedule = host.Stripe.Subscriptions[subscription.StripeId].ScheduleId!;

        host.Time.SetUtcNow(new DateTimeOffset(2026, 11, 25, 8, 0, 0, TimeSpan.Zero));
        host.Products.Set(subscription.ShopId, 1900);
        var results = await EvaluateAsync(host, account.TenantId, subscription.ShopId);

        Assert.Contains(results, r => r.Outcome == PlanResult.Canceled);
        Assert.Equal(new object?[] { "tier", "canceled", Renewal.UtcDateTime, "not_needed", 19m, 29m, "t5000", null }, Assert.Single(await ChangesAsync(subscription.Id)));
        Assert.Equal("2026-12-01", await AdminScalarAsync<string>(
            "SELECT params->>'date' FROM iam.notifications WHERE user_id = $1 AND kind = 'price_change_canceled'", account.Owner));
        Assert.Equal(1L, await EmailsAsync(account, "price_change_canceled"));

        Assert.Equal(ComposeOutcome.Released, (await ComposeAsync(host, account, subscription)).Outcome);
        Assert.Equal("released", host.Stripe.Schedules[schedule].Status);
        Assert.Equal((Price(list, "t2000"), null), (host.Stripe.Subscriptions[subscription.StripeId].PriceId, host.Stripe.Subscriptions[subscription.StripeId].ScheduleId));
        Assert.Null(await AdminScalarAsync<string>("SELECT stripe_schedule_id FROM billing.subscriptions WHERE id = $1", subscription.Id));
        Assert.Equal(ComposeOutcome.Unchanged, (await ComposeAsync(host, account, subscription)).Outcome);
        Assert.Equal(1, host.Stripe.Count(nameof(IStripeGateway.ReleaseScheduleAsync)));
    }

    [Fact]
    public async Task PartialCount_PlansOnlyAHigherTier_AndACountByAgreementOnlyRaisesAnAlert()
    {
        await using var host = await CreateAsync(new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero));
        var (account, list) = await AccountAsync(host);
        var subscription = await SubscribedAsync(host, account, list, "t2000", 19m, Renewal);

        host.Products.Set(subscription.ShopId, 300, lowerBound: true);
        await EvaluateAsync(host, account.TenantId, subscription.ShopId);
        host.Products.Set(subscription.ShopId, 25000);
        await EvaluateAsync(host, account.TenantId, subscription.ShopId);

        Assert.Empty(await ChangesAsync(subscription.Id));
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.audit_log WHERE action = 'billing.alert.tier_custom' AND entity_id = $1", subscription.Id.ToString("D")));
        Assert.Equal(0L, await ComposeJobsAsync(subscription.Id));

        host.Products.Set(subscription.ShopId, 2150, lowerBound: true);
        await EvaluateAsync(host, account.TenantId, subscription.ShopId);

        Assert.Equal("t5000", (string?)Assert.Single(await ChangesAsync(subscription.Id))[6]);
    }

    [Fact]
    public async Task ChangeOnItsDate_WaitsForStripe_AndOneMissedForADay_IsCanceledWithAnAlert()
    {
        await using var host = await CreateAsync(new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero));
        var (account, list) = await AccountAsync(host);
        var subscription = await SubscribedAsync(host, account, list, "t2000", 19m, Renewal);
        host.Products.Set(subscription.ShopId, 2150);
        await EvaluateAsync(host, account.TenantId, subscription.ShopId);
        await ComposeAsync(host, account, subscription);

        host.Time.SetUtcNow(Renewal.AddMinutes(10));
        var waiting = await ComposeAsync(host, account, subscription);
        host.Time.SetUtcNow(Renewal.AddHours(25));
        var missed = await ComposeAsync(host, account, subscription);

        Assert.Equal(ComposeOutcome.Retry(SubscriptionScheduleComposer.ChangePending), waiting);
        Assert.Equal(ComposeOutcome.Released, missed.Outcome);
        Assert.Equal(new object?[] { "canceled", "missed" }, Assert.Single(await AdminRowsAsync(
            "SELECT status, reason_code FROM billing.subscription_changes WHERE subscription_id = $1", subscription.Id)));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'billing.alert.change_missed' AND tenant_id = $1", account.TenantId));
    }

    [Fact]
    public async Task EndOfTheFirstShop_TakesTheDiscountOfTheThird_FromItsNextPeriod_WithAnEmail()
    {
        await using var host = await CreateAsync(new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero), configure: s => s.AddRunService());
        var market = await BillingData.MarketAsync();
        var list = await BillingData.PriceListAsync(market, "published", validFrom: Old, discount: (3, 10m));
        var account = await AccountOfAsync(host, market);
        var coupon = $"coupon_{BillingData.Key(list)}_3";
        var first = await SubscribedAsync(host, account, list, "t2000", 19m, Renewal, ordinal: 1);
        var second = await SubscribedAsync(host, account, list, "t2000", 19m, Renewal, ordinal: 2);
        var third = await SubscribedAsync(host, account, list, "t2000", 19m, Renewal, ordinal: 3, coupon: coupon, percent: 10m);
        var now = host.Time.GetUtcNow();
        host.Stripe.Subscriptions[first.StripeId] = host.Stripe.Subscriptions[first.StripeId] with { Status = "canceled", CanceledAt = now, EndedAt = now };

        await ProcessAsync(host, await StoreAsync(host, "customer.subscription.deleted", "subscription", first.StripeId));

        Assert.Equal([1, 1, 2], (await AdminRowsAsync(
            "SELECT shop_ordinal FROM billing.subscriptions WHERE id = ANY($1) ORDER BY created_at", new[] { first.Id, second.Id, third.Id })).Select(r => (int)r[0]!));
        Assert.Empty(await ChangesAsync(second.Id));
        var change = Assert.Single(await ChangesAsync(third.Id));
        Assert.Equal(new object?[] { "discount", "notified", Renewal.UtcDateTime, "discount_lost", 19m, 19m, "t2000", null }, change);
        Assert.Equal(new object?[] { "17.10", "19.00", "2026-12-01", "false" }, Assert.Single(await AdminRowsAsync(
            "SELECT params->>'oldAmount', params->>'newAmount', params->>'date', params->>'decrease' FROM iam.notifications WHERE user_id = $1 AND kind = 'price_change'",
            account.Owner)));
        Assert.Equal(1L, await EmailsAsync(account, "price_change"));
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.audit_log WHERE action = 'subscription.change_scheduled' AND tenant_id = $1 AND data->>'kind' = 'discount'", account.TenantId));

        Assert.Equal(ComposeOutcome.Composed, (await ComposeAsync(host, account, third)).Outcome);
        Assert.Equal(
            new (DateTimeOffset, DateTimeOffset?, string, string?)[] { (third.PeriodStart, Renewal, Price(list, "t2000"), coupon), (Renewal, null, Price(list, "t2000"), null) },
            Phases(host, third));
    }

    [Theory]
    [InlineData(false, 2027, 4, "2027-04-15")]
    [InlineData(true, 2028, 10, "2028-09-30")]
    public async Task MoreExpensivePriceList_AfterItsNotice_AndAfterTheLockedPriceOfAFounder(bool founder, int year, int month, string lockedUntil)
    {
        var activation = new DateTimeOffset(2027, 3, 1, 0, 0, 0, TimeSpan.Zero);
        await using var host = await CreateAsync(activation.AddMinutes(5));
        var market = await BillingData.MarketAsync();
        var current = await BillingData.PriceListAsync(market, "published", validFrom: Old);
        var next = await BillingData.PriceListAsync(market, "published", validFrom: activation, monthly: new Dictionary<string, decimal> { ["t5000"] = 32m });
        var account = await AccountOfAsync(host, market, founder ? new DateTimeOffset(2028, 9, 30, 0, 0, 0, TimeSpan.Zero) : null);
        var subscription = await SubscribedAsync(host, account, current, "t5000", 29m, new DateTimeOffset(2027, 3, 15, 0, 0, 0, TimeSpan.Zero));
        var effective = new DateTimeOffset(year, month, 15, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(1, await TransferAsync(host, next));
        Assert.Equal(1, await TransferAsync(host, next));

        Assert.Equal(new object?[] { "price_list", "notified", effective.UtcDateTime, "price_increase", 29m, 32m, "t5000", null },
            Assert.Single(await ChangesAsync(subscription.Id)));
        Assert.Equal(new object?[] { "29.00", "32.00", $"{year}-{month:00}-15", founder ? "true" : "false", lockedUntil, "EUR" }, Assert.Single(await AdminRowsAsync(
            """
            SELECT params->>'oldAmount', params->>'newAmount', params->>'date', params->>'founder', params->>'lockedUntil', params->>'currency'
            FROM iam.notifications WHERE user_id = $1 AND kind = 'price_change'
            """, account.Owner)));
        Assert.Equal(1L, await EmailsAsync(account, "price_change"));

        Assert.Equal(ComposeOutcome.Composed, (await ComposeAsync(host, account, subscription)).Outcome);
        Assert.Equal(
            new (DateTimeOffset, DateTimeOffset?, string, string?)[] { (subscription.PeriodStart, effective, Price(current, "t5000"), null), (effective, null, Price(next, "t5000"), null) },
            Phases(host, subscription));
    }

    [Fact]
    public async Task CheaperPriceList_FromItsValidity_WithoutNotice()
    {
        var activation = new DateTimeOffset(2027, 3, 1, 0, 0, 0, TimeSpan.Zero);
        await using var host = await CreateAsync(activation.AddMinutes(5));
        var market = await BillingData.MarketAsync();
        var current = await BillingData.PriceListAsync(market, "published", validFrom: Old);
        var next = await BillingData.PriceListAsync(market, "published", validFrom: activation, monthly: new Dictionary<string, decimal> { ["t500"] = 8m });
        var account = await AccountOfAsync(host, market);
        var subscription = await SubscribedAsync(host, account, current, "t500", 9m, new DateTimeOffset(2027, 3, 5, 0, 0, 0, TimeSpan.Zero));
        var effective = new DateTimeOffset(2027, 3, 5, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(1, await TransferAsync(host, next));

        Assert.Equal(new object?[] { "price_list", "notified", effective.UtcDateTime, "price_decrease", 9m, 8m, "t500", null }, Assert.Single(await ChangesAsync(subscription.Id)));
        Assert.Equal(new object?[] { "9.00", "8.00", "2027-03-05", "true" }, Assert.Single(await AdminRowsAsync(
            "SELECT params->>'oldAmount', params->>'newAmount', params->>'date', params->>'decrease' FROM iam.notifications WHERE user_id = $1 AND kind = 'price_change'",
            account.Owner)));
    }

    [Fact]
    public async Task HigherTierAndNewPriceList_MakeThreePhases_ComposedOnce_AndTheEventOfStripeAppliesTheFirst()
    {
        await using var host = await CreateAsync(new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero), configure: s => s.AddRunService());
        var market = await BillingData.MarketAsync();
        var current = await BillingData.PriceListAsync(market, "published", validFrom: Old);
        var january = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var next = await BillingData.PriceListAsync(market, "published", validFrom: january, monthly: new Dictionary<string, decimal> { ["t5000"] = 32m });
        await AdminAsync("UPDATE billing.price_lists SET published_at = $2 WHERE id = $1", next, host.Time.GetUtcNow().AddDays(-15));
        var account = await AccountOfAsync(host, market);
        var subscription = await SubscribedAsync(host, account, current, "t2000", 19m, Renewal);
        host.Products.Set(subscription.ShopId, 2150);

        await EvaluateAsync(host, account.TenantId, subscription.ShopId);
        Assert.Equal(1, await TransferAsync(host, next));

        Assert.Equal(
            [
                new object?[] { "tier", "notified", Renewal.UtcDateTime, "tier_up", 19m, 29m, "t5000", null },
                new object?[] { "price_list", "notified", january.UtcDateTime, "price_increase", 29m, 32m, "t5000", null },
            ],
            await ChangesAsync(subscription.Id));
        Assert.Equal(ComposeOutcome.Composed, (await ComposeAsync(host, account, subscription)).Outcome);
        await EvaluateAsync(host, account.TenantId, subscription.ShopId);
        Assert.Equal(ComposeOutcome.Unchanged, (await ComposeAsync(host, account, subscription)).Outcome);
        Assert.Equal(
            new (DateTimeOffset, DateTimeOffset?, string, string?)[]
            {
                (subscription.PeriodStart, Renewal, Price(current, "t2000"), null),
                (Renewal, january, Price(current, "t5000"), null),
                (january, null, Price(next, "t5000"), null),
            },
            Phases(host, subscription));
        Assert.Equal(1, host.Stripe.Count(nameof(IStripeGateway.UpdateScheduleAsync)));

        host.Time.SetUtcNow(Renewal.AddMinutes(10));
        host.Stripe.Subscriptions[subscription.StripeId] = host.Stripe.Subscriptions[subscription.StripeId] with
        {
            PriceId = Price(current, "t5000"), CurrentPeriodStart = Renewal, CurrentPeriodEnd = january,
        };
        await ProcessAsync(host, await StoreAsync(host, "customer.subscription.updated", "subscription", subscription.StripeId));

        var applied = await AdminRowsAsync(
            "SELECT kind, status, applied_at FROM billing.subscription_changes WHERE subscription_id = $1 ORDER BY effective_at", subscription.Id);
        Assert.Equal(new object?[] { "tier", "applied", Renewal.AddMinutes(10).UtcDateTime }, applied[0]);
        Assert.Equal(new object?[] { "price_list", "notified", null }, applied[1]);
        Assert.Equal(new object?[] { "t5000", 29m, Price(current, "t5000") }, Assert.Single(await AdminRowsAsync(
            "SELECT tier_code, unit_price, stripe_price_id FROM billing.subscriptions WHERE id = $1", subscription.Id)));
        Assert.Equal(new object?[] { Price(current, "t2000"), Price(current, "t5000"), 1 }, Assert.Single(await AdminRowsAsync(
            """
            SELECT data->>'from_price', data->>'to_price', jsonb_array_length(data->'changes')
            FROM ops.audit_log WHERE action = 'subscription.price_changed' AND entity_id = $1
            """, subscription.Id.ToString("D"))));

        Assert.Equal(ComposeOutcome.Composed, (await ComposeAsync(host, account, subscription)).Outcome);
        Assert.Equal(
            new (DateTimeOffset, DateTimeOffset?, string, string?)[] { (Renewal, january, Price(current, "t5000"), null), (january, null, Price(next, "t5000"), null) },
            Phases(host, subscription));
    }

    private sealed record Account(Guid TenantId, string CustomerId, Guid Owner);

    private sealed record Subscribed(Guid Id, Guid ShopId, string StripeId, DateTimeOffset PeriodStart);

    private static async Task<(Account Account, Guid List)> AccountAsync(BillingTestHost host)
    {
        var market = await BillingData.MarketAsync();
        var list = await BillingData.PriceListAsync(market, "published", validFrom: Old);
        return (await AccountOfAsync(host, market), list);
    }

    /// <summary>A tenant of the market with its customer of Stripe and an owner (who gets the notifications and e-mails of billing).</summary>
    private static async Task<Account> AccountOfAsync(BillingTestHost host, string market, DateTimeOffset? founderUntil = null)
    {
        var tenant = await BillingData.TenantAsync(market, founderUntil: founderUntil);
        var customer = host.Stripe.NewId("cus");
        host.Stripe.Customers[customer] = new StripeCustomerState(customer, null, [], false, new Dictionary<string, string>());
        await AdminAsync("UPDATE iam.tenants SET stripe_customer_id = $2 WHERE id = $1", tenant, customer);
        return new Account(tenant, customer, await MemberAsync(tenant, "owner"));
    }

    /// <summary>An active monthly subscription of a new e-shop renewed at <paramref name="periodEnd"/>, the same in Stripe.</summary>
    private static async Task<Subscribed> SubscribedAsync(
        BillingTestHost host, Account account, Guid list, string tier, decimal unitPrice, DateTimeOffset periodEnd, int ordinal = 1, string? coupon = null, decimal? percent = null)
    {
        var shop = await BillingData.ShopAsync(account.TenantId);
        var stripeId = host.Stripe.NewId("sub");
        var price = Price(list, tier);
        var id = await BillingData.SubscriptionAsync(account.TenantId, shop, list, tier, unitPrice, periodEnd, "active", price, stripeId);
        await AdminAsync("UPDATE billing.subscriptions SET shop_ordinal = $2 WHERE id = $1", id, ordinal);
        if (coupon is not null)
        {
            await AdminAsync("UPDATE billing.subscriptions SET stripe_coupon_id = $2, discount_percent = $3 WHERE id = $1", id, coupon, percent);
        }

        var start = periodEnd.AddMonths(-1);
        host.Stripe.Subscriptions[stripeId] = new StripeSubscriptionState(stripeId, account.CustomerId, "active", price, coupon, null, start, periodEnd, false, null, null, null,
            null, null, null, start, new Dictionary<string, string> { ["tenant_id"] = account.TenantId.ToString("D"), ["shop_id"] = shop.ToString("D") });
        return new Subscribed(id, shop, stripeId, start);
    }

    private static string Price(Guid list, string tier) => $"price_{BillingData.Key(list)}_{tier}_monthly";

    private static Task<IReadOnlyList<PlanResult>> EvaluateAsync(BillingTestHost host, Guid tenantId, Guid shopId) =>
        host.RunAsync(s => s.GetServices<IJobHandler>().OfType<EvaluateTiersHandler>().Single().RunAsync(tenantId, shopId, Ct));

    private static Task<int> TransferAsync(BillingTestHost host, Guid priceListId) =>
        host.RunAsync(s => s.GetServices<IJobHandler>().OfType<SchedulePriceListTransferHandler>().Single().RunAsync(priceListId, Ct));

    private static Task<ComposeOutcome> ComposeAsync(BillingTestHost host, Account account, Subscribed subscription) =>
        host.RunAsync(s => s.GetRequiredService<SubscriptionScheduleComposer>().ComposeAsync(account.TenantId, subscription.Id, Ct));

    /// <summary>kind, status, effective_at, reason_code, the unit price before and after, the tier and the coupon after.</summary>
    private static Task<List<object?[]>> ChangesAsync(Guid subscriptionId) => AdminRowsAsync(
        """
        SELECT kind, status, effective_at, reason_code, ("from"->>'unit_price')::numeric, ("to"->>'unit_price')::numeric, "to"->>'tier_code', "to"->>'coupon_id'
        FROM billing.subscription_changes WHERE subscription_id = $1 ORDER BY effective_at, created_at
        """, subscriptionId);

    private static Task<long> EmailsAsync(Account account, string template) => AdminScalarAsync<long>(
        "SELECT count(*) FROM ops.outbox WHERE tenant_id = $1 AND kind = 'email' AND payload->>'template' = $2", account.TenantId, template);

    private static Task<long> ComposeJobsAsync(Guid subscriptionId) => AdminScalarAsync<long>(
        "SELECT count(*) FROM ops.jobs WHERE kind = 'billing.compose_schedule' AND dedupe_key LIKE $1", $"schedule:{subscriptionId:N}:%");

    /// <summary>The phases of the active schedule of the subscription in Stripe: start, end, Price, coupon.</summary>
    private static (DateTimeOffset, DateTimeOffset?, string, string?)[] Phases(BillingTestHost host, Subscribed subscription) =>
        host.Stripe.Schedules.Values.Single(s => s.SubscriptionId == subscription.StripeId && s.Status == "active").Phases
            .Select(p => (p.StartDate, p.EndDate, p.PriceId, p.CouponId)).ToArray();
}

/// <summary>The pure rules of group 8: the counted products of a scope and the results of the job of composition.</summary>
public sealed class SubscriptionChangeRulesTests
{
    [Fact]
    public void CountedProducts_AreTheAnalyzedTimesTheMarketsOfEachVersion_ElseThePriceBasisOfTheScope()
    {
        var both = Scope([Version("sk", ["sk", "cz"])], priceCount: 900);
        var uneven = Scope([Version("sk", ["sk", "cz"]), Version("hu", ["hu"])], priceCount: 900);
        var unknown = Scope([Version("sk", ["sk"])], priceCount: null);

        Assert.Equal(new CountedProducts(2400), CountedProductsReader.From(both, 1200, partial: false));
        Assert.Equal(new CountedProducts(2400, LowerBound: true), CountedProductsReader.From(both, 1200, partial: true));
        Assert.Equal(new CountedProducts(900), CountedProductsReader.From(uneven, 1200, partial: false));
        Assert.Equal(new CountedProducts(900), CountedProductsReader.From(both, null, partial: false));
        Assert.Null(CountedProductsReader.From(unknown, null, partial: false));
    }

    [Fact]
    public void ComposeOutcome_WaitsForStripe_RetriesTransientErrors_AndFailsTheRest()
    {
        Assert.Equal(new JobResult.Retry(SubscriptionScheduleComposer.ChangePending, ComposeScheduleHandler.WaitForStripe),
            ComposeScheduleHandler.Map(ComposeOutcome.Retry(SubscriptionScheduleComposer.ChangePending)));
        Assert.Equal(new JobResult.Retry(SubscriptionScheduleComposer.SubscriptionStale, ComposeScheduleHandler.WaitForStripe),
            ComposeScheduleHandler.Map(ComposeOutcome.Retry(SubscriptionScheduleComposer.SubscriptionStale)));
        Assert.Equal(new JobResult.Retry("stripe.api_error"), ComposeScheduleHandler.Map(ComposeOutcome.Retry("stripe.api_error")));
        Assert.Equal(new JobResult.Fail(SubscriptionTimeline.TierUnavailable), ComposeScheduleHandler.Map(ComposeOutcome.Fail(SubscriptionTimeline.TierUnavailable)));
        Assert.Same(JobResult.Done, ComposeScheduleHandler.Map(new ComposeOutcome(ComposeOutcome.Composed)));
        Assert.Same(JobResult.Done, ComposeScheduleHandler.Map(new ComposeOutcome(ComposeOutcome.Unchanged)));
    }

    private static ScopeCheckedVersion Version(string language, IReadOnlyList<string> markets) =>
        new(language, $"https://bylinkovo.test/{language}/", language == "sk", null, null, markets, markets, "market_language");

    private static ShopScope Scope(IReadOnlyList<ScopeCheckedVersion> versions, int? priceCount) =>
        new([.. versions.SelectMany(v => v.Markets)], versions, [], [], [], null, null, null, [], "hash", priceCount is null ? null : PriceUnits.Products, priceCount);
}
