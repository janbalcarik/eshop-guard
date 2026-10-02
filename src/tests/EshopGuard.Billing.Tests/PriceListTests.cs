using System.Text.Json;
using EshopGuard.Application.Problems;
using EshopGuard.Billing.Jobs;
using EshopGuard.Billing.Pricing;
using EshopGuard.Billing.Stripe;
using EshopGuard.Jobs.Processing;
using Microsoft.Extensions.DependencyInjection;
using static EshopGuard.Billing.Tests.BillingTestHost;

namespace EshopGuard.Billing.Tests;

/// <summary>
/// Price lists in the database and in Stripe (change 12, tasks 3.2–3.5 and 3.8; requirements „Ceník v databázi po trzích a
/// měnách“ and „Synchronizace ceníku do Stripe“), run as the worker with <see cref="Shared.FakeStripeGateway"/>.
/// </summary>
[Trait("Category", "Db")]
public sealed class PriceListTests
{
    [Fact]
    public async Task Draft_CopiesTheActiveList_WithoutIdsOfStripe_AndIsAudited()
    {
        await using var host = await CreateAsync();
        var market = await BillingData.MarketAsync();
        var active = await BillingData.PriceListAsync(market, "published", discount: (3, 10m));
        var draft = Guid.CreateVersion7();
        var actor = await AdminScalarAsync<Guid>("SELECT id FROM iam.users LIMIT 1");

        await host.RunAsync(s => s.GetRequiredService<PriceListAdminService>().CreateDraftAsync(draft, market, "EUR", null, null, actor, Ct));

        var tiers = await AdminRowsAsync("SELECT code, analysis_price, stripe_price_monthly FROM billing.price_tiers WHERE price_list_id = $1 ORDER BY min_products", draft);
        Assert.Equal(["t500", "t2000", "t5000", "t20000", "custom"], tiers.Select(t => (string)t[0]!));
        Assert.All(tiers, t => Assert.Null(t[2]));
        Assert.Equal(10m, await AdminScalarAsync<decimal>("SELECT percent FROM billing.volume_discounts WHERE price_list_id = $1", draft));
        Assert.Equal("draft", await AdminScalarAsync<string>("SELECT status FROM billing.price_lists WHERE id = $1", draft));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'price_list.created' AND entity_id = $1 AND tenant_id IS NULL", draft.ToString("D")));
        Assert.NotEqual(active, draft);
    }

    [Fact]
    public async Task PublishedList_IsNotEditable_AndTiersMustNotHaveGaps()
    {
        await using var host = await CreateAsync();
        var market = await BillingData.MarketAsync();
        var published = await BillingData.PriceListAsync(market, "published");
        var draft = await BillingData.PriceListAsync(market);
        PriceTierInput[] gap = [new("a", 0, 500, 39, 9, 90), new("b", 502, null, 69, 19, 190)];

        var refused = await Assert.ThrowsAsync<DomainException>(() => host.RunAsync(s =>
            s.GetRequiredService<PriceListAdminService>().SetTiersAsync(published, [new("a", 0, null, 1, 1, null)], null, null, null, Ct)));
        var invalid = await Assert.ThrowsAsync<DomainException>(() => host.RunAsync(s =>
            s.GetRequiredService<PriceListAdminService>().SetTiersAsync(draft, gap, null, null, null, Ct)));

        Assert.Equal(BillingCodes.PriceListNotEditable, refused.Code);
        Assert.Equal(BillingCodes.PriceListTiersInvalid, invalid.Code);
        Assert.Equal("gap_or_overlap", invalid.Parameters["reason"]);
    }

    [Fact]
    public async Task NewTiersOfADraft_GetTheirLookupKeys()
    {
        await using var host = await CreateAsync();
        var market = await BillingData.MarketAsync();
        var draft = await BillingData.PriceListAsync(market);

        await host.RunAsync(s => s.GetRequiredService<PriceListAdminService>().SetTiersAsync(draft,
            [new("t500", 0, 500, 39, 9, 90), new("t2000", 501, 2000, 69, 21, 210), new("custom", 2001, null, null, null, null)], 45, 3m, null, Ct));

        var rows = await AdminRowsAsync("SELECT code, monitoring_monthly, lookup_key_monthly FROM billing.price_tiers WHERE price_list_id = $1 ORDER BY min_products", draft);
        Assert.Equal(21m, (decimal)rows[1][1]!);
        Assert.Equal($"{market}_eur_t2000_monthly", rows[1][2]);
        Assert.Null(rows[2][2]);
        Assert.Equal(45, await AdminScalarAsync<int>("SELECT notice_days FROM billing.price_lists WHERE id = $1", draft));
    }

