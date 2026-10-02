using EshopGuard.Api.Contracts;
using EshopGuard.Api.Problems;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Problems;
using EshopGuard.Billing;
using EshopGuard.Billing.Admin;
using EshopGuard.Billing.Contracts;
using EshopGuard.Billing.Pricing;
using EshopGuard.Application.Shops;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Memory;

namespace EshopGuard.Api.Endpoints;

/// <summary>
/// The admin API of price lists (change 12, task 3.6): drafts, tiers, volume discounts, the impact on running subscriptions and
/// publishing from <c>valid_from</c>. Only the administrators of EshopGuard (<c>Admin:UserIds</c>); the changes are run by the
/// worker and audited there (<c>price_list.*</c>). An answer waits for the worker at most <c>Api:InteractiveWaitSeconds</c>,
/// then <c>202</c> with the job.
/// </summary>
public static class AdminPriceListEndpoints
{
    public static RouteGroupBuilder MapAdminPriceListEndpoints(this RouteGroupBuilder api)
    {
        var lists = api.MapGroup("/admin/price-lists").WithTags("admin").RequirePlatformAdmin();

        lists.MapGet("", async (string? market, PriceListAdminApi admin, CancellationToken ct) => TypedResults.Ok(await admin.ListAsync(market, ct)));

        lists.MapGet("/{priceListId:guid}", async (Guid priceListId, PriceListAdminApi admin, CancellationToken ct) => TypedResults.Ok(await admin.GetAsync(priceListId, ct)))
            .ProducesProblemCodes(BillingCodes.PriceListNotFound);

        lists.MapPost("", async Task<Results<Created<PriceListDto>, Accepted<AdminJobDto>>> (
                CreatePriceListRequest? body, HttpContext context, PriceListAdminApi admin, IJobCompletionAwaiter awaiter, CancellationToken ct) =>
            {
                var (id, jobId) = await admin.CreateDraftAsync(context.User.RequireUserId(), body!.MarketCode!, body.Currency!, body.Name, body.CopyFrom, ct);
                var outcome = await WaitAsync(admin, awaiter, jobId, ct);
                return outcome.Done
                    ? TypedResults.Created($"/api/admin/price-lists/{id:D}", await admin.GetAsync(id, ct))
                    : TypedResults.Accepted($"/api/admin/price-lists/{id:D}", new AdminJobDto(jobId, outcome.State));
            })
            .Validate<CreatePriceListRequest>()
            .ProducesProblemCodes(BillingCodes.PriceListNotFound, BillingCodes.Unavailable);

        lists.MapPut("/{priceListId:guid}/tiers", async Task<Results<Ok<PriceListDto>, Accepted<AdminJobDto>>> (
                Guid priceListId, SetPriceTiersRequest? body, HttpContext context, PriceListAdminApi admin, IJobCompletionAwaiter awaiter, CancellationToken ct) =>
            {
                var tiers = body!.Tiers!.Select(t => new PriceTierInput(t.Code!.Trim(), t.MinProducts!.Value, t.MaxProducts, t.AnalysisPrice, t.MonitoringMonthly, t.MonitoringYearly)).ToList();
                var jobId = await admin.SetTiersAsync(context.User.RequireUserId(), priceListId, tiers, body.NoticeDays, body.FairUseFactor, ct);
                return await ResultAsync(admin, awaiter, priceListId, jobId, ct);
            })
            .Validate<SetPriceTiersRequest>()
            .ProducesProblemCodes(BillingCodes.PriceListNotFound, BillingCodes.PriceListNotEditable, BillingCodes.PriceListTiersInvalid, BillingCodes.Unavailable);

        lists.MapPut("/{priceListId:guid}/volume-discounts", async Task<Results<Ok<PriceListDto>, Accepted<AdminJobDto>>> (
                Guid priceListId, SetVolumeDiscountsRequest? body, HttpContext context, PriceListAdminApi admin, IJobCompletionAwaiter awaiter, CancellationToken ct) =>
            {
                var discounts = body!.Discounts!.Select(d => new VolumeDiscountInput(d.FromShopNumber!.Value, d.Percent!.Value)).ToList();
                var jobId = await admin.SetDiscountsAsync(context.User.RequireUserId(), priceListId, discounts, ct);
                return await ResultAsync(admin, awaiter, priceListId, jobId, ct);
            })
            .Validate<SetVolumeDiscountsRequest>()
            .ProducesProblemCodes(BillingCodes.PriceListNotFound, BillingCodes.PriceListNotEditable, BillingCodes.Unavailable);

        lists.MapGet("/{priceListId:guid}/impact", async Task<Results<Ok<PriceListImpactDto>, Accepted<AdminJobDto>>> (
                Guid priceListId, HttpContext context, PriceListAdminApi admin, IJobCompletionAwaiter awaiter, CancellationToken ct) =>
            {
                var jobId = await admin.RequestImpactAsync(context.User.RequireUserId(), priceListId, ct);
                var outcome = await WaitAsync(admin, awaiter, jobId, ct);
                return outcome.Done && await admin.ImpactAsync(priceListId, ct) is { } impact
                    ? TypedResults.Ok(impact)
                    : TypedResults.Accepted($"/api/admin/price-lists/{priceListId:D}/impact", new AdminJobDto(jobId, outcome.State));
            })
            .ProducesProblemCodes(BillingCodes.PriceListNotFound, BillingCodes.Unavailable);

        lists.MapPost("/{priceListId:guid}/publish", async (
                Guid priceListId, PublishPriceListRequest? body, HttpContext context, PriceListAdminApi admin, IJobCompletionAwaiter awaiter, CancellationToken ct) =>
            {
                var jobId = await admin.PublishAsync(context.User.RequireUserId(), priceListId, body!.ValidFrom!.Value, ct);
                var outcome = await WaitAsync(admin, awaiter, jobId, ct);
                return TypedResults.Accepted($"/api/admin/price-lists/{priceListId:D}", new AdminJobDto(jobId, outcome.State));
            })
            .Validate<PublishPriceListRequest>()
            .ProducesProblemCodes(BillingCodes.PriceListNotFound, BillingCodes.PriceListNotEditable, BillingCodes.PriceListTiersInvalid,
                BillingCodes.PriceListValidFromInvalid, BillingCodes.Unavailable);
        return lists;
    }

