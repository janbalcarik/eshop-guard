using System.Net;
using EshopGuard.Application.Protocols;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Protocols;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using static EshopGuard.Api.Tests.Findings.ProposalTests;
using static EshopGuard.Api.Tests.Findings.ShopSeed;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>
/// Protocols of the checks (change 11, tasks 9.1, 9.2 and 9.6; AD 11): the number per tenant and year, the content of
/// <c>Protocol.dc.html</c> from the database, the request (202) and the PDF only when it is ready (302).
/// </summary>
public sealed class ProtocolTests : FindingsTestBase
{
    /// <summary>31. 10. 2026, the day of issue of the design.</summary>
    private static readonly DateTimeOffset IssuedAt = new(2026, 10, 31, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Request_Is202_WithTheNextNumber_AndThePdfOnlyWhenReady()
    {
        var (factory, owner, data) = await ShopAsync();
        await using var _ = factory;
        using var __ = owner;
        await ExecuteAsync(
            "INSERT INTO fixes.protocols (tenant_id, shop_id, number, period_from, period_to, locale, rule_set_ids, status, created_at, updated_at) VALUES ($1, $2, 'EG-2026-0141', '2026-09-01', '2026-09-30', 'sk', '{}', 'ready', now(), now())",
            owner.TenantId, data.ShopId);
        var path = $"{S(owner, data.ShopId)}/protocols";

        using var requested = await SendAsync(owner, HttpMethod.Post, path, new { periodFrom = "2026-09-30", periodTo = "2026-10-31" }, null);

        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var body = await ApiClient.JsonAsync(requested);
        Assert.Equal(("EG-2026-0142", "rendering", "sk"), (body.GetProperty("number").GetString(), body.GetProperty("status").GetString(), body.GetProperty("locale").GetString()));
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.jobs WHERE kind = 'protocol.render' AND shop_id = $1 AND priority = 1 AND resource_class = 'cpu'", data.ShopId));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE tenant_id = $1 AND action = 'protocol.requested'", owner.TenantId));

        using (var early = await owner.Browser.GetAsync($"{path}/{id}/pdf"))
        {
            await ProblemAsync(early, HttpStatusCode.Conflict, "protocol.not_ready");
        }

        // The job of the worker (ProtocolRenderHandlerTests) stores the PDF and makes the protocol ready.
        await AdminAsync("UPDATE fixes.protocols SET status = 'ready', pdf_blob_key = $2 WHERE id = $1", id,
            ProtocolJobs.PdfKey(owner.TenantId, data.ShopId, "EG-2026-0142").Value);
        using var ready = await owner.Browser.GetAsync($"{path}/{id}/pdf");

