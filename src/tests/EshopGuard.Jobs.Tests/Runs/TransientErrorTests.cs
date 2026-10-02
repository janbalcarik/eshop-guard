using EshopGuard.Core.Crawl;
using EshopGuard.Core.Fix;
using EshopGuard.Core.Jev;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Runs;
using EshopGuard.Jobs.Tests.Runs.Support;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>
/// Temporary and fatal errors of the services and of the e-shop heal without the user (design of change 8, "Chyby a
/// samooprava"; task 11.7). The queue repeats a batch with a growing delay (<c>Jobs:Retry</c> of the tests: 0.2 s, at most 1 s).
/// </summary>
public sealed class TransientErrorTests(JobsTestDatabase database) : JobsTestBase(database)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    [Fact]
    public async Task JevUnavailableForAWhile_TheBatchesAreRepeated_AndTheRunEndsWithEverythingAnswered()
    {
        var reference = await RunAsync(new DeterministicTestJevClient());
        var jev = new OutageJevClient(failFirst: 25);

        var (shop, runId, status, _) = await RunAsync(jev);

        Assert.Equal(reference.Status, status);
        Assert.True(jev.Failed >= 25, $"failures {jev.Failed}");
        var stats = await RunTests.RunJsonAsync(Db, shop, runId, "stats");
        Assert.Null(stats["unchecked"]?["not_evaluated"]);
        Assert.Null(stats["unchecked"]?["sieve_unanswered"]);
        var repeated = await Db.ScalarAsync<int>("SELECT max(attempts) FROM ops.jobs WHERE run_id = $1 AND kind IN ('run.sieve', 'run.evaluate')", runId);
        Assert.True(repeated > 1, "a batch of Jev was repeated");
        Assert.Equal(await FindingCountAsync(reference.Shop), await FindingCountAsync(shop));
    }

    [Fact]
    public async Task JevUnavailableThroughEveryAttempt_TheRunEndsPartial_AndIsNeverLeftUnfinished()
    {
        var jev = new OutageJevClient(failFirst: int.MaxValue);

        var (shop, runId, status, _) = await RunAsync(jev);

        Assert.Equal("partial", status);
        var stats = await RunTests.RunJsonAsync(Db, shop, runId, "stats");
        Assert.True((long?)stats["unchecked"]?["not_evaluated"] > 0, stats.ToJsonString());
        Assert.Equal(RunPlan.BatchMaxAttempts, await Db.ScalarAsync<int>("SELECT max(attempts) FROM ops.jobs WHERE run_id = $1 AND kind = 'run.evaluate'", runId));
        Assert.Equal(0L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE run_id = $1 AND state = 'failed'", runId));
    }

    [Fact]
    public async Task OpenAiOutOfCredit_PausesTheLlmClass_WithoutUsingAnAttempt_AndTheRunEndsAfterResume()
    {
        var rewrite = new CreditOnceRewriteClient();
        var shop = await RunTests.CreateShopAsync();
        var storage = Directory.CreateTempSubdirectory("eshopguard-transient-").FullName;
        var services = RunTests.Services(RunTests.SlovakSite(shop.BaseUrl), new DeterministicTestJevClient());
        var worker = await Workers.StartAsync("w0", RunTests.WorkerSettings(storage), s =>
        {
            s.AddSingleton<IRewriteClient>(rewrite);
            services(s);
        });
        var runId = await StartAsync(worker, shop);

        await WaitAsync(async () => (await Direct.Queue.GetPausedClassesAsync(Ct)).Contains(JobResourceClass.Llm));
        Assert.Equal(1L, await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM checks.run_events WHERE run_id = $1 AND code = 'run.paused_internal'", runId));
        var events = await RunTests.RowsAsync(Db, shop.TenantId, "SELECT data::text FROM checks.run_events WHERE run_id = $1 AND code = 'run.paused_internal'", runId);
        Assert.DoesNotContain("openai", (string)events[0][0]!, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual("finished", await RunTests.ScalarAsync<string>(Db, shop.TenantId, "SELECT status FROM checks.runs WHERE id = $1", runId));

        await Direct.Queue.ResumeResourceClassAsync(JobResourceClass.Llm, Ct);
        var status = await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], RunTests.Final);

        Assert.True(status is "finished" or "partial", status);
        Assert.Equal(1, await Db.ScalarAsync<int>("SELECT max(attempts) FROM ops.jobs WHERE run_id = $1 AND kind = 'run.rewrite'", runId));
        Assert.True(rewrite.Calls > 1);
    }

    [Fact]
    public async Task ShopAskingToSlowDown_TheAddressIsAskedAgain_AndFailsWithItsStatusAfterTheLimit()
    {
        var (shop, runId, status, fetcher) = await RunAsync(new DeterministicTestJevClient(), url => url.AbsolutePath == "/o-nas.html" ? 429 : null);

        Assert.Equal("partial", status);
        Assert.Equal(3, fetcher.Requested.Count(u => u.AbsolutePath == "/o-nas.html"));
        var rows = await RunTests.RowsAsync(Db, shop.TenantId, "SELECT state, error_code, http_status FROM checks.run_urls WHERE run_id = $1 AND url LIKE '%/o-nas.html'", runId);
        Assert.Equal(["failed", "http_429", (short)429], rows.Single());
        Assert.True(await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM checks.run_events WHERE run_id = $1 AND code = 'crawl.throttled'", runId) > 0);
    }

    [Fact]
    public async Task MissingPage_IsGone_WithItsStatus()
    {
        var (shop, runId, status, _) = await RunAsync(new DeterministicTestJevClient(), url => url.AbsolutePath == "/o-nas.html" ? 404 : null);

        Assert.Equal("partial", status);
        var rows = await RunTests.RowsAsync(Db, shop.TenantId, "SELECT state, error_code, http_status FROM checks.run_urls WHERE run_id = $1 AND url LIKE '%/o-nas.html'", runId);
        Assert.Equal(["gone", "http_404", (short)404], rows.Single());
    }

    private async Task<(RunShop Shop, Guid RunId, string Status, ThrottlingFetcher Fetcher)> RunAsync(IJevClient jev, Func<Uri, int?>? fail = null)
    {
        var shop = await RunTests.CreateShopAsync();
        var storage = Directory.CreateTempSubdirectory("eshopguard-transient-").FullName;
        var fetcher = new ThrottlingFetcher(RunTests.SlovakSite(shop.BaseUrl), fail ?? (_ => null));
        var worker = await Workers.StartAsync("w0", RunTests.WorkerSettings(storage), RunTests.Services(fetcher, jev));
        var runId = await StartAsync(worker, shop);
        var status = await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], RunTests.Final);
        await worker.Host.StopAsync(Ct);
        return (shop, runId, status, fetcher);
    }

    private async Task<Guid> StartAsync(TestWorker worker, RunShop shop)
    {
        var runId = (await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFullAnalysisAsync(shop.ShopId, null, Ct))).RunId!.Value;
        await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], [.. RunTests.Final, "awaiting_payment"]);
        if (await RunTests.ScalarAsync<string>(Db, shop.TenantId, "SELECT status FROM checks.runs WHERE id = $1", runId) == "awaiting_payment")
        {
            await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.ApproveWithoutPaymentAsync(runId, Guid.CreateVersion7(), "test", Ct));
        }

        return runId;
    }

    private Task<long> FindingCountAsync(RunShop shop) =>
        RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM checks.finding_occurrences WHERE shop_id = $1", shop.ShopId);

    private static async Task WaitAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!await condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "timed out");
            await Task.Delay(100, Ct);
        }
    }
}