    private static async Task<AdminCommandOutcome> WaitAsync(PriceListAdminApi admin, IJobCompletionAwaiter awaiter, long jobId, CancellationToken ct)
    {
        await awaiter.WaitAsync(jobId, ct);
        return await admin.OutcomeAsync(jobId, ct);
    }

    private static async Task<Results<Ok<PriceListDto>, Accepted<AdminJobDto>>> ResultAsync(
        PriceListAdminApi admin, IJobCompletionAwaiter awaiter, Guid priceListId, long jobId, CancellationToken ct)
    {
        var outcome = await WaitAsync(admin, awaiter, jobId, ct);
        return outcome.Done
            ? TypedResults.Ok(await admin.GetAsync(priceListId, ct))
            : TypedResults.Accepted($"/api/admin/price-lists/{priceListId:D}", new AdminJobDto(jobId, outcome.State));
    }
}

/// <summary>
/// <c>GET /api/public/prices?market=sk</c> (task 3.7): the active price list of a market in its currency for the public web,
/// without anything of Stripe; a draft is never shown, no active list is <c>404 billing.price_list_missing</c>. Cached 5 minutes.
/// </summary>
public static class PublicPricesEndpoints
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    public static RouteGroupBuilder MapPublicPricesEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/public/prices", async (string? market, HttpContext context, PriceListReader reader, IMemoryCache cache, CancellationToken ct) =>
            {
                var code = (market ?? string.Empty).Trim().ToLowerInvariant();
                if (code.Length is 0 or > 8)
                {
                    throw new DomainException(ProblemCodes.ValidationFailed, 400) { Errors = new Dictionary<string, IReadOnlyList<string>> { ["market"] = [ProblemCodes.Fields.Required] } };
                }

                var prices = await cache.GetOrCreateAsync("public-prices:" + code, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CacheFor;
                    var currency = await reader.MarketCurrencyAsync(code, ct);
                    var list = currency is null ? null : await reader.ActiveAsync(code, currency, ct);
                    return list is null ? null : new PublicPriceListDto(code, list.List.Currency, list.List.ValidFrom, list.List.NoticeDays,
                        list.Tiers.Select(t => new PublicPriceTierDto(t.Code, t.MinProducts, t.MaxProducts, t.AnalysisPrice, t.MonitoringMonthly, t.MonitoringYearly, TierResolver.IsCustom(t))).ToList(),
                        list.Discounts.Select(d => new VolumeDiscountDto(d.FromShopNumber, d.Percent)).ToList());
                });
                if (prices is null)
                {
                    throw new DomainException(BillingCodes.PriceListMissing, 404);
                }

                context.Response.Headers.CacheControl = "public, max-age=300";
                return TypedResults.Ok(prices);
            })
            .WithTags("public")
            .ProducesProblemCodes(BillingCodes.PriceListMissing, ProblemCodes.ValidationFailed);
        return api;
    }
}
