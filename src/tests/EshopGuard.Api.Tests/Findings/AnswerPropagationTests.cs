using System.Net;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>
/// Where an answer applies (change 11, task 5.1; K rozhodnutí 9): the same question about the same text in every e-shop of
/// the tenant, a question of the whole site only in its e-shop, never in another tenant.
/// </summary>
public sealed class AnswerPropagationTests : FindingsTestBase
{
    [Fact]
    public async Task Answer_ReachesTheSameTextInEveryShopOfTheTenant_ButNotAnotherTenant()
    {
        var (factory, owner, first) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var second = await BylinkovoSeed.SeedAsync(factory, owner);
        using var other = await People.OwnerAsync(factory);
        var foreign = await BylinkovoSeed.SeedAsync(factory, other);

        using var before = await owner.Browser.GetAsync($"{S(owner, first.ShopId)}/questions?status=open");
        var applies = (await ApiClient.JsonAsync(before)).EnumerateArray().Single(q => q.GetProperty("questionId").GetGuid() == first.Questions["vodnar"]).GetProperty("appliesTo");
        using var answer = await owner.Browser.PostAsync($"{S(owner, first.ShopId)}/questions/{first.Questions["vodnar"]}/answer", new { answer = "no" });

        Assert.Equal((2, 2, 8), (applies.GetProperty("questions").GetInt32(), applies.GetProperty("findings").GetInt32(), applies.GetProperty("pages").GetInt32()));
        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        var result = await ApiClient.JsonAsync(answer);
        Assert.Equal((2, 8), (result.GetProperty("affectedQuestions").GetInt32(), result.GetProperty("affectedPages").GetInt32()));
        Assert.Equal("answered", await AdminScalarAsync<string>("SELECT status FROM checks.questions WHERE id = $1", second.Questions["vodnar"]));
        Assert.Equal("proposed", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", second.Findings["vodnar"]));
        Assert.Equal("open", await AdminScalarAsync<string>("SELECT status FROM checks.questions WHERE id = $1", foreign.Questions["vodnar"]));
        Assert.Equal("needs_answer", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", foreign.Findings["vodnar"]));
    }

    [Fact]
    public async Task QuestionOfTheWholeSite_StaysInItsShop()
    {
        var (factory, owner, first) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var second = await BylinkovoSeed.SeedAsync(factory, owner);

        using var answer = await owner.Browser.PostAsync($"{S(owner, first.ShopId)}/questions/{first.Questions["site"]}/answer", new { answer = "yes" });

        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        Assert.Equal(1, (await ApiClient.JsonAsync(answer)).GetProperty("affectedQuestions").GetInt32());
        Assert.Equal("kept_with_evidence", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", first.Findings["site"]));
        Assert.Equal("open", await AdminScalarAsync<string>("SELECT status FROM checks.questions WHERE id = $1", second.Questions["site"]));
    }
}
