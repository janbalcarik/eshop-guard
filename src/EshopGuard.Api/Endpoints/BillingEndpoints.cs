using EshopGuard.Api.Contracts;
using EshopGuard.Api.Problems;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Tenants;
using EshopGuard.Billing;
using EshopGuard.Billing.Contracts;
using EshopGuard.Billing.Orders;
using EshopGuard.Billing.Subscriptions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EshopGuard.Api.Endpoints;

/// <summary>
/// Billing of a tenant (change 12): the order of an analysis from a quote, its Checkout and its state after the return from
/// Stripe. Only owners and admins (<c>403 auth.forbidden_role</c> for the others); paying works also in a suspended tenant.
/// </summary>
public static class BillingEndpoints
{
    public static RouteGroupBuilder MapBillingEndpoints(this RouteGroupBuilder tenant)
    {
        var billing = tenant.MapGroup("").WithTags("billing");

        billing.MapPost("/shops/{shopId:guid}/orders", async Task<Results<Created<OrderDto>, Ok<OrderDto>>> (
                Guid tenantId, Guid shopId, CreateOrderRequest? body, HttpContext context, OrderService orders, CancellationToken ct) =>
            {
                var (order, created) = await orders.CreateAsync(context.User.RequireUserId(), shopId, body!.QuoteId!.Value, body.ScopeHash!, body.TermsVersion!, ct);
                return created
                    ? TypedResults.Created($"/api/t/{tenantId:D}/orders/{order.Id:D}", order)
                    : TypedResults.Ok(order);
            })
            .RequireTenantRole(TenantRole.Admin)
            .AllowSuspendedTenant()
            .Validate<CreateOrderRequest>()
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, BillingCodes.QuoteNotFound, BillingCodes.QuoteStale, BillingCodes.QuoteNotPayable, BillingCodes.CompanyIdRequired,
                BillingCodes.TaxTreatmentUndetermined, OrderService.TermsOutdated, ProblemCodes.MarketsNotConfirmed, ProblemCodes.ShopOwnershipNotVerified,
                ProblemCodes.SampleNotFinished, ProblemCodes.ShopStatusNotOrderable);

        billing.MapGet("/orders/{orderId:guid}", async (Guid orderId, string? sessionId, OrderService orders, CancellationToken ct) =>
                TypedResults.Ok(await orders.GetAsync(orderId, sessionId, ct)))
            .RequireTenantRole(TenantRole.Admin)
            .AllowSuspendedTenant()
            .ProducesProblemCodes(BillingCodes.OrderNotFound);

        billing.MapPost("/orders/{orderId:guid}/checkout", async (Guid orderId, HttpContext context, CheckoutService checkout, CancellationToken ct) =>
                TypedResults.Ok(await checkout.CreateSessionAsync(context.User.RequireUserId(), orderId, ct)))
            .RequireTenantRole(TenantRole.Admin)
            .AllowSuspendedTenant()
            .ProducesProblemCodes(BillingCodes.OrderNotFound, BillingCodes.OrderNotOpen, BillingCodes.TaxIdPending, BillingCodes.TaxTreatmentUndetermined, BillingCodes.Unavailable);

        billing.MapPost("/orders/{orderId:guid}/pay-with-saved-card", async (Guid orderId, HttpContext context, SavedCardPaymentService payments, CancellationToken ct) =>
                TypedResults.Ok(await payments.PayAsync(context.User.RequireUserId(), orderId, ct)))
            .RequireTenantRole(TenantRole.Admin)
            .AllowSuspendedTenant()
            .ProducesProblemCodes(BillingCodes.OrderNotFound, BillingCodes.OrderNotOpen, BillingCodes.SavedCardMissing, BillingCodes.TaxIdPending,
                BillingCodes.TaxTreatmentUndetermined, BillingCodes.Unavailable);

        billing.MapGet("/billing/overview", async (BillingOverviewService overview, CancellationToken ct) => TypedResults.Ok(await overview.GetAsync(ct)))
            .RequireTenantRole(TenantRole.Admin)
            .AllowSuspendedTenant();

        billing.MapPost("/billing/card/portal-session", async (CardSessionService cards, CancellationToken ct) => TypedResults.Ok(await cards.PortalAsync(ct)))
            .RequireTenantRole(TenantRole.Admin)
            .AllowSuspendedTenant()
            .ProducesProblemCodes(BillingCodes.SavedCardMissing, BillingCodes.Unavailable);

        billing.MapPost("/billing/card/setup-intent", async (CardSessionService cards, CancellationToken ct) => TypedResults.Ok(await cards.SetupIntentAsync(ct)))
            .RequireTenantRole(TenantRole.Admin)
            .AllowSuspendedTenant()
            .ProducesProblemCodes(BillingCodes.Unavailable);

        billing.MapPost("/shops/{shopId:guid}/subscription/cancel", async (Guid shopId, HttpContext context, SubscriptionService subscriptions, CancellationToken ct) =>
                TypedResults.Ok(await subscriptions.CancelAsync(context.User.RequireUserId(), shopId, ct)))
            .RequireTenantRole(TenantRole.Admin)
            .AllowSuspendedTenant()
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, BillingCodes.SubscriptionNotFound, BillingCodes.SubscriptionNotCancelable, BillingCodes.Unavailable);

        billing.MapPost("/shops/{shopId:guid}/subscription/resume", async (Guid shopId, HttpContext context, SubscriptionService subscriptions, CancellationToken ct) =>
                TypedResults.Ok(await subscriptions.ResumeAsync(context.User.RequireUserId(), shopId, ct)))
            .RequireTenantRole(TenantRole.Admin)
            .AllowSuspendedTenant()
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, BillingCodes.SubscriptionNotFound, BillingCodes.SubscriptionNotResumable, BillingCodes.Unavailable);

        billing.MapPost("/shops/{shopId:guid}/subscription", async (
                Guid shopId, StartSubscriptionRequest? body, HttpContext context, SubscriptionService subscriptions, CancellationToken ct) =>
                TypedResults.Ok(await subscriptions.StartAgainAsync(context.User.RequireUserId(), shopId, body?.Confirm == true, body?.Amount, ct)))
            .RequireTenantRole(TenantRole.Admin)
            .AllowSuspendedTenant()
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, BillingCodes.SubscriptionNotFound, BillingCodes.SubscriptionAlreadyRunning, BillingCodes.SavedCardMissing,
                BillingCodes.PriceListMissing, BillingCodes.TierUnavailable, BillingCodes.CompanyIdRequired, BillingCodes.TaxIdPending, BillingCodes.TaxTreatmentUndetermined,
                BillingCodes.Unavailable);
        return tenant;
    }
}
