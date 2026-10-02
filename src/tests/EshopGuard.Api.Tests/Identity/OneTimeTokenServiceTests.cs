using EshopGuard.Application.Security;
using EshopGuard.Data.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Api.Tests.Identity;

/// <summary>One use of a token (change 9, task 3.8).</summary>
public sealed class OneTimeTokenServiceTests : ApiTestBase
{
    [Fact]
    public async Task HundredConcurrentConsumes_OfOneToken_GiveExactlyOneSuccess()
    {
        await using var factory = Factory();
        var email = NewEmail();
        string token;
        using (var scope = Scope(factory))
        {
            (token, _) = await scope.ServiceProvider.GetRequiredService<OneTimeTokenService>()
                .IssueAsync(email, null, OneTimeTokenPurpose.MagicLink, TimeSpan.FromMinutes(15), null, Ct);
        }

        // At most 20 connections at once: the local PostgreSQL allows 100 for all tests together.
        using var gate = new SemaphoreSlim(20);
        using var start = new ManualResetEventSlim(false);
        var attempts = Enumerable.Range(0, 100).Select(_ => Task.Run(async () =>
        {
            start.Wait(Ct);
            await gate.WaitAsync(Ct);
            try
            {
                using var scope = Scope(factory);
                return (await scope.ServiceProvider.GetRequiredService<OneTimeTokenService>().ConsumeAsync(token, OneTimeTokenPurpose.MagicLink, Ct)).State;
            }
            finally
            {
                gate.Release();
            }
        }, Ct)).ToList();
        start.Set();
        var states = await Task.WhenAll(attempts);

        Assert.Equal(1, states.Count(s => s == TokenState.Valid));
        Assert.Equal(99, states.Count(s => s == TokenState.Used));
    }

    [Fact]
    public async Task UnknownAndMalformedTokens_AreInvalid()
    {
        await using var factory = Factory();
        using var scope = Scope(factory);
        var tokens = scope.ServiceProvider.GetRequiredService<OneTimeTokenService>();

        Assert.Equal(TokenState.Invalid, (await tokens.ConsumeAsync(OneTimeTokens.Create().Token, OneTimeTokenPurpose.MagicLink, Ct)).State);
        Assert.Equal(TokenState.Invalid, (await tokens.InspectAsync("kratky", OneTimeTokenPurpose.MagicLink, Ct)).State);
        Assert.Equal(TokenState.Invalid, (await tokens.InspectAsync(null, OneTimeTokenPurpose.Reset, Ct)).State);
    }

    private static IServiceScope Scope(ApiFactory factory)
    {
        var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetUser(null);
        return scope;
    }
}
