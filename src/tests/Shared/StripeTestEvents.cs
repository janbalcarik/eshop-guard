using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace EshopGuard.Tests.Shared;

/// <summary>
/// Events of Stripe as the webhook receives them (change 12): the JSON of an event with its object and the header
/// <c>Stripe-Signature</c> (<c>t=…,v1=HMAC-SHA256(secret, "t.body")</c>) with a secret of the tests. No network.
/// </summary>
internal static class StripeTestEvents
{
    /// <summary>The JSON of an event; <paramref name="data"/> adds fields to its object (<c>customer</c>, …).</summary>
    public static string Json(
        string id, string type, string objectType, string objectId, bool livemode = false, DateTimeOffset? created = null, JsonObject? data = null)
    {
        var obj = new JsonObject { ["id"] = objectId, ["object"] = objectType, ["livemode"] = livemode };
        foreach (var (key, value) in data ?? [])
        {
            obj[key] = value?.DeepClone();
        }

        return new JsonObject
        {
            ["id"] = id,
            ["object"] = "event",
            ["api_version"] = "2026-09-30.endive",
            ["created"] = (created ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds(),
            ["livemode"] = livemode,
            ["pending_webhooks"] = 1,
            ["request"] = new JsonObject { ["id"] = null, ["idempotency_key"] = null },
            ["type"] = type,
            ["data"] = new JsonObject { ["object"] = obj },
        }.ToJsonString();
    }

    /// <summary>The header <c>Stripe-Signature</c> of the body signed at <paramref name="at"/>.</summary>
    public static string Signature(string json, string secret, DateTimeOffset? at = null)
    {
        var timestamp = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(timestamp + "." + json));
        return $"t={timestamp},v1={Convert.ToHexStringLower(hash)}";
    }
}
