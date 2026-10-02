using System.Net;
using System.Net.Http.Headers;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>The catalog of the texts of the rules (change 11, task 2.6; AD 2).</summary>
public sealed class RuleTextCatalogTests : FindingsTestBase
{
    [Fact]
    public async Task Czech_ReturnsExplanationsByJurisdiction_WithAnETag()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var eco = await ShopSeed.RuleSetAsync("eco", "eco");

        using var response = await owner.Browser.GetAsync($"/api/catalog/rule-texts?locale=cs&ruleSetIds={eco}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.ETag);
        Assert.Contains("private", response.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        var body = await ApiClient.JsonAsync(response);
        var set = Assert.Single(body.GetProperty("ruleSets").EnumerateArray());
        Assert.Equal("cs", set.GetProperty("locale").GetString());
        var rule = set.GetProperty("rules").GetProperty("eco_generic_claim");
        Assert.False(string.IsNullOrWhiteSpace(rule.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(rule.GetProperty("explanation").GetProperty("default").GetString()));
        Assert.True(rule.GetProperty("explanation").GetProperty("byJurisdiction").TryGetProperty("cz", out _));
        Assert.False(string.IsNullOrWhiteSpace(rule.GetProperty("recommendation").GetString()));
    }

    [Fact]
    public async Task Slovak_WithoutAReviewedTranslation_ReturnsTheOriginalTexts_AndSaysWhichLanguage()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var eco = await ShopSeed.RuleSetAsync("eco", "eco");

        var body = await GetJsonAsync(owner, $"/api/catalog/rule-texts?locale=sk&ruleSetIds={eco}");

        // The Slovak texts of eco are a machine draft (change 6): never shown before a person reviews them.
        Assert.Equal("cs", body.GetProperty("ruleSets")[0].GetProperty("locale").GetString());
    }

    [Fact]
    public async Task SameETag_IsNotModified()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var eco = await ShopSeed.RuleSetAsync("eco", "eco");
        var path = $"/api/catalog/rule-texts?locale=cs&ruleSetIds={eco}";
        using var first = await owner.Browser.GetAsync(path);

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var second = await owner.Browser.Http.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task RuleSetWithoutAnyTexts_IsIncomplete_NotPartial()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var eco = await ShopSeed.RuleSetAsync("eco", "eco");
        var unknown = await ShopSeed.RuleSetAsync("unknown_set", "eco");

        using var response = await owner.Browser.GetAsync($"/api/catalog/rule-texts?locale=cs&ruleSetIds={eco},{unknown}");

        var problem = await ProblemAsync(response, HttpStatusCode.Conflict, "catalog.locale_incomplete");
        Assert.Equal(unknown.ToString(), problem.GetProperty("params").GetProperty("ruleSetId").GetString());
    }

    [Fact]
    public async Task LanguageNotEnabled_IsRefused()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);

        using var response = await owner.Browser.GetAsync("/api/catalog/rule-texts?locale=de");

        await ProblemAsync(response, HttpStatusCode.BadRequest, "locale.not_enabled");
    }
}