    [Fact]
    public async Task Impact_40SubscriptionsGetMoreExpensive_FromTheFirstPeriodAfterTheNotice()
    {
        var now = new DateTimeOffset(2027, 3, 1, 9, 0, 0, TimeSpan.Zero);
        await using var host = await CreateAsync(now);
        var market = await BillingData.MarketAsync();
        var current = await BillingData.PriceListAsync(market, "published", validFrom: now.AddMonths(-6));
        var draft = await BillingData.PriceListAsync(market, validFrom: now, monthly: new Dictionary<string, decimal> { ["t2000"] = 21m });
        var tenant = await BillingData.TenantAsync(market);
        for (var i = 0; i < 40; i++)
        {
            var shop = await BillingData.ShopAsync(tenant);
            await BillingData.SubscriptionAsync(tenant, shop, current, "t2000", 19m, new DateTimeOffset(2027, 3, 15, 9, 0, 0, TimeSpan.Zero));
        }

        await host.RunAsync(s => s.GetRequiredService<PriceListAdminService>().ComputeImpactAsync(draft, Ct));

        var impact = JsonDocument.Parse((await AdminScalarAsync<string>("SELECT impact::text FROM billing.price_lists WHERE id = $1", draft))!).RootElement;
        Assert.Equal(40, impact.GetProperty("increase").GetInt32());
        Assert.Equal(0, impact.GetProperty("decrease").GetInt32());
        Assert.Equal(0, impact.GetProperty("unchanged").GetInt32());
        Assert.Equal(new DateTimeOffset(2027, 4, 15, 9, 0, 0, TimeSpan.Zero), impact.GetProperty("first_increase_at").GetDateTimeOffset());
        Assert.Equal(0L, host.Stripe.Calls.Count);
    }

    [Fact]
    public async Task Publish_CreatesTwelvePricesAndACoupon_AndMovesTheLookupKeysAtValidFrom()
    {
        var now = DateTimeOffset.UtcNow;
        await using var host = await CreateAsync(now);
        var market = await BillingData.MarketAsync();
        var previous = await BillingData.PriceListAsync(market, "published", validFrom: now.AddMonths(-3));
        var draft = await BillingData.PriceListAsync(market, discount: (3, 10m));
        await host.RunAsync(s => s.GetRequiredService<PriceListAdminService>().RequestPublishAsync(draft, now.AddDays(5), Guid.CreateVersion7(), null, Ct));

        var result = await host.RunAsync(s => s.GetRequiredService<StripeCatalogSync>().SyncAsync(draft, Ct));

        Assert.True(result.Published);
        Assert.Equal(12, host.Stripe.Prices.Values.Count(p => p.Metadata["price_list_id"] == draft.ToString("D")));
        var coupon = Assert.Single(host.Stripe.Coupons.Values, c => c.Metadata["price_list_id"] == draft.ToString("D"));
        Assert.Equal(10m, coupon.PercentOff);
        Assert.Equal(host.Stripe.Prices.Values.First(p => p.RecurringInterval == "month").ProductId, coupon.AppliesToProductId);
        Assert.NotEqual(host.Stripe.Prices.Values.First(p => p.RecurringInterval is null).ProductId, coupon.AppliesToProductId);
        Assert.All(host.Stripe.Prices.Values, p => Assert.Null(p.LookupKey));
        Assert.Equal(["published", "synced", "test"], (await AdminRowsAsync("SELECT status, sync_status, stripe_mode FROM billing.price_lists WHERE id = $1", draft))[0]);
        Assert.Equal(0, host.Stripe.Count(nameof(IStripeGateway.TransferLookupKeyAsync)));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE dedupe_key = $1 AND not_before > now() + interval '4 days'", $"price-activate:{draft:N}"));

        // valid_from: the activation moves the keys, points the market to the list and retires the previous one.
        host.Time.Advance(TimeSpan.FromDays(5));
        await AdminAsync("UPDATE billing.price_lists SET valid_from = now() - interval '1 minute' WHERE id = $1", draft);
        var activated = await host.RunAsync(s => s.GetRequiredService<StripeCatalogSync>().ActivateAsync(draft, Ct));

        Assert.True(activated.Published);
        Assert.Equal(12, host.Stripe.Count(nameof(IStripeGateway.TransferLookupKeyAsync)));
        Assert.Equal($"price_", host.Stripe.LookupKeys[$"{market}_eur_t2000_monthly"][..6]);
        Assert.Equal(draft, await AdminScalarAsync<Guid>("SELECT price_list_id FROM ref.markets WHERE code = $1", market));
        Assert.Equal("retired", await AdminScalarAsync<string>("SELECT status FROM billing.price_lists WHERE id = $1", previous));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE dedupe_key = $1", $"price-transfer:{draft:N}"));
    }

