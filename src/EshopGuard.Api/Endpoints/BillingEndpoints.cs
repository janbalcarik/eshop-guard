using EshopGuard.Api.Contracts;
using EshopGuard.Api.Problems;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Tenants;
using EshopGuard.Billing;
using EshopGuard.Billing.Contracts;
using EshopGuard.Billing.Orders;
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
        return tenant;
    }
}
