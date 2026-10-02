using System.Net;
using System.Text.Json;
using EshopGuard.Api.Tests.Shops;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>Helpers of the tests of change 11: the e-shop „bylinkovo.sk“ and its paths.</summary>
public abstract class FindingsTestBase : ShopTestBase
{
    internal static string S(Person person, Guid shopId) => $"/api/t/{person.TenantId}/shops/{shopId}";

    internal static async Task<(ApiFactory Factory, Person Owner, BylinkovoSeed Data)> BylinkovoAsync()
    {
        var factory = Factory();
        var owner = await People.OwnerAsync(factory);
        var data = await BylinkovoSeed.SeedAsync(factory, owner);
        return (factory, owner, data);
    }

    internal static async Task<JsonElement> ProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"{(int)response.StatusCode} {body}");
        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal(code, json.GetProperty("code").GetString());
        return json;
    }
}
