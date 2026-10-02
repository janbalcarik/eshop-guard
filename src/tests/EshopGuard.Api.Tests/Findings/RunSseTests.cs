using System.Net;
using System.Text.Json;
using EshopGuard.Application.Runs;
using Microsoft.Extensions.DependencyInjection;
using static EshopGuard.Api.Tests.Findings.ProposalTests;
using static EshopGuard.Api.Tests.Findings.ShopSeed;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>
/// Runs and their live progress (change 11, tasks 11.4 and 11.5; AD 13): the stream of a run in its order, resumed after
/// <c>Last-Event-ID</c>, ended after the final state; a run of another tenant is 404, the 11th stream of a user 429, and a
/// notification of another tenant never reaches a stream.
/// </summary>
public sealed class RunSseTests : FindingsTestBase
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task Stream_SendsSnapshotEventsProgressAndStatus_InOrder_AndEndsAfterTheFinalState()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var (shopId, runId) = await RunAsync(owner);
        await EventAsync(owner, runId, "crawl.started");

        using var response = await OpenAsync(owner, runId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        Assert.Equal("no", response.Headers.GetValues("X-Accel-Buffering").Single());
        await using var events = new SseReader(await response.Content.ReadAsStreamAsync(Ct));

        var snapshot = await events.NextAsync(Wait);
        Assert.Equal(("snapshot", "crawling"), (snapshot.Event, snapshot.Data.GetProperty("status").GetString()));
        Assert.Equal(shopId, snapshot.Data.GetProperty("shopId").GetGuid());
        var first = await events.NextAsync(Wait);
        Assert.Equal(("run_event", "crawl.started"), (first.Event, first.Data.GetProperty("code").GetString()));

        var second = await EventAsync(owner, runId, "crawl.batch_done");
        var next = await events.NextAsync(Wait);
        Assert.Equal(("run_event", second.ToString(System.Globalization.CultureInfo.InvariantCulture)), (next.Event, next.Id));

        await AdminAsync("UPDATE checks.runs SET progress = '{\"pages_planned\":120,\"pages_fetched\":60,\"pages_processed\":10}' WHERE id = $1", runId);
        var progress = await events.NextAsync(Wait);
        Assert.Equal(("progress", 60L), (progress.Event, progress.Data.GetProperty("pagesFetched").GetInt64()));

        await AdminAsync("UPDATE checks.runs SET status = 'finished', finished_at = now() WHERE id = $1", runId);
        var status = await events.NextAsync(Wait);
        var end = await events.NextAsync(Wait);
        Assert.Equal(("status", "finished"), (status.Event, status.Data.GetProperty("status").GetString()));
        Assert.Equal("end", end.Event);
        Assert.True(await events.EndedAsync(Wait));
    }

    [Fact]
    public async Task LastEventId_ResumesAfterIt()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var (_, runId) = await RunAsync(owner);
        await EventAsync(owner, runId, "crawl.started");
        var seen = await EventAsync(owner, runId, "crawl.batch_done");
        var missed = await EventAsync(owner, runId, "crawl.finished");

        using var response = await OpenAsync(owner, runId, lastEventId: seen);
        await using var events = new SseReader(await response.Content.ReadAsStreamAsync(Ct));

        Assert.Equal("snapshot", (await events.NextAsync(Wait)).Event);
        var resumed = await events.NextAsync(Wait);
        Assert.Equal((missed.ToString(System.Globalization.CultureInfo.InvariantCulture), "crawl.finished"), (resumed.Id, resumed.Data.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task RunOfAnotherTenant_Is404_AndThe11thStreamOfAUser_Is429()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        using var other = await People.OwnerAsync(factory);
        var (_, foreign) = await RunAsync(other);
        var (_, runId) = await RunAsync(owner);

        using (var refused = await owner.Browser.Http.GetAsync($"/api/t/{owner.TenantId}/runs/{foreign}/events", Ct))
        {
            await ProblemAsync(refused, HttpStatusCode.NotFound, "run.not_found");
        }

        var open = new List<HttpResponseMessage>();
        try
        {
            for (var i = 0; i < 10; i++)
            {
                open.Add(await OpenAsync(owner, runId));
                Assert.Equal(HttpStatusCode.OK, open[^1].StatusCode);
            }

            using var eleventh = await OpenAsync(owner, runId);
            await ProblemAsync(eleventh, HttpStatusCode.TooManyRequests, "sse.too_many_connections");
        }
        finally
        {
            open.ForEach(r => r.Dispose());
        }
    }

    [Fact]
    public async Task NotificationOfAnotherTenant_NeverReachesTheStream()
    {
        await using var factory = Factory();
        var stream = factory.Services.GetRequiredService<RunEventStream>();
        var (tenantA, tenantB, runId, shopId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        using var run = stream.Subscribe(tenantA, Guid.NewGuid(), runId, null);
        using var shop = stream.Subscribe(tenantA, Guid.NewGuid(), null, shopId);

        stream.Dispatch("eg_run", $"{tenantB:D}:{runId:D}:5");
        stream.Dispatch("eg_shop", $"{tenantB:D}:{shopId:D}:proposal:{Guid.NewGuid():D}");

        Assert.False(run.Signals.TryRead(out _));
        Assert.False(shop.Signals.TryRead(out _));

        stream.Dispatch("eg_run", $"{tenantA:D}:{runId:D}:5");
        stream.Dispatch("eg_shop", $"{tenantA:D}:{shopId:D}:proposal:{runId:D}");

        Assert.True(run.Signals.TryRead(out var signal) && signal.RunEventId == 5);
        Assert.True(shop.Signals.TryRead(out var change) && change.Entity == "proposal");
    }

    [Fact]
    public async Task ShopStream_SaysWhatChanged()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;

        using var response = await owner.Browser.Http.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"{S(owner, data.ShopId)}/events"), HttpCompletionOption.ResponseHeadersRead, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode} {(response.StatusCode == HttpStatusCode.OK ? "" : await response.Content.ReadAsStringAsync(Ct))}");
        await using var events = new SseReader(await response.Content.ReadAsStreamAsync(Ct));
        Assert.Equal("ready", (await events.NextAsync(Wait)).Event);

        await AdminAsync("UPDATE fixes.fix_proposals SET recheck_status = 'ok' WHERE id = $1", data.Proposals["group"]);
        var change = await events.NextAsync(Wait);

        Assert.Equal(("proposal", "ok"), (change.Event, change.Data.GetProperty("recheckStatus").GetString()));
        Assert.Equal(data.Proposals["group"], change.Data.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task ListDetailAndCancel_OnlyTheFreeSampleOrRecheck()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var (shopId, sample) = await RunAsync(owner);
        var (_, analysis) = await RunAsync(owner, kind: "full_analysis", shopId: shopId);

        using (var list = await owner.Browser.GetAsync($"{S(owner, shopId)}/runs?kind=free_sample"))
        {
            var body = await ApiClient.JsonAsync(list);
            Assert.Equal(sample, Assert.Single(body.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        }

        using (var refused = await SendAsync(owner, HttpMethod.Post, $"/api/t/{owner.TenantId}/runs/{analysis}/cancel", null, null))
        {
            var problem = await ProblemAsync(refused, HttpStatusCode.Conflict, "run.not_cancelable");
            Assert.Equal("full_analysis", problem.GetProperty("params").GetProperty("kind").GetString());
        }

        using var canceled = await SendAsync(owner, HttpMethod.Post, $"/api/t/{owner.TenantId}/runs/{sample}/cancel", null, null);

        Assert.Equal(HttpStatusCode.Accepted, canceled.StatusCode);
        Assert.True(await AdminScalarAsync<bool>("SELECT cancel_requested FROM checks.runs WHERE id = $1", sample));
        using var detail = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}/runs/{sample}");
        Assert.True((await ApiClient.JsonAsync(detail)).GetProperty("cancelRequested").GetBoolean());
    }

    private static async Task<(Guid ShopId, Guid RunId)> RunAsync(Person owner, string kind = "free_sample", Guid? shopId = null)
    {
        var shop = shopId ?? (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        var runId = Guid.CreateVersion7();
        await ExecuteAsync(
            """
            INSERT INTO checks.runs (id, tenant_id, shop_id, kind, trigger, status, priority, jurisdictions, modules, progress, started_at, created_at, updated_at)
            VALUES ($1, $2, $3, $4, 'user', 'crawling', 0, '{sk}', '{eco}', '{"pages_planned":120,"pages_fetched":10,"pages_processed":0}'::jsonb, now(), now(), now())
            """, runId, owner.TenantId, shop, kind);
        return (shop, runId);
    }

    private static Task<long> EventAsync(Person owner, Guid runId, string code) => ScalarAsync<long>(
        "INSERT INTO checks.run_events (at, tenant_id, run_id, level, code, message, data, created_at) VALUES (now(), $1, $2, 'info', $3, $3, '{}'::jsonb, now()) RETURNING id",
        owner.TenantId, runId, code);

    private static async Task<HttpResponseMessage> OpenAsync(Person person, Guid runId, long? lastEventId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/t/{person.TenantId}/runs/{runId}/events");
        if (lastEventId is { } id)
        {
            request.Headers.Add("Last-Event-ID", id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return await person.Browser.Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, Ct);
    }

    /// <summary>Reads server-sent events (comments skipped).</summary>
    private sealed class SseReader(Stream stream) : IAsyncDisposable
    {
        private readonly StreamReader reader = new(stream);

        public async Task<(string Event, string? Id, JsonElement Data)> NextAsync(TimeSpan timeout)
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            limit.CancelAfter(timeout);
            string? name = null;
            string? id = null;
            string? data = null;
            while (await reader.ReadLineAsync(limit.Token) is { } line)
            {
                if (line.Length == 0)
                {
                    if (name is not null)
                    {
                        return (name, id, JsonDocument.Parse(data ?? "{}").RootElement.Clone());
                    }

                    continue;
                }

                if (line.StartsWith("event: ", StringComparison.Ordinal))
                {
                    name = line[7..];
                }
                else if (line.StartsWith("id: ", StringComparison.Ordinal))
                {
                    id = line[4..];
                }
                else if (line.StartsWith("data: ", StringComparison.Ordinal))
                {
                    data = line[6..];
                }
            }

            throw new EndOfStreamException();
        }

        public async Task<bool> EndedAsync(TimeSpan timeout)
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            limit.CancelAfter(timeout);
            return await reader.ReadLineAsync(limit.Token) is null;
        }

        public ValueTask DisposeAsync()
        {
            reader.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
