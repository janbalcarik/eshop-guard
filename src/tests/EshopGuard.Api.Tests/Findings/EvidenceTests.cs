using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>
/// Evidence of the tenant (change 11, task 6.6; AD 10): upload checked by content, size limit, download only through a short
/// signed link, soft deletion that reopens the findings, one piece of evidence for two e-shops, open questions as „Čaká na
/// odpoveď“.
/// </summary>
public sealed class EvidenceTests : FindingsTestBase
{
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n%%EOF\n");

    [Fact]
    public async Task PdfUpload_IsStoredUnderTheTenant_AndDownloadedThroughAShortSignedLink()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);

        using var created = await UploadAsync(owner, Metadata(validUntil: Day(363)), PdfBytes, "Certifikát Vegan (Biopurus).pdf");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var evidence = await ApiClient.JsonAsync(created);
        var id = evidence.GetProperty("id").GetGuid();
        Assert.Equal(("Certifikat-Vegan-Biopurus.pdf", true, "valid", "upload"), (evidence.GetProperty("fileName").GetString(), evidence.GetProperty("hasFile").GetBoolean(),
            evidence.GetProperty("status").GetString(), evidence.GetProperty("source").GetString()));
        Assert.StartsWith($"tenants/{owner.TenantId}/evidence/{id:N}/", await AdminScalarAsync<string>("SELECT file_blob_key FROM fixes.evidence_items WHERE id = $1", id));

        using var redirect = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}/evidence/{id}/file");
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        var location = redirect.Headers.Location!.OriginalString;
        Assert.StartsWith("/api/files/", location);
        using var file = await owner.Browser.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal(PdfBytes, await file.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal("attachment", file.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("application/pdf", file.Content.Headers.ContentType?.MediaType);
        using var forged = await owner.Browser.GetAsync(location[..^4] + "AAAA");
        Assert.Equal(HttpStatusCode.NotFound, forged.StatusCode);
    }

    [Fact]
    public async Task ForgedType_AndTooLargeFile_AreRefused()
    {
        await using var factory = Factory(settings: new Dictionary<string, string?> { ["Evidence:MaxFileBytes"] = "1024" });
        using var owner = await People.OwnerAsync(factory);

        using var forged = await UploadAsync(owner, Metadata(), Encoding.UTF8.GetBytes("<html>not a pdf</html>"), "certifikat.pdf");
        using var large = await UploadAsync(owner, Metadata(), [.. PdfBytes, .. new byte[2048]], "velky.pdf");
        using var noClaim = await UploadAsync(owner, Metadata(claim: " "), PdfBytes, "a.pdf");
        using var dates = await UploadAsync(owner, Metadata(validFrom: "2027-01-01", validUntil: "2026-12-31"), PdfBytes, "a.pdf");

        await ProblemAsync(forged, HttpStatusCode.BadRequest, "evidence.file_type_not_allowed");
        await ProblemAsync(large, HttpStatusCode.BadRequest, "evidence.file_too_large");
        await ProblemAsync(noClaim, HttpStatusCode.BadRequest, "evidence.claim_required");
        await ProblemAsync(dates, HttpStatusCode.BadRequest, "evidence.valid_until_before_from");
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM fixes.evidence_items WHERE tenant_id = $1", owner.TenantId));
    }

    [Fact]
    public async Task OneEvidence_HoldsInTwoShops_AndDeletingItReopensTheFindings()
    {
        var (factory, owner, first) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var second = await BylinkovoSeed.SeedAsync(factory, owner);
        var id = (await ApiClient.JsonAsync(await UploadAsync(owner, Metadata(claim: "Certifikovaná prírodná kozmetika COSMOS"), PdfBytes, "cosmos.pdf"))).GetProperty("id").GetGuid();
        var path = $"/api/t/{owner.TenantId}/evidence/{id}";

        using var linked = await owner.Browser.PostAsync(path + "/links", new { findingIds = new[] { first.Findings["serum"], second.Findings["serum"] } });

        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        var links = (await ApiClient.JsonAsync(linked)).GetProperty("links");
        Assert.Equal((2, 2, 2), (links.GetProperty("findings").GetInt32(), links.GetProperty("pages").GetInt32(), links.GetProperty("shops").GetInt32()));
        Assert.Equal("kept_with_evidence", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", second.Findings["serum"]));
        Assert.Equal(2L, await AdminScalarAsync<long>("SELECT count(*) FROM fixes.decision_memory WHERE evidence_id = $1 AND superseded_at IS NULL", id));

        using var deleted = await owner.Browser.DeleteAsync(path);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal("open", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", first.Findings["serum"]));
        Assert.Equal("open", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", second.Findings["serum"]));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM fixes.decision_memory WHERE evidence_id = $1 AND superseded_at IS NULL", id));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM fixes.evidence_items WHERE id = $1 AND deleted_at IS NOT NULL", id));
        using var gone = await owner.Browser.GetAsync(path);
        await ProblemAsync(gone, HttpStatusCode.NotFound, "evidence.not_found");
    }

    [Fact]
    public async Task LinkingAProposedFinding_IsNotAllowed_AndRemovingTheLastLinkReopens()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var id = (await ApiClient.JsonAsync(await UploadAsync(owner, Metadata(), PdfBytes, "a.pdf"))).GetProperty("id").GetGuid();
        var path = $"/api/t/{owner.TenantId}/evidence/{id}";

        using var proposed = await owner.Browser.PostAsync(path + "/links", new { findingIds = new[] { data.Findings["zubna.5"] } });
        using var linked = await owner.Browser.PostAsync(path + "/links", new { findingIds = new[] { data.Findings["serum"] } });
        var linkId = (await ApiClient.JsonAsync(linked)).GetProperty("items")[0].GetProperty("linkId").GetGuid();
        using var removed = await owner.Browser.DeleteAsync($"{path}/links/{linkId}");

        await ProblemAsync(proposed, HttpStatusCode.Conflict, "finding.transition_not_allowed");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal("open", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", data.Findings["serum"]));
    }

    [Fact]
    public async Task List_HasTheOpenQuestionsAsAwaitingAnswer_AndTheStats()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        await UploadAsync(owner, Metadata(validUntil: Day(18)), PdfBytes, "bdih.pdf");
        await UploadAsync(owner, Metadata(validUntil: Day(400)), PdfBytes, "vegan.pdf");
        using (var no = await owner.Browser.PostAsync($"{S(owner, data.ShopId)}/questions/{data.Questions["vodnar"]}/answer", new { answer = "no" }))
        {
            Assert.Equal(HttpStatusCode.OK, no.StatusCode);
        }

        var list = await ApiClient.JsonAsync(await owner.Browser.GetAsync($"/api/t/{owner.TenantId}/evidence"));
        using var expiring = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}/evidence?status=expiring");
        using var wrong = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}/evidence?status=lost");

        var stats = list.GetProperty("stats");
        // zubna, cosmos, vegan are open; vodnar was answered „Nie“ (claim_removed, 4 pages).
        Assert.Equal((1, 1, 3), (stats.GetProperty("valid").GetInt32(), stats.GetProperty("expiring").GetInt32(), stats.GetProperty("awaitingAnswer").GetInt32()));
        Assert.Equal(4, stats.GetProperty("productsCovered").GetInt32());
        var items = list.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal("expiring", items[0].GetProperty("status").GetString());
        var removed = items.Single(i => i.GetProperty("status").GetString() == "claim_removed");
        Assert.Equal((4, "answer"), (removed.GetProperty("links").GetProperty("pages").GetInt32(), removed.GetProperty("source").GetString()));
        var awaiting = items.Where(i => i.GetProperty("status").GetString() == "awaiting_answer").ToList();
        Assert.Contains(awaiting, i => i.GetProperty("questionId").GetGuid() == data.Questions["cosmos"] && i.GetProperty("subjectLabel").GetString() == "COSMOS");
        Assert.Single((await ApiClient.JsonAsync(expiring)).GetProperty("items").EnumerateArray());
        await ProblemAsync(wrong, HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task Patch_NeedsIfMatch_AndRecomputesTheState()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        using var created = await UploadAsync(owner, Metadata(validUntil: Day(400)), PdfBytes, "a.pdf");
        var id = (await ApiClient.JsonAsync(created)).GetProperty("id").GetGuid();
        var etag = created.Headers.ETag!.Tag;
        var path = $"/api/t/{owner.TenantId}/evidence/{id}";

        using var withoutVersion = await owner.Browser.PatchAsync(path, new { validUntil = Day(18) });
        using var changed = await SendWithIfMatchAsync(owner, HttpMethod.Patch, path, new { validUntil = Day(18) }, etag);
        using var stale = await SendWithIfMatchAsync(owner, HttpMethod.Patch, path, new { title = "Iný" }, etag);

        await ProblemAsync(withoutVersion, HttpStatusCode.BadRequest, "validation.failed");
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal("expiring", (await ApiClient.JsonAsync(changed)).GetProperty("status").GetString());
        await ProblemAsync(stale, HttpStatusCode.Conflict, "concurrency.conflict");
    }

    /// <summary>A day from today (UTC), as the API takes it.</summary>
    internal static string Day(int offset) => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(offset).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    internal static object Metadata(string claim = "Vegan", string? validFrom = null, string? validUntil = null) =>
        new { claimText = claim, subjectKind = "brand", subjectLabel = "Biopurus", kind = "certificate", title = "Certifikát Vegan", validFrom, validUntil };

    internal static async Task<HttpResponseMessage> UploadAsync(Person person, object metadata, byte[] file, string fileName)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/t/{person.TenantId}/evidence");
        request.Headers.Add(ApiFactory.ClientIpHeader, person.Browser.Ip);
        request.Headers.Add("X-CSRF-TOKEN", await person.Browser.CsrfAsync());
        var form = new MultipartFormDataContent { { new StringContent(JsonSerializer.Serialize(metadata)), "metadata" } };
        var content = new ByteArrayContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(content, "file", fileName);
        request.Content = form;
        return await person.Browser.Http.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> SendWithIfMatchAsync(Person person, HttpMethod method, string path, object body, string etag)
    {
        using var request = new HttpRequestMessage(method, path) { Content = System.Net.Http.Json.JsonContent.Create(body) };
        request.Headers.Add(ApiFactory.ClientIpHeader, person.Browser.Ip);
        request.Headers.Add("X-CSRF-TOKEN", await person.Browser.CsrfAsync());
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        return await person.Browser.Http.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