    [Fact]
    public async Task StripeFailsAfterFivePrices_TheListStaysADraft_AndTheRetryCreatesOnlyTheRest()
    {
        await using var host = await CreateAsync();
        var market = await BillingData.MarketAsync();
        var draft = await BillingData.PriceListAsync(market, discount: (3, 10m));
        host.Stripe.FailCreatePriceAfter = 5;

        var failed = await host.RunAsync(s => s.GetRequiredService<StripeCatalogSync>().SyncAsync(draft, Ct));

        Assert.False(failed.Published);
        Assert.True(failed.Transient);
        Assert.IsType<JobResult.Retry>(SyncPriceListHandler.Outcome(failed));
        Assert.Equal(["draft", "failed"], (await AdminRowsAsync("SELECT status, sync_status FROM billing.price_lists WHERE id = $1", draft))[0]);
        Assert.Equal(5, host.Stripe.Prices.Count);

        host.Stripe.FailCreatePriceAfter = null;
        var retried = await host.RunAsync(s => s.GetRequiredService<StripeCatalogSync>().SyncAsync(draft, Ct));

        Assert.True(retried.Published);
        Assert.Equal(12, host.Stripe.Prices.Count);
        Assert.Equal(12, host.Stripe.Calls.Where(c => c.Method == nameof(IStripeGateway.CreatePriceAsync)).Select(c => c.Key).Distinct().Count());
        Assert.Equal(["published", "synced"], (await AdminRowsAsync("SELECT status, sync_status FROM billing.price_lists WHERE id = $1", draft))[0]);
    }

    [Fact]
    public async Task ActivatingTwice_ChangesNothingTwice()
    {
        await using var host = await CreateAsync();
        var market = await BillingData.MarketAsync();
        var list = await BillingData.PriceListAsync(market, "published");
        await AdminAsync("UPDATE billing.price_lists SET activated_at = NULL WHERE id = $1", list);

        await host.RunAsync(s => s.GetRequiredService<StripeCatalogSync>().ActivateAsync(list, Ct));
        var calls = host.Stripe.Calls.Count;
        await host.RunAsync(s => s.GetRequiredService<StripeCatalogSync>().ActivateAsync(list, Ct));

        Assert.Equal(calls, host.Stripe.Calls.Count);
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'price_list.activated' AND entity_id = $1", list.ToString("D")));
    }

    [Fact]
    public async Task ListOfTheOtherMode_IsNotSynchronized()
    {
        await using var host = await CreateAsync();
        var market = await BillingData.MarketAsync();
        var draft = await BillingData.PriceListAsync(market, mode: "live");

        var result = await host.RunAsync(s => s.GetRequiredService<StripeCatalogSync>().SyncAsync(draft, Ct));

        Assert.Equal(BillingCodes.StripeModeMismatch, result.ErrorCode);
        Assert.Empty(host.Stripe.Prices);
        Assert.Equal(BillingCodes.StripeModeMismatch, await AdminScalarAsync<string>("SELECT sync_error FROM billing.price_lists WHERE id = $1", draft));
    }

    [Fact]
    public async Task UnusedPricesOfARetiredList_AreArchived_TheUsedOnesStay()
    {
        await using var host = await CreateAsync();
        var market = await BillingData.MarketAsync();
        var retired = await BillingData.PriceListAsync(market, "published");
        await AdminAsync("UPDATE billing.price_lists SET status = 'retired' WHERE id = $1", retired);
        var used = await AdminScalarAsync<string>("SELECT stripe_price_monthly FROM billing.price_tiers WHERE price_list_id = $1 AND code = 't2000'", retired);
        var tenant = await BillingData.TenantAsync(market);
        await AdminAsync("UPDATE iam.tenants SET stripe_customer_id = $2 WHERE id = $1", tenant, "cus_" + tenant.ToString("N"));
        await BillingData.SubscriptionAsync(tenant, await BillingData.ShopAsync(tenant), retired, "t2000", 19m, DateTimeOffset.UtcNow.AddDays(10), stripePriceId: used);

        await host.RunAsync(s => s.GetServices<IJobHandler>().OfType<ArchiveUnusedPricesHandler>().Single().RunAsync(Ct));

        var archived = await AdminRowsAsync("SELECT code, archived_at IS NOT NULL FROM billing.price_tiers WHERE price_list_id = $1 AND stripe_price_monthly IS NOT NULL ORDER BY min_products", retired);
        Assert.Equal([("t500", true), ("t2000", false), ("t5000", true), ("t20000", true)], archived.Select(r => ((string)r[0]!, (bool)r[1]!)));
        Assert.DoesNotContain(host.Stripe.Calls, c => c.Method == nameof(IStripeGateway.SetPriceActiveAsync) && c.Argument == used);
    }
}