        Assert.Equal(HttpStatusCode.Redirect, ready.StatusCode);
        Assert.StartsWith("/api/files/", ready.Headers.Location!.OriginalString, StringComparison.Ordinal);
        using var list = await owner.Browser.GetAsync(path);
        Assert.Equal(2, (await ApiClient.JsonAsync(list)).GetArrayLength());
    }

    [Fact]
    public async Task DefaultLanguage_IsTheOneOfTheHomeMarket_AndAFailureIsSaid()
    {
        var (factory, owner, data) = await ShopAsync();
        await using var _ = factory;
        using var __ = owner;
        await ExecuteAsync("UPDATE shop.shops SET home_country = 'CZ' WHERE id = $1", data.ShopId);

        using var requested = await SendAsync(owner, HttpMethod.Post, $"{S(owner, data.ShopId)}/protocols", new { periodFrom = "2026-10-01", periodTo = "2026-10-31" }, null);

        var body = await ApiClient.JsonAsync(requested);
        Assert.Equal("cs", body.GetProperty("locale").GetString());
        var id = body.GetProperty("id").GetGuid();
        await AdminAsync("UPDATE fixes.protocols SET status = 'failed', error_code = 'pdf_renderer_unavailable' WHERE id = $1", id);
        using var failed = await owner.Browser.GetAsync($"{S(owner, data.ShopId)}/protocols/{id}/pdf");
        var problem = await ProblemAsync(failed, HttpStatusCode.Conflict, "protocol.failed");
        Assert.Equal("pdf_renderer_unavailable", problem.GetProperty("params").GetProperty("errorCode").GetString());
    }

    [Theory]
    [InlineData("2026-10-31", "2026-10-01")]
    [InlineData("2026-10-01", "2026-11-01")]
    public async Task InvalidPeriod_Is400(string from, string to)
    {
        var (factory, owner, data) = await ShopAsync();
        await using var _ = factory;
        using var __ = owner;

        using var requested = await SendAsync(owner, HttpMethod.Post, $"{S(owner, data.ShopId)}/protocols", new { periodFrom = from, periodTo = to }, null);

        await ProblemAsync(requested, HttpStatusCode.BadRequest, "protocol.period_invalid");
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM fixes.protocols WHERE shop_id = $1", data.ShopId));
    }

    [Fact]
    public async Task WithoutAFinishedCheck_Is409()
    {
        await using var factory = Factory(time: new FakeTimeProvider(IssuedAt));
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();

        using var requested = await SendAsync(owner, HttpMethod.Post, $"{S(owner, shopId)}/protocols", new { periodFrom = "2026-10-01", periodTo = "2026-10-31" }, null);

        await ProblemAsync(requested, HttpStatusCode.Conflict, "protocol.no_completed_run");
    }

    [Fact]
    public async Task Numbers_AreConsecutiveUnderConcurrency_AndANewYearStartsANewSeries()
    {
        var (factory, owner, data) = await ShopAsync();
        await using var _ = factory;
        using var __ = owner;

        var numbers = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => AllocateAsync(factory, owner, data.ShopId, IssuedAt)));

        Assert.Equal(Enumerable.Range(1, 20).Select(n => $"EG-2026-{n:D4}"), numbers.Order(StringComparer.Ordinal));
        // 31. 12. 23:30 UTC is already 1. 1. 2027 in Bratislava.
        Assert.Equal("EG-2027-0001", await AllocateAsync(factory, owner, data.ShopId, new DateTimeOffset(2026, 12, 31, 23, 30, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task Content_FollowsTheDesign_AndTheLawStaysSlovakInTheCzechProtocol()
    {
        var (factory, owner, data) = await ShopAsync();
        await using var _ = factory;
        using var __ = owner;
        await DecisionsAsync(owner, data);

        var slovak = await BuildAsync(factory, owner, data.ShopId, "sk");
        var czech = await BuildAsync(factory, owner, data.ShopId, "cs");

        var doc = slovak.Document;
        Assert.Equal(("Protokol o kontrole textov e-shopu", "Č. EG-2026-0500", "Vystavené 31. 10. 2026"), (doc.Heading, doc.NumberLine, doc.IssuedLine));
        Assert.Equal(data.Domain, Fact(doc, "shop"));
        Assert.Equal("30. 9. – 31. 10. 2026", Fact(doc, "period"));
        Assert.EndsWith(", 120 stránok", Fact(doc, "initial_check"), StringComparison.Ordinal);
        Assert.Equal("zakázané v robots.txt: 7", Fact(doc, "unchecked"));
        Assert.Equal("test-eco, test-legal_sk", Fact(doc, "rules"));
        Assert.Equal("Česko, Slovensko", Fact(doc, "countries"));
        var initial = await AdminScalarAsync<long>("SELECT count(*) FROM checks.findings WHERE shop_id = $1 AND first_run_id IS NOT NULL", data.ShopId);
        Assert.Equal((int)initial, doc.Summary.Single(s => s.Code == "initial").Value);
        Assert.Equal(["initial", "fixed", "kept_with_evidence", "kept", "approved", "waiting"], doc.Summary.Select(s => s.Code));

        var replaced = Assert.Single(doc.Decisions.Rows, r => r.Before == "„Okamžitý komfort pre citlivé zuby v ekologickom sete.“");
        Assert.Equal("Zubná pasta + bambusová kefka", replaced.Pages);
        Assert.StartsWith("Nahradené „Okamžitý komfort pre citlivé zuby v sete s bambusovou kefkou.“, zverejnené ", replaced.Fix, StringComparison.Ordinal);
        Assert.Equal("§ 7 zákona č. 108/2024 Z. z.", replaced.Reference);
        var bulk = Assert.Single(doc.Decisions.Rows, r => r.Before == "„Všetky naše produkty balíme ekologicky.“");
        Assert.EndsWith("(hromadne)", bulk.Pages, StringComparison.Ordinal);
        Assert.Contains(doc.Decisions.Rows, r => r.Fix.StartsWith("Ponechané, rozhodol prevádzkovateľ", StringComparison.Ordinal));
        Assert.Equal("„Vegan“, značka Biopurus: certifikát Vegan, zaznamenané " + DateText() + " (5 produktov).", Assert.Single(doc.Evidence.Lines));
        Assert.StartsWith("Protokol zaznamenáva kontrolu textov nástrojom EshopGuard", doc.Disclaimer, StringComparison.Ordinal);
        Assert.Equal(doc.Decisions.Rows.Count, (int)slovak.Summary["decisions"]!);

        Assert.Equal("Protokol o kontrole textů e-shopu", czech.Document.Heading);
        Assert.StartsWith("Nahrazeno „", Assert.Single(czech.Document.Decisions.Rows, r => r.Before.Contains("Okamžitý", StringComparison.Ordinal)).Fix, StringComparison.Ordinal);
        // The law is cited as it is worded: the Slovak law in Slovak, in the Czech protocol too.
        Assert.Contains(czech.Document.Decisions.Rows, r => r.Reference == "§ 7 zákona č. 108/2024 Z. z.");
        Assert.Equal("120 stránek", Fact(czech.Document, "initial_check").Split(", ")[1]);
    }

    private static string Fact(ProtocolDocument document, string code) => document.Facts.Single(f => f.Code == code).Value;

    private static string DateText()
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Bratislava")).DateTime);
        return ProtocolTexts.Date(today);
    }

    private static async Task<(ApiFactory Factory, Person Owner, BylinkovoSeed Data)> ShopAsync()
    {
        var factory = Factory(time: new FakeTimeProvider(IssuedAt));
        var owner = await People.OwnerAsync(factory);
        var data = await BylinkovoSeed.SeedAsync(factory, owner);
        var eco = await RuleSetAsync("eco", "eco");
        var legal = await RuleSetAsync("legal_sk", "legal");
        // The run of the seed finished on 30. 9. 2026 with the rule sets of the design.
        await ExecuteAsync("UPDATE checks.runs SET rule_set_ids = $2, finished_at = '2026-09-30T12:00:00Z' WHERE id = $1", data.Seed.RunId, new[] { eco, legal });
        return (factory, owner, data);
    }

    /// <summary>The decisions of the design: a published replacement, the group, a kept sentence and the evidence „Vegan“.</summary>
    private static async Task DecisionsAsync(Person owner, BylinkovoSeed data)
    {
        var shopId = data.ShopId;
        await ExecuteAsync("UPDATE fixes.fix_proposals SET status = 'accepted' WHERE id = $1", data.Proposals["zubna.1"]);
        await ExecuteAsync(
            """
            INSERT INTO fixes.decision_memory (tenant_id, shop_id, segment_hash, normalized_text, decision, replacement_text, source_proposal_id, created_by, created_at, updated_at)
            SELECT $1, $2, segment_hash, text, 'replace', 'Okamžitý komfort pre citlivé zuby v sete s bambusovou kefkou.', $3, $4, now(), now() FROM checks.findings WHERE id = $5
            """, owner.TenantId, shopId, data.Proposals["zubna.1"], owner.UserId, data.Findings["zubna.1"]);
        var connector = await ScalarAsync<Guid>("SELECT id FROM shop.connectors WHERE shop_id = $1", shopId);
        await ExecuteAsync(
            """
            INSERT INTO fixes.publications (tenant_id, shop_id, connector_id, page_id, field, new_value, idempotency_key, status, attempts, published_at, fix_proposal_ids, created_at, updated_at)
            VALUES ($1, $2, $3, $4, 'block', 'x', $5, 'published', 1, now(), $6, now(), now())
            """, owner.TenantId, shopId, connector, data.Pages["zubna"].PageId, Guid.NewGuid().ToString("N"), new[] { data.Proposals["zubna.1"] });
        await ExecuteAsync(
            """
            INSERT INTO fixes.decision_memory (tenant_id, shop_id, segment_hash, normalized_text, decision, replacement_text, created_by, created_at, updated_at)
            VALUES ($1, $2, $3, 'Všetky naše produkty balíme ekologicky.', 'replace', 'Obal: papierová krabica bez plastovej výplne.', $4, now(), now())
            """, owner.TenantId, shopId, BylinkovoSeed.GroupHash, owner.UserId);
        await ExecuteAsync(
            """
            INSERT INTO fixes.decision_memory (tenant_id, shop_id, segment_hash, normalized_text, decision, created_by, created_at, updated_at)
            VALUES ($1, $2, 7105, 'Set sme pripravili pre ekologicky zmýšľajúcich zákazníkov', 'keep', $3, now(), now())
            """, owner.TenantId, shopId, owner.UserId);
        var evidence = Guid.CreateVersion7();
        await ExecuteAsync(
            """
            INSERT INTO fixes.evidence_items (id, tenant_id, claim_text, subject_kind, subject_label, kind, title, source, status, created_by, created_at, updated_at)
            VALUES ($1, $2, 'Vegan', 'brand', 'Biopurus', 'certificate', 'Vegan', 'upload', 'valid', $3, now(), now())
            """, evidence, owner.TenantId, owner.UserId);
        foreach (var page in data.Pages.Where(p => p.Key.StartsWith("vegan.", StringComparison.Ordinal)))
        {
            await ExecuteAsync("INSERT INTO fixes.evidence_links (tenant_id, evidence_id, shop_id, page_id, created_at) VALUES ($1, $2, $3, $4, now())",
                owner.TenantId, evidence, shopId, page.Value.PageId);
        }
    }

    private static async Task<ProtocolBuild> BuildAsync(ApiFactory factory, Person owner, Guid shopId, string locale)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(owner.TenantId, owner.UserId);
        var db = scope.ServiceProvider.GetRequiredService<EshopGuardDb>();
        var builder = scope.ServiceProvider.GetRequiredService<ProtocolDocumentBuilder>();
        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var shop = await db.Shops.AsNoTracking().FirstAsync(s => s.Id == shopId, Ct);
            return await builder.BuildAsync(shop, "EG-2026-0500", ProtocolTexts.For(locale)!, new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 31), IssuedAt, Ct);
        }, Ct);
    }

    private static async Task<string> AllocateAsync(ApiFactory factory, Person owner, Guid shopId, DateTimeOffset issuedAt)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(owner.TenantId, owner.UserId);
        var db = scope.ServiceProvider.GetRequiredService<EshopGuardDb>();
        var allocator = scope.ServiceProvider.GetRequiredService<ProtocolNumberAllocator>();
        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var number = await allocator.NextAsync(owner.TenantId, issuedAt, Ct);
            db.Protocols.Add(new Protocol { ShopId = shopId, Number = number, PeriodFrom = new DateOnly(2026, 10, 1), PeriodTo = new DateOnly(2026, 10, 31), Locale = "sk" });
            await db.SaveChangesAsync(Ct);
            return number;
        }, Ct);
    }
}
