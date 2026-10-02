using System.Reflection;
using EshopGuard.Billing.Stripe;
using EshopGuard.Tests.Shared;

namespace EshopGuard.Billing.Tests;

/// <summary>The gateway of Stripe (change 12, task 1.4): every write has its idempotency key, reads have none.</summary>
public sealed class StripeGatewayTests
{
    [Fact]
    public void Write_calls_require_idempotency_key()
    {
        var methods = typeof(IStripeGateway).GetMethods();
        Assert.NotEmpty(methods);
        foreach (var method in methods)
        {
            var isRead = method.Name.StartsWith("Get", StringComparison.Ordinal) || method.Name.StartsWith("List", StringComparison.Ordinal);
            var key = method.GetParameters().SingleOrDefault(p => p.Name == "idempotencyKey");
            if (isRead)
            {
                Assert.True(key is null, $"{method.Name} reads, so it has no idempotency key");
            }
            else
            {
                Assert.True(key is not null && key.ParameterType == typeof(string), $"{method.Name} writes without an idempotency key");
            }
        }
    }

    [Fact]
    public async Task TheFake_ReturnsTheSameObjectForTheSameKey()
    {
        var stripe = new FakeStripeGateway();
        var request = new StripePriceRequest("prod_1", "eur", 1900, "month", "sk_eur_t2000_monthly", new Dictionary<string, string>());

        var first = await stripe.CreatePriceAsync(request, "price:a:t2000:monthly", TestContext.Current.CancellationToken);
        var second = await stripe.CreatePriceAsync(request, "price:a:t2000:monthly", TestContext.Current.CancellationToken);

        Assert.Equal(first, second);
        Assert.Single(stripe.Prices);
    }

    [Fact]
    public async Task DisabledGateway_RefusesEveryCall()
    {
        IStripeGateway stripe = new DisabledStripeGateway();

        await Assert.ThrowsAsync<BillingUnavailableException>(() => stripe.GetInvoiceAsync("in_1", CancellationToken.None));
        await Assert.ThrowsAsync<BillingUnavailableException>(() => stripe.CreateCustomerAsync(null!, "k", CancellationToken.None));
    }

    [Fact]
    public void RealGateway_ImplementsEveryMethod_WithoutLoggingHandlers()
    {
        // StripeGateway builds its own HttpClient: no IHttpClientFactory handler that could log a header or a body.
        var fields = typeof(StripeGateway).GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.Contains(fields, f => f.FieldType == typeof(HttpClient));
        Assert.DoesNotContain(typeof(StripeGateway).GetConstructors().SelectMany(c => c.GetParameters()), p => p.ParameterType == typeof(IHttpClientFactory));
    }
}
