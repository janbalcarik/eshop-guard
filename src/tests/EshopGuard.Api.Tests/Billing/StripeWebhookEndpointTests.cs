using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using EshopGuard.Tests.Shared;

namespace EshopGuard.Api.Tests.Billing;

/// <summary>
/// <c>POST /api/webhooks/stripe</c> (change 12, task 6.1): a signed event is stored with one job <c>billing.process_stripe_event</c>
/// and answered <c>200</c> at once; a duplicate is <c>200</c> without a second job; a wrong or old signature and an event of the
/// other mode are <c>400</c> with nothing stored and nothing of the body in the log. No session, no CSRF.
/// </summary>
public sealed class StripeWebhookEndpointTests : ApiTestBase
{
    [Fact]
    public async Task SignedEvent_IsStoredWithItsJob_And200()
    {
        await using var factory = Factory();
        var id = NewEventId();
        var json = StripeTestEvents.Json(id, "checkout.session.completed", "checkout.session", "cs_test_" + id[4..], data: new JsonObject { ["customer"] = "cus_x" });

        using var response = await PostAsync(factory, json, StripeTestEvents.Signature(json, ApiFactory.StripeWebhookSecret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await AdminRowsAsync("SELECT type, status, object_id, livemode, payload->>'id' FROM billing.stripe_events WHERE id = $1", id);
        Assert.Equal(new object?[] { "checkout.session.completed", "received", "cs_test_" + id[4..], false, id }, Assert.Single(rows));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind = 'billing.process_stripe_event' AND dedupe_key = $1", "stripe:" + id));
        Assert.Empty(factory.Stripe.Calls);
    }

    [Fact]
    public async Task Duplicate_Is200_WithoutASecondJob()
    {
        await using var factory = Factory();
        var id = NewEventId();
        var json = StripeTestEvents.Json(id, "invoice.paid", "invoice", "in_" + id[4..]);

        using var first = await PostAsync(factory, json, StripeTestEvents.Signature(json, ApiFactory.StripeWebhookSecret));
        using var second = await PostAsync(factory, json, StripeTestEvents.Signature(json, ApiFactory.StripeWebhookSecret));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (first.StatusCode, second.StatusCode));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM billing.stripe_events WHERE id = $1", id));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE dedupe_key = $1", "stripe:" + id));
    }

    [Fact]
    public async Task WrongSecret_Is400_NothingStored_NothingOfTheBodyLogged()
    {
        await using var factory = Factory();
        var id = NewEventId();
        var json = StripeTestEvents.Json(id, "invoice.paid", "invoice", "in_" + id[4..]);

        using var response = await PostAsync(factory, json, StripeTestEvents.Signature(json, "whsec_SomeoneElse000000000000000"));

        var problem = await ApiClient.ProblemAsync(response);
        Assert.Equal((HttpStatusCode.BadRequest, "billing.webhook_signature_invalid"), (problem.Status, problem.Code));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM billing.stripe_events WHERE id = $1", id));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE dedupe_key = $1", "stripe:" + id));
        Assert.True(factory.Logs.Contains("stripe.webhook_refused billing.webhook_signature_invalid"));
        Assert.False(factory.Logs.Contains(id));
    }

    [Fact]
    public async Task SignatureOlderThanTheTolerance_Is400()
    {
        await using var factory = Factory();
        var id = NewEventId();
        var json = StripeTestEvents.Json(id, "invoice.paid", "invoice", "in_" + id[4..]);

        using var old = await PostAsync(factory, json, StripeTestEvents.Signature(json, ApiFactory.StripeWebhookSecret, DateTimeOffset.UtcNow.AddSeconds(-301)));
        using var missing = await PostAsync(factory, json, null);

        Assert.Equal("billing.webhook_signature_invalid", (await ApiClient.ProblemAsync(old)).Code);
        Assert.Equal("billing.webhook_signature_invalid", (await ApiClient.ProblemAsync(missing)).Code);
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM billing.stripe_events WHERE id = $1", id));
    }

    [Fact]
    public async Task LiveEvent_InTheTestMode_Is400ModeMismatch()
    {
        await using var factory = Factory();
        var id = NewEventId();
        var json = StripeTestEvents.Json(id, "invoice.paid", "invoice", "in_" + id[4..], livemode: true);

        using var response = await PostAsync(factory, json, StripeTestEvents.Signature(json, ApiFactory.StripeWebhookSecret));

        var problem = await ApiClient.ProblemAsync(response);
        Assert.Equal((HttpStatusCode.BadRequest, "billing.webhook_mode_mismatch"), (problem.Status, problem.Code));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM billing.stripe_events WHERE id = $1", id));
    }

    [Fact]
    public async Task BodyOver512KB_Is413_NothingStored()
    {
        await using var factory = Factory();
        var id = NewEventId();
        var json = StripeTestEvents.Json(id, "invoice.paid", "invoice", "in_" + id[4..], data: new JsonObject { ["description"] = new string('x', 513 * 1024) });

        using var response = await PostAsync(factory, json, StripeTestEvents.Signature(json, ApiFactory.StripeWebhookSecret));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM billing.stripe_events WHERE id = $1", id));
    }

    private static string NewEventId() => "evt_" + Guid.NewGuid().ToString("N");

    /// <summary>The request of Stripe: no cookie, no CSRF token, the raw body and its signature.</summary>
    private static async Task<HttpResponseMessage> PostAsync(ApiFactory factory, string json, string? signature)
    {
        using var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/stripe") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        if (signature is not null)
        {
            request.Headers.Add("Stripe-Signature", signature);
        }

        return await client.Http.SendAsync(request, Ct);
    }
}
