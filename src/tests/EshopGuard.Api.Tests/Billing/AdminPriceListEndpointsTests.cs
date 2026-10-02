using System.Net;
using EshopGuard.Application.Options;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Api.Tests.Billing;

/// <summary>
/// The admin API of price lists and the public prices (change 12, tasks 3.6 and 3.7): only the administrators of EshopGuard
/// (<c>Admin:UserIds</c>), the changes go to the worker as jobs (none runs here, so the answer is <c>202</c>), wrong requests are
/// refused before a job is created; the public prices show the active list only, without anything of Stripe.
/// </summary>
public sealed class AdminPriceListEndpointsTests : ApiTestBase
{
    [Fact]
    public async Task OnlyAdministratorsOfEshopGuard_GetIn()
    {
        var admins = new List<Guid>();
        await using var factory = AdminFactory(admins);
        using var owner = await People.OwnerAsync(factory);
        var anonymous = factory.CreateApiClient();

        using var forbidden = await owner.Browser.GetAsync("/api/admin/price-lists");
        using var unauthenticated = await anonymous.GetAsync("/api/admin/price-lists");
        admins.Add(owner.UserId);
        using var allowed = await owner.Browser.GetAsync("/api/admin/price-lists?market=sk");

        Assert.Equal((HttpStatusCode.Forbidden, "auth.forbidden_role"), ((await ApiClient.ProblemAsync(forbidden)).Status, (await ApiClient.ProblemAsync(forbidden)).Code));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Contains((await ApiClient.JsonAsync(allowed)).EnumerateArray(), l => l.GetProperty("name").GetString() == "SK 2026");
    }

    [Fact]
    public async Task Draft_IsCreatedByTheWorker_TheApiAnswers202WithTheJob()
    {
        var admins = new List<Guid>();
        await using var factory = AdminFactory(admins);
        using var admin = await People.OwnerAsync(factory);
        admins.Add(admin.UserId);
        var market = await BillingSeed.MarketAsync();

        using var response = await admin.Browser.PostAsync("/api/admin/price-lists", new { marketCode = market, currency = "EUR" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var job = (await ApiClient.JsonAsync(response)).GetProperty("jobId").GetInt64();
        Assert.Equal("billing.price_list_admin", await AdminScalarAsync<string>("SELECT kind FROM ops.jobs WHERE id = $1", job));
        Assert.Contains(market, await AdminScalarAsync<string>("SELECT payload::text FROM ops.jobs WHERE id = $1", job), StringComparison.Ordinal);
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM billing.price_lists WHERE market_code = $1", market));
    }

    [Fact]
    public async Task WrongRequests_AreRefusedBeforeAJob()
    {
        var admins = new List<Guid>();
        await using var factory = AdminFactory(admins);
        using var admin = await People.OwnerAsync(factory);
        admins.Add(admin.UserId);
        var market = await BillingSeed.MarketAsync();
        var published = await BillingSeed.PublishedAsync(market);
        await AdminAsync("INSERT INTO billing.price_lists (id, name, market_code, currency, valid_from, status, notice_days, created_at, updated_at) VALUES ($1, 'koncept', $2, 'EUR', now(), 'draft', 30, now(), now())",
            Guid.CreateVersion7(), market);
        var draft = await AdminScalarAsync<Guid>("SELECT id FROM billing.price_lists WHERE market_code = $1 AND status = 'draft'", market);
        var jobs = await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind = 'billing.price_list_admin'");

        using var notEditable = await admin.Browser.PutAsync($"/api/admin/price-lists/{published}/tiers", new { tiers = new[] { new { code = "a", minProducts = 0 } } });
        using var gap = await admin.Browser.PutAsync($"/api/admin/price-lists/{draft}/tiers", new
        {
            tiers = new object[]
            {
                new { code = "t500", minProducts = 0, maxProducts = 500, analysisPrice = 39, monitoringMonthly = 9 },
                new { code = "t2000", minProducts = 502, analysisPrice = 69, monitoringMonthly = 19 },
            },
        });
        using var past = await admin.Browser.PostAsync($"/api/admin/price-lists/{draft}/publish", new { validFrom = DateTimeOffset.UtcNow.AddDays(-1) });
        using var missing = await admin.Browser.GetAsync($"/api/admin/price-lists/{Guid.NewGuid()}");

        Assert.Equal((HttpStatusCode.Conflict, "billing.price_list_not_editable"), ((await ApiClient.ProblemAsync(notEditable)).Status, (await ApiClient.ProblemAsync(notEditable)).Code));
        Assert.Equal((HttpStatusCode.BadRequest, "billing.price_list_tiers_invalid"), ((await ApiClient.ProblemAsync(gap)).Status, (await ApiClient.ProblemAsync(gap)).Code));
        Assert.Equal((HttpStatusCode.BadRequest, "billing.price_list_valid_from_invalid"), ((await ApiClient.ProblemAsync(past)).Status, (await ApiClient.ProblemAsync(past)).Code));
        Assert.Equal((HttpStatusCode.NotFound, "billing.price_list_not_found"), ((await ApiClient.ProblemAsync(missing)).Status, (await ApiClient.ProblemAsync(missing)).Code));
        Assert.Equal(jobs, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind = 'billing.price_list_admin'"));
    }

    [Fact]
    public async Task PublicPrices_ShowTheActiveListOnly_WithoutStripe()
    {
        await using var factory = Factory();
        var anonymous = factory.CreateApiClient();
        var market = await BillingSeed.MarketAsync();
        var draftOnly = await BillingSeed.MarketAsync();
        await BillingSeed.PublishedAsync(market, discount: (3, 10m));
        await AdminAsync("INSERT INTO billing.price_lists (name, market_code, currency, valid_from, status, notice_days, created_at, updated_at) VALUES ('koncept', $1, 'EUR', now(), 'draft', 30, now(), now())", draftOnly);

        using var prices = await anonymous.GetAsync($"/api/public/prices?market={market}");
        using var none = await anonymous.GetAsync($"/api/public/prices?market={draftOnly}");

        Assert.Equal(HttpStatusCode.OK, prices.StatusCode);
        Assert.Equal("public, max-age=300", prices.Headers.CacheControl?.ToString());
        var body = await prices.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("price_", body, StringComparison.Ordinal);
        Assert.DoesNotContain("coupon_", body, StringComparison.Ordinal);
        var json = await ApiClient.JsonAsync(prices);
        Assert.Equal(5, json.GetProperty("tiers").GetArrayLength());
        Assert.True(json.GetProperty("tiers")[4].GetProperty("isCustom").GetBoolean());
        Assert.Equal(10m, json.GetProperty("volumeDiscounts")[0].GetProperty("percent").GetDecimal());
        Assert.Equal((HttpStatusCode.NotFound, "billing.price_list_missing"), ((await ApiClient.ProblemAsync(none)).Status, (await ApiClient.ProblemAsync(none)).Code));
    }

    private static ApiFactory AdminFactory(List<Guid> admins) =>
        Factory(services: s => s.PostConfigure<PlatformAdminOptions>(o => o.UserIds = admins));
}
