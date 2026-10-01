using EshopGuard.Core.Crawl;
using EshopGuard.Core.Options;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>Recording of the answers of a site and their replay without network (comparison of old and new code, change 5).</summary>
public sealed class RecordReplayTests
{
    [Fact]
    public async Task RecordingAndReplay_GiveTheSameScan()
    {
        var folder = Directory.CreateTempSubdirectory("eshopguard-recording-").FullName;
        var recorder = new RecordingPageFetcher(FileSystemPageFetcher.ForSlovakFixture(), folder);
        var recorded = await ScanAsync(recorder);

        var replay = new ReplayPageFetcher(folder);
        var replayed = await ScanAsync(replay);

        Assert.True(File.Exists(Path.Combine(folder, PageRecording.IndexFile)));
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(folder, PageRecording.BodiesFolder)));
        Assert.Equal(recorded, replayed);
        Assert.Contains("findings.json", (IDictionary<string, string>)replayed);
    }

    [Fact]
    public async Task Replay_AnswersUnknownUrlWith404_AndRepeatsTheRecordedOrder()
    {
        var folder = Directory.CreateTempSubdirectory("eshopguard-recording-").FullName;
        var recorder = new RecordingPageFetcher(new SequenceFetcher(), folder);
        var url = new Uri("http://fixture.test/busy");
        await recorder.FetchAsync(url, TestContext.Current.CancellationToken);
        await recorder.FetchAsync(url, TestContext.Current.CancellationToken);

        var replay = new ReplayPageFetcher(folder);

        Assert.Equal(2, replay.Count);
        var first = await replay.FetchAsync(url, TestContext.Current.CancellationToken);
        Assert.Equal(429, first.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(3), first.RetryAfter);
        var second = await replay.FetchAsync(url, TestContext.Current.CancellationToken);
        Assert.Equal(200, second.StatusCode);
        Assert.Equal("\"v1\"", second.ETag);
        Assert.Equal("hello"u8.ToArray(), second.Body);
        Assert.Equal(200, (await replay.FetchAsync(url, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(404, (await replay.FetchAsync(new Uri("http://fixture.test/missing"), TestContext.Current.CancellationToken)).StatusCode);
    }

    private static async Task<SortedDictionary<string, string>> ScanAsync(IPageFetcher fetcher)
    {
        await using var provider = TestServices.Create(fetcher);
        var result = await provider.GetRequiredService<IEshopGuard>().ScanSiteAsync(
            FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions { Country = "sk" }, ct: TestContext.Current.CancellationToken);
        return await OutputNormalizer.WriteAsync(provider, result);
    }

    /// <summary>First a 429 with Retry-After, then the page.</summary>
    private sealed class SequenceFetcher : IPageFetcher
    {
        private int _calls;

        public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct) => Task.FromResult(++_calls == 1
            ? new FetchResponse { Url = url, StatusCode = 429, RetryAfter = TimeSpan.FromSeconds(3) }
            : new FetchResponse { Url = url, StatusCode = 200, MediaType = "text/html", Charset = "utf-8", Body = "hello"u8.ToArray(), ETag = "\"v1\"" });
    }
}
