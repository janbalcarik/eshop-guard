using System.Text.Json.Nodes;
using EshopGuard.Application.Options;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Shops.Onboarding;
using EshopGuard.Application.Shops.Pricing;
using EshopGuard.Billing.Contracts;
using EshopGuard.Billing.Pricing;
using EshopGuard.Billing.Tax;
using EshopGuard.Data;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EshopGuard.Billing.Orders;

/// <summary>
/// The order of an analysis with monitoring (requirement „Garantovaná cena objednávky“, task 5.1):
/// <list type="number">
/// <item>an open order of the e-shop is returned, a second one is never created (also between two tabs);</item>
/// <item>the readiness of change 10 (sample, markets, versions, ownership, state of the e-shop) blocks with its codes (409);</item>
/// <item>the scope is computed again from the stored state: another <c>scopeHash</c> or a price list that is no longer active is
/// <c>409 quote.stale</c> (<c>reason</c> <c>scope_changed</c> / <c>price_list_changed</c>) with the new quote;</item>
/// <item>only an <c>offer</c> can be ordered (<c>billing.quote_not_payable</c>), only by a company (<c>billing.company_id_required</c>)
/// with a known tax treatment (<c>billing.tax_treatment_undetermined</c>, the case is audited for the accountant);</item>
/// <item>the order keeps the snapshot of the amounts, the currency, the tax treatment, the terms, the <c>scope_hash</c> and the
/// prices and the coupon of Stripe, and the run of the full analysis that waits for the payment (change 8).</item>
/// </list>
/// </summary>
public sealed class OrderService(
    EshopGuardDb db,
    IShopOrderReadiness readiness,
    PriceQuoteService quotes,
    PriceListReader prices,
    IRunService runs,
    IOptions<BillingOptions> options,
    IOptions<LegalOptions> legal,
    TimeProvider time,
    ILogger<OrderService> logger)
{
    public const string TermsOutdated = "billing.terms_outdated";

    public async Task<(OrderDto Order, bool Created)> CreateAsync(Guid userId, Guid shopId, Guid quoteId, string scopeHash, string termsVersion, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeHash);
        var tenantId = db.TenantContext.RequireTenantId();
        if (!await db.ExecuteInTenantTransactionAsync(() => db.Shops.AsNoTracking().AnyAsync(s => s.Id == shopId, ct), ct).ConfigureAwait(false))
        {
            // RLS hides the e-shop of another tenant: the same answer as for one that does not exist.
            throw new DomainException(ProblemCodes.ShopNotFound, 404);
        }

        if (!string.Equals(termsVersion, legal.Value.TermsVersion, StringComparison.Ordinal))
        {
            throw new DomainException(TermsOutdated, 409, new Dictionary<string, object?> { ["current"] = legal.Value.TermsVersion });
        }

        if (await OpenOrderAsync(shopId, ct).ConfigureAwait(false) is { } open)
        {
            return (ToDto(open, null), false);
        }

        var quote = await db.ExecuteInTenantTransactionAsync(() => db.PriceQuotes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quoteId && q.ShopId == shopId, ct), ct)
            .ConfigureAwait(false) ?? throw new DomainException(BillingCodes.QuoteNotFound, 404);
        var ready = await readiness.CheckAsync(shopId, ct).ConfigureAwait(false);

        // An e-shop whose earlier order expired waits for the payment already; it may be ordered again.
        var blocking = ready.Blocking
            .Where(code => !(code == ProblemCodes.ShopStatusNotOrderable && ready.Stored.Shop.Status == ShopStatus.AwaitingPayment)).ToList();
        if (blocking.Count > 0)
        {
            throw new DomainException(blocking[0], 409, new Dictionary<string, object?> { ["blocking"] = blocking });
        }

        var scope = ready.Scope!;
        var request = new PriceQuoteRequest(tenantId, shopId, scope, userId);
        var current = await db.ExecuteInTenantTransactionAsync(async () => await quotes.StoreAsync(request, await quotes.ComputeAsync(request, ct).ConfigureAwait(false), ct)
            .ConfigureAwait(false), ct).ConfigureAwait(false);
        if (!string.Equals(scope.ScopeHash, scopeHash, StringComparison.Ordinal) || !string.Equals(quote.ScopeHash, scopeHash, StringComparison.Ordinal))
        {
            throw await StaleAsync("scope_changed", current, ct).ConfigureAwait(false);
        }

        if (quote.PriceListId != current.PriceListId)
        {
            throw await StaleAsync("price_list_changed", current, ct).ConfigureAwait(false);
        }

        if (quote.Status != PriceQuoteStatus.Offer || quote.PriceListId is not { } priceListId || quote.TierCode is not { } tierCode)
        {
            throw new DomainException(BillingCodes.QuoteNotPayable, 409, new Dictionary<string, object?> { ["reason"] = quote.ReasonCode });
        }

        var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantId, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(tenant.Ico))
        {
            throw new DomainException(BillingCodes.CompanyIdRequired, 422);
        }

        var treatment = TaxTreatmentResolver.Resolve(TaxBuyer.Of(tenant), options.Value.Tax);
        if (treatment == TaxTreatment.Undetermined)
        {
            await AuditAsync(tenantId, userId, "billing.tax_treatment_undetermined", "shop", shopId, new JsonObject { ["country"] = tenant.CountryCode }, ct).ConfigureAwait(false);
            throw new DomainException(BillingCodes.TaxTreatmentUndetermined, 422);
        }

        var list = await prices.GetAsync(priceListId, ct).ConfigureAwait(false) ?? throw await StaleAsync("price_list_changed", current, ct).ConfigureAwait(false);
        var tier = list.Tier(tierCode) ?? throw await StaleAsync("price_list_changed", current, ct).ConfigureAwait(false);
        var discount = quote.DiscountPercent is { } percent ? list.Discounts.FirstOrDefault(d => d.Percent == percent) : null;
        var runId = await RunAsync(shopId, userId, ct).ConfigureAwait(false);
        var vatRate = treatment == TaxTreatment.DomesticVat ? options.Value.Tax.DomesticVatRate : 0m;
        var analysis = quote.AnalysisPrice ?? 0m;
        var vat = TaxTreatmentResolver.Vat(analysis, vatRate);
        var now = time.GetUtcNow();
        var order = new Order
        {
            TenantId = tenantId,
            ShopId = shopId,
            Kind = OrderKind.AnalysisWithTrial,
            PriceListId = priceListId,
            TierCode = tierCode,
            AmountNet = analysis,
            DiscountAmount = 0m,
            VatRate = vatRate,
            VatAmount = vat,
            AmountGross = analysis + vat,
            Currency = quote.Currency,
            Status = OrderStatus.Created,
            RunId = runId,
            CreatedBy = userId,
            PriceQuoteId = quote.Id,
            ScopeHash = scopeHash,
            StripePriceAnalysis = tier.StripePriceAnalysis,
            StripePriceMonitoring = tier.StripePriceMonthly,
            StripeCouponId = discount?.StripeCouponId,
            MonitoringMonthly = quote.MonitoringMonthly,
            MonitoringDiscountPercent = quote.DiscountPercent,
            TermsVersion = termsVersion,
            TaxTreatment = treatment,
            CreatedAt = now,
            UpdatedAt = now,
        };

        try
        {
            await db.ExecuteInTenantTransactionAsync(async () =>
            {
                db.Orders.Add(order);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await db.Shops.Where(s => s.Id == shopId && s.Status == ShopStatus.Sample)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ShopStatus.AwaitingPayment).SetProperty(x => x.UpdatedAt, now), ct).ConfigureAwait(false);
                await BillingSql.AuditAsync(Transaction(), tenantId, userId, BillingSql.User, "order.created", "order", order.Id.ToString("D"),
                    new JsonObject { ["shop_id"] = shopId.ToString("D"), ["tier"] = tierCode, ["amount_net"] = analysis, ["currency"] = quote.Currency }, now, ct).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A second tab created the open order at the same time: it is the answer.
            db.ChangeTracker.Clear();
            return (ToDto((await OpenOrderAsync(shopId, ct).ConfigureAwait(false))!, null), false);
        }

        logger.LogInformation("order.created {OrderId} {TenantId} {ShopId} {Tier}", order.Id, tenantId, shopId, tierCode);
        return (ToDto(order, null), true);
    }

    public async Task<OrderDto> GetAsync(Guid orderId, string? returnedSessionId, CancellationToken ct)
    {
        var order = await db.ExecuteInTenantTransactionAsync(() => db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct), ct).ConfigureAwait(false)
            ?? throw new DomainException(BillingCodes.OrderNotFound, 404);
        return ToDto(order, returnedSessionId);
    }

    /// <summary>
    /// The order as the API shows it; <c>AwaitingConfirmation</c> when the customer came back from Stripe with the session of the
    /// open order (the return itself never confirms a payment, the webhook does).
    /// </summary>
    public static OrderDto ToDto(Order order, string? returnedSessionId)
    {
        ArgumentNullException.ThrowIfNull(order);
        return new OrderDto(
            order.Id, order.ShopId, SnakeCaseEnumConverter<OrderStatus>.ToText(order.Status),
            order.Status == OrderStatus.CheckoutOpen && returnedSessionId is not null && returnedSessionId == order.StripeCheckoutSessionId,
            order.PriceQuoteId, order.TierCode, order.Currency, order.AmountNet, order.VatRate, order.VatAmount, order.AmountGross, order.MonitoringMonthly,
            order.MonitoringDiscountPercent, order.TaxTreatment is { } t ? SnakeCaseEnumConverter<TaxTreatment>.ToText(t) : null, order.TrialEndPlanned,
            order.CheckoutExpiresAt, order.PaidAt, order.RunId, order.CreatedAt);
    }

    private Task<Order?> OpenOrderAsync(Guid shopId, CancellationToken ct) =>
        db.ExecuteInTenantTransactionAsync(() => db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.ShopId == shopId && (o.Status == OrderStatus.Created || o.Status == OrderStatus.CheckoutOpen), ct), ct);

    /// <summary>The full analysis of the e-shop that waits for the payment, or a new one (change 8).</summary>
    private async Task<Guid> RunAsync(Guid shopId, Guid userId, CancellationToken ct)
    {
        var waiting = await db.ExecuteInTenantTransactionAsync(() => db.Runs.AsNoTracking()
            .Where(r => r.ShopId == shopId && r.Kind == RunKind.FullAnalysis
                && (r.Status == RunStatus.Queued || r.Status == RunStatus.Discovering || r.Status == RunStatus.AwaitingPayment))
            .OrderByDescending(r => r.CreatedAt).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct), ct).ConfigureAwait(false);
        if (waiting is { } id)
        {
            return id;
        }

        var created = await runs.CreateFullAnalysisAsync(shopId, userId, ct).ConfigureAwait(false);
        return created.RunId ?? throw new DomainException(created.ErrorCode ?? RunCodes.RunAlreadyActive, created.ErrorCode == RunCodes.ShopNotFound ? 404 : 409);
    }

    private async Task<DomainException> StaleAsync(string reason, PriceQuote current, CancellationToken ct)
    {
        var list = current.PriceListId is { } id ? await prices.GetAsync(id, ct).ConfigureAwait(false) : null;
        var dto = quotes.ToDto(current, current.TierCode is { } code ? list?.Tier(code)?.MaxProducts : null);
        return new DomainException(BillingCodes.QuoteStale, 409, new Dictionary<string, object?> { ["reason"] = reason, ["quote"] = dto });
    }

    private Task AuditAsync(Guid tenantId, Guid userId, string action, string entityType, Guid entityId, JsonObject data, CancellationToken ct) =>
        db.ExecuteInTenantTransactionAsync(() => BillingSql.AuditAsync(Transaction(), tenantId, userId, BillingSql.User, action, entityType, entityId.ToString("D"), data,
            time.GetUtcNow(), ct), ct);

    private NpgsqlTransaction Transaction() => (NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction();
}
