using System.Net;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>
/// Questions and answers (change 11, task 5.6; spec „Odpovědi na otázky platné pro stejný text“). Vodnár is one finding on
/// 4 pages with one question (the identity of findings of change 8), so „Nie“ changes 1 question, 1 finding and 4 pages.
/// </summary>
public sealed class QuestionTests : FindingsTestBase
{
    [Fact]
    public async Task Vodnar_No_SelectsTheReadyVariantOnAllFourPages_AndTheSameAnswerAgainChangesNothing()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var path = $"{S(owner, data.ShopId)}/questions/{data.Questions["vodnar"]}/answer";

        using var response = await owner.Browser.PostAsync(path, new { answer = "no" });
        using var again = await owner.Browser.PostAsync(path, new { answer = "no" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ApiClient.JsonAsync(response);
        Assert.Equal((1, 1, 4, false), (result.GetProperty("affectedQuestions").GetInt32(), result.GetProperty("affectedFindings").GetInt32(),
            result.GetProperty("affectedPages").GetInt32(), result.GetProperty("generationPending").GetBoolean()));
        Assert.Equal("proposed", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", data.Findings["vodnar"]));
        Assert.Equal(4L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM fixes.fix_proposals WHERE $1 = ANY(finding_ids) AND selected_alternative = 'answer_no' AND status = 'proposed'", data.Findings["vodnar"]));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.audit_log WHERE tenant_id = $1 AND action = 'question.answered' AND entity_id = $2", owner.TenantId, data.Questions["vodnar"].ToString("D")));
    }

    [Fact]
    public async Task Cosmos_Yes_CreatesEvidenceLinkedToTheFinding_AndKeepsIt()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;

        using var response = await owner.Browser.PostAsync($"{S(owner, data.ShopId)}/questions/{data.Questions["cosmos"]}/answer", new { answer = "yes" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var evidenceId = (await ApiClient.JsonAsync(response)).GetProperty("evidenceId").GetGuid();
        var evidence = await AdminRowsAsync("SELECT kind, source, status, subject_label, claim_text, file_blob_key FROM fixes.evidence_items WHERE id = $1", evidenceId);
        Assert.Equal(["answer", "answer", "valid", "COSMOS", "Certifikovaná prírodná kozmetika COSMOS", null], evidence[0]);
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM fixes.evidence_links WHERE evidence_id = $1 AND finding_id = $2 AND page_id = $3", evidenceId, data.Findings["serum"], data.Pages["serum"].PageId));
        Assert.Equal("kept_with_evidence", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", data.Findings["serum"]));
        Assert.Equal(evidenceId, await AdminScalarAsync<Guid>("SELECT evidence_id FROM checks.questions WHERE id = $1", data.Questions["cosmos"]));
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM fixes.decision_memory WHERE evidence_id = $1 AND decision = 'keep_with_evidence' AND superseded_at IS NULL", evidenceId));
    }

    [Fact]
    public async Task Vegan_No_WithoutAReadyVariant_QueuesTheGeneration()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;

        using var response = await owner.Browser.PostAsync($"{S(owner, data.ShopId)}/questions/{data.Questions["vegan"]}/answer", new { answer = "no" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await ApiClient.JsonAsync(response)).GetProperty("generationPending").GetBoolean());
        Assert.Equal("open", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", data.Findings["vegan"]));
        Assert.Equal(["llm", (short)0], (await AdminRowsAsync(
            "SELECT resource_class, priority FROM ops.jobs WHERE kind = 'fix.generate_for_answer' AND shop_id = $1 AND payload->>'finding_id' = $2",
            data.ShopId, data.Findings["vegan"].ToString("D")))[0]);
    }

    [Fact]
    public async Task ExhaustedBudget_Is429_AndTheQuestionStaysOpen()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        await AdminScalarAsync<int>(
            """
            WITH i AS (INSERT INTO ops.rate_limit_buckets (key, capacity, tokens, refill_per_sec, created_at, updated_at)
                VALUES ($1, 50, 0, 50.0 / 86400, now(), now()) RETURNING 1) SELECT count(*)::int FROM i
            """, "fixes:generate:tenant:" + owner.TenantId.ToString("D"));

        using var response = await owner.Browser.PostAsync($"{S(owner, data.ShopId)}/questions/{data.Questions["vegan"]}/answer", new { answer = "no" });

        await ProblemAsync(response, HttpStatusCode.TooManyRequests, "budget.daily_limit_reached");
        Assert.True(response.Headers.RetryAfter is not null);
        Assert.Equal("open", await AdminScalarAsync<string>("SELECT status FROM checks.questions WHERE id = $1", data.Questions["vegan"]));
        Assert.Equal("needs_answer", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", data.Findings["vegan"]));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind = 'fix.generate_for_answer' AND shop_id = $1", data.ShopId));
    }

    [Fact]
    public async Task ChangedAnswer_TakesThePreviousBack_UntilAProposalIsPublished()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var path = $"{S(owner, data.ShopId)}/questions/{data.Questions["vodnar"]}/answer";
        using (var no = await owner.Browser.PostAsync(path, new { answer = "no" }))
        {
            Assert.Equal(HttpStatusCode.OK, no.StatusCode);
        }

        using var yes = await owner.Browser.PostAsync(path, new { answer = "yes" });

        Assert.Equal(HttpStatusCode.OK, yes.StatusCode);
        Assert.Equal("kept_with_evidence", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", data.Findings["vodnar"]));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM fixes.fix_proposals WHERE $1 = ANY(finding_ids) AND selected_alternative IS NOT NULL", data.Findings["vodnar"]));