/// <summary>Jev answers 503 to its first calls (an outage), then like <see cref="DeterministicTestJevClient"/>.</summary>
internal sealed class OutageJevClient(int failFirst) : IJevClient
{
    private readonly DeterministicTestJevClient _inner = new();
    private int _calls;
    private int _failed;

    public int Failed => Volatile.Read(ref _failed);

    public Task<JevResult> EvaluateAsync(object state, IReadOnlyDictionary<string, JevQuestion> questions, CancellationToken ct)
    {
        if (Interlocked.Increment(ref _calls) <= failFirst)
        {
            Interlocked.Increment(ref _failed);
            throw new JevApiException("Service Unavailable", 503, null, isFatal: false);
        }

        return _inner.EvaluateAsync(state, questions, ct);
    }
}

/// <summary>OpenAI out of credit (429 <c>insufficient_quota</c>, fatal) on the first rewrite of findings, then the mock.</summary>
internal sealed class CreditOnceRewriteClient : IRewriteClient
{
    private readonly MockRewriteClient _inner = new();
    private int _calls;
    private int _failed;

    public int Calls => Volatile.Read(ref _calls);

    public Task<RewriteResponse> RewriteAsync(RewriteRequest request, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);
        if (request.Findings.Count > 0 && Interlocked.Exchange(ref _failed, 1) == 0)
        {
            throw new RewriteApiException("insufficient_quota", 429, isFatal: true);
        }

        return _inner.RewriteAsync(request, ct);
    }
}

/// <summary>The fixture with answers of an HTTP status for some addresses; 429 and 503 ask to wait 50 ms.</summary>
internal sealed class ThrottlingFetcher(IPageFetcher inner, Func<Uri, int?> fail) : IPageFetcher
{
    public System.Collections.Concurrent.ConcurrentQueue<Uri> Requested { get; } = new();

    public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct)
    {
        Requested.Enqueue(url);
        return fail(url) is { } status
            ? Task.FromResult(new FetchResponse { Url = url, StatusCode = status, RetryAfter = status is 429 or 503 ? TimeSpan.FromMilliseconds(50) : null })
            : inner.FetchAsync(url, ct);
    }
}
