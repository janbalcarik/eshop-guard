using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Api.Tests.Identity;

/// <summary>The store of Identity over <c>iam.users</c> as <c>eshopguard_app</c> (change 9, task 2.5).</summary>
public sealed class EgUserStoreTests : ApiTestBase
{
    [Fact]
    public async Task EmailIsFoundRegardlessOfCase()
    {
        await using var factory = Factory();
        var id = Guid.NewGuid().ToString("N");
        var stored = $"jana.{id}@bylinkovo.sk";
        await CreateAsync(factory, stored);

        using var scope = Scope(factory);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var found = await users.FindByEmailAsync($"  Jana.{id.ToUpperInvariant()}@Bylinkovo.SK ");

        Assert.NotNull(found);
        Assert.Equal(stored, found.Email);
    }

    [Fact]
    public async Task LockoutCounter_AndSecurityStamp_AreStored()
    {
        await using var factory = Factory();
        var email = NewEmail();
        var user = await CreateAsync(factory, email);
        string? stamp;
        using (var scope = Scope(factory))
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var loaded = (await users.FindByIdAsync(user.Id.ToString("D")))!;
            stamp = loaded.SecurityStamp;
            Assert.False(string.IsNullOrEmpty(stamp));
            Assert.True((await users.AccessFailedAsync(loaded)).Succeeded);
            Assert.True((await users.AccessFailedAsync(loaded)).Succeeded);
            Assert.True((await users.UpdateSecurityStampAsync(loaded)).Succeeded);
        }

        var row = (await AdminRowsAsync("SELECT access_failed_count, security_stamp FROM iam.users WHERE id = $1", user.Id)).Single();
        Assert.Equal(2, row[0]);
        Assert.NotEqual(stamp, row[1]);
    }

    [Fact]
    public async Task ExternalLogin_IsUniqueByProviderKey()
    {
        await using var factory = Factory();
        var first = await CreateAsync(factory, NewEmail("a"));
        var second = await CreateAsync(factory, NewEmail("b"));
        var key = "g-" + Guid.NewGuid().ToString("N");
        using (var scope = Scope(factory))
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            Assert.True((await users.AddLoginAsync((await users.FindByIdAsync(first.Id.ToString("D")))!, new UserLoginInfo("google", key, "google"))).Succeeded);
        }

        using (var scope = Scope(factory))
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var result = await users.AddLoginAsync((await users.FindByIdAsync(second.Id.ToString("D")))!, new UserLoginInfo("google", key, "google"));
            Assert.False(result.Succeeded);
            Assert.Equal(first.Id, (await users.FindByLoginAsync("google", key))!.Id);
        }

        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM iam.user_logins WHERE provider_key = $1", key));
    }

    private static IServiceScope Scope(ApiFactory factory)
    {
        var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetUser(null);
        return scope;
    }

    private static async Task<User> CreateAsync(ApiFactory factory, string email)
    {
        using var scope = Scope(factory);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User { Email = email, EmailConfirmed = true, Locale = "sk" };
        var result = await users.CreateAsync(user);
        Assert.True(result.Succeeded, string.Join(",", result.Errors.Select(e => e.Code)));
        return user;
    }
}
