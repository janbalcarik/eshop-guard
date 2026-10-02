using System.Net;
using System.Text;
using EshopGuard.Api.Tests.Fakes;
using EshopGuard.Application.Fixes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using static EshopGuard.Api.Tests.Findings.EvidenceTests;
using static EshopGuard.Api.Tests.Findings.ProposalTests;
using static EshopGuard.Api.Tests.Findings.ShopSeed;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>
/// The whole work of the merchant on „bylinkovo.sk“ without paid services (change 11, tasks 12.4 and 12.5): review, own wording
/// and its recheck, acceptance, the bulk fix, the answer, evidence, publication (<see cref="FakeFixPublisher"/>), the protocol,
/// notifications and the stream of the e-shop. The rechecks of Jev are the jobs of the worker (their own tests); here their
/// result is written as the worker writes it. The logs of the whole flow hold no text of a finding, a proposal or evidence.
/// </summary>
public sealed class FindingsFlowTests : FindingsTestBase
{
    private const string OwnWording = "Bambusová kefka – rukoväť z bambusu namiesto plastu.";
    private const string Fact = "papierovej krabice bez plastovej výplne";
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog /Tajne (obsah-dokladu-123) >>\nendobj\ntrailer\n%%EOF\n");

    [Fact]
    public async Task WholeFlow_WithoutPaidServices_AndTheLogsHoldNoTexts()
    {
        await using var factory = Factory(time: new FakeTimeProvider(new DateTimeOffset(2026, 10, 31, 10, 0, 0, TimeSpan.Zero)),
            services: s => s.AddSingleton<IFixPublisher, FakeFixPublisher>());
        using var owner = await People.OwnerAsync(factory);
        var data = await BylinkovoSeed.SeedAsync(factory, owner);
        await ExecuteAsync("UPDATE shop.shops SET ownership_verified_at = now(), verification_method = 'connector' WHERE id = $1", data.ShopId);
        var shop = S(owner, data.ShopId);

        // The stream of the e-shop is open the whole time.
        using var stream = await owner.Browser.Http.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"{shop}/events"), HttpCompletionOption.ResponseHeadersRead, Ct);
        Assert.Equal(HttpStatusCode.OK, stream.StatusCode);

        // 1. Review of the page.
        using (var review = await owner.Browser.GetAsync($"{shop}/pages/{data.Pages["zubna"].PageId}/review"))
        {
            Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        }

        // 2. Own wording, its recheck (the job of the worker), acceptance.
        var proposal = $"{shop}/proposals/{data.Proposals["zubna.3"]}";
        using (var edited = await SendAsync(owner, HttpMethod.Put, proposal + "/text", new { text = OwnWording }, await ETagAsync(owner, proposal)))
        {
            Assert.Equal(HttpStatusCode.Accepted, edited.StatusCode);
        }

        await AdminAsync("UPDATE fixes.fix_proposals SET recheck_status = 'ok', recheck_result = '{\"jurisdictions\":{\"sk\":\"ok\",\"cz\":\"ok\"}}'::jsonb WHERE id = $1",
            data.Proposals["zubna.3"]);
        using (var accepted = await SendAsync(owner, HttpMethod.Post, proposal + "/accept", null, await ETagAsync(owner, proposal)))
        {
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        // 3. The bulk fix.
        var group = $"{shop}/fix-groups/{data.Groups["group"]}";
        using (var values = await SendAsync(owner, HttpMethod.Put, group + "/values", new { values = new Dictionary<string, string> { ["materiál obalu"] = Fact } },
            await ETagAsync(owner, group)))
        {
            Assert.Equal(HttpStatusCode.Accepted, values.StatusCode);
        }

        await AdminAsync("UPDATE fixes.fix_groups SET recheck_status = 'ok' WHERE id = $1", data.Groups["group"]);
        using (var approved = await SendAsync(owner, HttpMethod.Post, group + "/approve", null, await ETagAsync(owner, group)))
        {
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        }

        // 4. The answer „Áno“ and the evidence.
        using (var answered = await SendAsync(owner, HttpMethod.Post, $"{shop}/questions/{data.Questions["vodnar"]}/answer", new { answer = "yes" }, null))
        {
            Assert.Equal(HttpStatusCode.OK, answered.StatusCode);
        }

        using (var uploaded = await UploadAsync(owner, Metadata(claim: "Vegan"), Pdf, "vegan.pdf"))
        {
            Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        }

        // 5. Publication through the connector and „Kopírovať text“.
        using (var published = await SendAsync(owner, HttpMethod.Post, $"{shop}/publications", new { pageIds = new[] { data.Pages["zubna"].PageId } }, null))
        {
            Assert.Equal(HttpStatusCode.Accepted, published.StatusCode);
            Assert.NotEmpty((await ApiClient.JsonAsync(published)).GetProperty("publications").EnumerateArray());
        }

        using (var copied = await owner.Browser.GetAsync($"{shop}/pages/{data.Pages["zubna"].PageId}/fixed-text?field=block"))
        {
            Assert.Contains(OwnWording, (await ApiClient.JsonAsync(copied)).GetProperty("text").GetString(), StringComparison.Ordinal);
        }

        // 6. The protocol.
        using (var protocol = await SendAsync(owner, HttpMethod.Post, $"{shop}/protocols", new { periodFrom = "2026-09-30", periodTo = "2026-10-31" }, null))
        {
            Assert.Equal(HttpStatusCode.Accepted, protocol.StatusCode);
        }

        // 7. Notifications and the stream: the change of the proposal was told.
        using (var notifications = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}/notifications"))
        {
            Assert.Equal(HttpStatusCode.OK, notifications.StatusCode);
        }

        using (var reader = new StreamReader(await stream.Content.ReadAsStreamAsync(Ct)))
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            limit.CancelAfter(TimeSpan.FromSeconds(20));
            var seen = new List<string>();
            while (!seen.Contains("event: proposal") && await reader.ReadLineAsync(limit.Token) is { } line)
            {
                seen.Add(line);
            }

            Assert.Contains("event: ready", seen);
            Assert.Contains("event: proposal", seen);
        }

        Assert.Equal("approved", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", data.Findings["zubna.3"]));
        Assert.Equal("kept_with_evidence", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", data.Findings["vodnar"]));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind = 'publish.fix' AND shop_id = $1", data.ShopId));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind = 'protocol.render' AND shop_id = $1", data.ShopId));

        // Texts of findings, proposals, facts and the content of the evidence never get into the logs.
        var texts = new[]
        {
            OwnWording, Fact, "Bambusová kefka – ekologická alternatíva", "Všetky naše produkty balíme ekologicky.", "Okamžitý komfort pre citlivé zuby",
            "Doba horenia", "obsah-dokladu-123",
        };
        Assert.NotEmpty(factory.Logs.Logs);
        foreach (var log in factory.Logs.Logs)
        {
            Assert.All(texts, t => Assert.DoesNotContain(t, log.AllText, StringComparison.OrdinalIgnoreCase));
        }
    }
}