        using (var no = await owner.Browser.PostAsync(path, new { answer = "no" }))
        {
            Assert.Equal(HttpStatusCode.OK, no.StatusCode);
        }

        // The evidence of „Áno“ is deleted and its memory superseded.
        Assert.Equal(0L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM fixes.evidence_items e JOIN fixes.evidence_links l ON l.evidence_id = e.id WHERE l.finding_id = $1 AND e.deleted_at IS NULL", data.Findings["vodnar"]));
        Assert.Equal(0L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM fixes.decision_memory WHERE shop_id = $1 AND segment_hash = $2 AND superseded_at IS NULL", data.ShopId, BylinkovoSeed.VodnarHash));
        await AdminScalarAsync<int>(
            "WITH u AS (UPDATE fixes.fix_proposals SET status = 'published' WHERE id = $1 RETURNING 1) SELECT count(*)::int FROM u", data.Proposals["vodnar.levanduľa"]);

        using var locked = await owner.Browser.PostAsync(path, new { answer = "yes" });

        await ProblemAsync(locked, HttpStatusCode.Conflict, "question.answer_locked");
    }

    [Fact]
    public async Task SiteQuestion_No_KeepsTheRemedyOfTheRule_WithoutAModel()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;

        using var response = await owner.Browser.PostAsync($"{S(owner, data.ShopId)}/questions/{data.Questions["site"]}/answer", new { answer = "no" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await ApiClient.JsonAsync(response)).GetProperty("generationPending").GetBoolean());
        Assert.Equal("open", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", data.Findings["site"]));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind = 'fix.generate_for_answer' AND shop_id = $1", data.ShopId));
    }

    [Fact]
    public async Task List_ShowsWhereAnAnswerApplies_AndFilters()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var path = $"{S(owner, data.ShopId)}/questions";

        using var all = await owner.Browser.GetAsync(path);
        using var site = await owner.Browser.GetAsync(path + "?scope=site");
        using var wrong = await owner.Browser.GetAsync(path + "?status=closed");

        var questions = (await ApiClient.JsonAsync(all)).EnumerateArray().ToList();
        Assert.Equal(5, questions.Count);
        var vodnar = questions.Single(q => q.GetProperty("questionId").GetGuid() == data.Questions["vodnar"]);
        Assert.Equal((1, 1, 4), (vodnar.GetProperty("appliesTo").GetProperty("questions").GetInt32(), vodnar.GetProperty("appliesTo").GetProperty("findings").GetInt32(),
            vodnar.GetProperty("appliesTo").GetProperty("pages").GetInt32()));
        Assert.Equal("burn_time_evidence", vodnar.GetProperty("code").GetString());
        Assert.Equal(data.Questions["site"], Assert.Single((await ApiClient.JsonAsync(site)).EnumerateArray()).GetProperty("questionId").GetGuid());
        var problem = await ProblemAsync(wrong, HttpStatusCode.BadRequest, "validation.failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("status", out var _));
    }

    [Fact]
    public async Task UnknownAnswer_Is400_AndUnknownQuestion_Is404()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;

        using var maybe = await owner.Browser.PostAsync($"{S(owner, data.ShopId)}/questions/{data.Questions["vodnar"]}/answer", new { answer = "maybe" });
        using var unknown = await owner.Browser.PostAsync($"{S(owner, data.ShopId)}/questions/{Guid.NewGuid()}/answer", new { answer = "no" });

        var problem = await ProblemAsync(maybe, HttpStatusCode.BadRequest, "validation.failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("answer", out var _));
        await ProblemAsync(unknown, HttpStatusCode.NotFound, "question.not_found");
    }
}
