using Microsoft.Extensions.Options;
using S = global::Stripe;

namespace EshopGuard.Billing.Stripe;

/// <summary>Result of the check of a webhook: the event, or the code why it is refused (nothing of the body).</summary>
public sealed record StripeWebhookResult(StripeEventEnvelope? Event, string? ErrorCode)
{
    public static StripeWebhookResult Refused(string code) => new(null, code);
}

/// <summary>
/// Checks a webhook of Stripe (AD 6, task 6.1): the signature of <c>Stripe-Signature</c> with the secret of the endpoint and the
/// tolerance <c>Billing:Stripe:WebhookToleranceSeconds</c> (<c>EventUtility.ConstructEvent</c>), then <c>livemode</c> against
/// the configured mode. Never throws for a bad request and never returns text of the body.
/// </summary>
public sealed class StripeWebhookVerifier(IOptions<BillingOptions> options)
{
    public const string SignatureInvalid = "billing.webhook_signature_invalid";
    public const string ModeMismatch = "billing.webhook_mode_mismatch";

    public StripeWebhookResult Verify(string json, string? signature)
    {
        ArgumentNullException.ThrowIfNull(json);
        var stripe = options.Value.Stripe;
        if (!options.Value.StripeEnabled)
        {
            return StripeWebhookResult.Refused(BillingCodes.Unavailable);
        }

        if (string.IsNullOrEmpty(signature))
        {
            return StripeWebhookResult.Refused(SignatureInvalid);
        }

        S.Event stripeEvent;
        try
        {
            stripeEvent = S.EventUtility.ConstructEvent(json, signature, stripe.WebhookSecret, stripe.WebhookToleranceSeconds, throwOnApiVersionMismatch: false);
        }
        catch (S.StripeException)
        {
            return StripeWebhookResult.Refused(SignatureInvalid);
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return StripeWebhookResult.Refused(SignatureInvalid);
        }

        return stripeEvent.Livemode != (stripe.Mode == StripeModes.Live)
            ? StripeWebhookResult.Refused(ModeMismatch)
            : new StripeWebhookResult(Envelope(stripeEvent, json), null);
    }

    /// <summary>The envelope of an event of Stripe.net with its raw JSON (the body of the webhook, or the event serialized again).</summary>
    internal static StripeEventEnvelope Envelope(S.Event stripeEvent, string? json = null) => new(
        stripeEvent.Id,
        stripeEvent.Type,
        (stripeEvent.Data?.Object as S.IHasId)?.Id,
        stripeEvent.Livemode,
        new DateTimeOffset(DateTime.SpecifyKind(stripeEvent.Created, DateTimeKind.Utc)),
        json ?? stripeEvent.ToJson());
}
