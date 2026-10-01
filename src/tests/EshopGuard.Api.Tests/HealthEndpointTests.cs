using System.Net;
using System.Text.Json;
using EshopGuard.Tests.Shared;

namespace EshopGuard.Api.Tests;

[Trait("Category", "Db")]
public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Health_WithAppRole_ReturnsOkForAllFourChecks()
    {
        await TestDatabase.EnsureMigratedAsync(TestContext.Current.CancellationToken);
        await using var factory = new ApiFactory(TestConfiguration.ConnectionString("App"));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());
        var checks = json.RootElement.GetProperty("checks").EnumerateArray()
            .ToDictionary(c => c.GetProperty("name").GetString()!, c => c.GetProperty("status").GetString());
        Assert.Equal(new Dictionary<string, string?> { ["database"] = "ok", ["database_role"] = "ok", ["migrations"] = "ok", ["storage"] = "ok" }, checks);
    }
}

/// <summary>Database unreachable while the API runs (startup guard removed to reach the endpoint).</summary>
public sealed class HealthEndpointWithoutDatabaseTests
{
    [Fact]
    public async Task Health_WithUnreachableDatabase_Returns503WithCodesOnly()
    {
        await using var factory = new ApiFactory(
            "Host=127.0.0.1;Port=1;Database=eshopguard_test;Username=eshopguard_app;Password=not-a-real-password;Timeout=2",
            withStartupGuard: false);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        var database = json.RootElement.GetProperty("checks").EnumerateArray().Single(c => c.GetProperty("name").GetString() == "database");
        Assert.Equal("failed", database.GetProperty("status").GetString());
        Assert.Equal("db.unreachable", database.GetProperty("code").GetString());
        var storage = json.RootElement.GetProperty("checks").EnumerateArray().Single(c => c.GetProperty("name").GetString() == "storage");
        Assert.Equal("ok", storage.GetProperty("status").GetString());
        foreach (var forbidden in new[] { "Exception", "Password", "Host=", "127.0.0.1", "not-a-real-password" })
        {
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
        }
    }
}
