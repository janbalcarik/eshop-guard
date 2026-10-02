using System.Text;
using EshopGuard.Api.Auth;
using EshopGuard.Api.Problems;
using EshopGuard.Billing;
using EshopGuard.Billing.Stripe;
using EshopGuard.Billing.Webhooks;
using Microsoft.AspNetCore.Http.Features;

namespace EshopGuard.Api.Endpoints;

/// <summary>
/// <c>POST /api/webhooks/stripe</c> (change 12, task 6.1): the raw body (at most 512 kB), the signature of <c>Stripe-Signature</c>
/// with a tolerance of 300 s and <c>livemode</c> of the configured mode; the event and its job in one transaction, then
/// <c>200</c> at once. A wrong signature or mode is <c>400</c> with nothing stored and only the code logged, never the body.
/// No session and no CSRF (Stripe signs the request).
/// </summary>
public static partial class StripeWebhookEndpoint
{
    public const int MaxBodyBytes = 512 * 1024;

    public static RouteGroupBuilder MapStripeWebhookEndpoint(this RouteGroupBuilder api)
    {
        api.MapPost("/webhooks/stripe", async (HttpContext context, StripeWebhookVerifier verifier, StripeEventIntake intake, ILoggerFactory loggers, CancellationToken ct) =>
            {
                var logger = loggers.CreateLogger("EshopGuard.Api.StripeWebhook");
                if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
                {
                    limit.MaxRequestBodySize = MaxBodyBytes;
                }

                if (context.Request.ContentLength > MaxBodyBytes)
                {
                    return EgProblem.Result(context, "request.invalid", StatusCodes.Status413PayloadTooLarge);
                }

                string json;
                using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
                {
                    json = await reader.ReadToEndAsync(ct);
                }

                var result = verifier.Verify(json, context.Request.Headers["Stripe-Signature"].ToString());
                if (result.Event is not { } stripeEvent)
                {
                    LogRefused(logger, result.ErrorCode!);
                    return result.ErrorCode == BillingCodes.Unavailable
                        ? EgProblem.Result(context, BillingCodes.Unavailable, StatusCodes.Status503ServiceUnavailable)
                        : EgProblem.Result(context, result.ErrorCode!, StatusCodes.Status400BadRequest);
                }

                await intake.StoreAsync(stripeEvent, ct);
                return Results.Ok(new { received = true });
            })
            .DisableCsrf()
            .WithTags("webhooks")
            .ProducesProblemCodes(StripeWebhookVerifier.SignatureInvalid, StripeWebhookVerifier.ModeMismatch, BillingCodes.Unavailable);
        return api;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "stripe.webhook_refused {Code}")]
    private static partial void LogRefused(ILogger logger, string code);
}
