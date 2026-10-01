using EshopGuard.Core.Fix;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Report;
using EshopGuard.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Rewrite of problematic passages: the prompt (shared part first, page last), the check of the new text by the rules
/// with mock Jev, the cache and the whole path from a scan's output files to rewrite.md.
/// </summary>
public class RewriteTests
{
    private const string Url = "https://shop.example/produkt";

    [Fact]
    public async Task SharedPartIsTheSameForEveryPageAndThePageComesLast()
    {
        var client = new RecordingClient(_ => """{"changes":[],"kept":[]}""");
        await using var provider = Create(client);
        var input = new RewriteInput
        {
            Pages = [Page(Url, "Tento šampón je ekologický."), Page(Url + "-2", "Toto mydlo je ekologické.")],
            Findings = [Finding("Tento šampón je ekologický.", Url), Finding("Toto mydlo je ekologické.", Url + "-2")],
        };

        await provider.GetRequiredService<ITextRewriter>().RewriteAsync(input, ct: TestContext.Current.CancellationToken);

        Assert.Equal(2, client.Requests.Count);
        Assert.Equal(client.Requests[0].SharedPart, client.Requests[1].SharedPart);
        Assert.DoesNotContain("shop.example", client.Requests[0].SharedPart, StringComparison.Ordinal);
        Assert.Contains("Example 10.", client.Requests[0].SharedPart, StringComparison.Ordinal);
        Assert.Contains("príloha č. 1", client.Requests[0].SharedPart, StringComparison.Ordinal);
        Assert.Contains("[B1] Tento šampón je ekologický.", client.Requests[0].PagePart, StringComparison.Ordinal);
        Assert.Contains("F1 | skupina: porušení", client.Requests[0].PagePart, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangesAreCheckedByTheRulesAndGetTheirStatus()
    {
        var page = Page(Url, "Tento šampón je ekologický.\nTento kondicionér je ekologický.\nToto mydlo je ekologické.\nTáto kefa je ekologická.\nTento gél je ekologický.");
        var client = new RecordingClient(_ => """
            {"changes":[
              {"block_ids":["B1"],"original":"","rewritten":"Tento šampón je bez parabénov.","finding_ids":["F1"],"placeholders":[],"reason_cs":"a"},
              {"block_ids":["B2"],"original":"","rewritten":"Tento kondicionér je ekologický [doplňte: v čom presne].","finding_ids":["F2"],"placeholders":["[doplňte: v čom presne]"],"reason_cs":"b"},
              {"block_ids":["B3"],"original":"","rewritten":"Toto mydlo je ekologické.","finding_ids":["F3"],"placeholders":[],"reason_cs":"c"},
              {"block_ids":["B5"],"original":"","rewritten":"Tento gél je z rastlín [doplňte: z akých]. Je ekologický.","finding_ids":["F5"],"placeholders":["[doplňte: z akých]"],"reason_cs":"e"},
              {"block_ids":["B99"],"original":"","rewritten":"Neznámy blok.","finding_ids":["F9"],"placeholders":[],"reason_cs":"d"}],
             "kept":[{"finding_id":"F4","reason_cs":"popis zloženia"}]}
            """);
        await using var provider = Create(client);
        var input = new RewriteInput
        {
            Pages = [page],
            Findings = page.MainText.Split('\n').Select(line => Finding(line, Url)).ToList(),
        };

        var result = await provider.GetRequiredService<ITextRewriter>().RewriteAsync(input, ct: TestContext.Current.CancellationToken);

        var rewritten = Assert.Single(result.Pages);
        Assert.Equal(4, rewritten.Changes.Count);
        Assert.Equal("Tento šampón je ekologický.", rewritten.Changes[0].Original);
        Assert.Equal(
            // F5: a placeholder in one sentence does not hide a finding in another sentence of the block.
            [RewriteStatus.Resolved, RewriteStatus.WaitingForFacts, RewriteStatus.StillFinding, RewriteStatus.Kept, RewriteStatus.StillFinding],
            rewritten.Findings.Select(f => f.Status));
        Assert.Contains(rewritten.Changes[2].RemainingFindings, f => f.RuleId == "eco_generic_claim");

        // The whole page before and after: changed blocks carry the new text, the kept one stays as it was.
        Assert.Equal(["B1", "B2", "B3", "B4", "B5"], rewritten.Blocks.Select(b => b.Id));
        Assert.Equal("Tento šampón je bez parabénov.", rewritten.Blocks[0].Rewritten);
        Assert.True(rewritten.Blocks[0].Changed);
        Assert.False(rewritten.Blocks[3].Changed);
        Assert.Equal(rewritten.Blocks[3].Original, rewritten.Blocks[3].Rewritten);
        Assert.True(result.Stats.CheckCalls > 0);
    }

    [Fact]
    public async Task SecondRunComesFromTheCacheAndCostsNothing()
    {
        var client = new RecordingClient(_ => """{"changes":[],"kept":[{"finding_id":"F1","reason_cs":"x"}]}""");
        var cache = new MemoryCache();
        await using var provider = Create(client, cache);
        var input = new RewriteInput { Pages = [Page(Url, "Tento šampón je ekologický.")], Findings = [Finding("Tento šampón je ekologický.", Url)] };
        var rewriter = provider.GetRequiredService<ITextRewriter>();

        var first = await rewriter.RewriteAsync(input, ct: TestContext.Current.CancellationToken);
        var estimate = await rewriter.EstimateAsync(input, TestContext.Current.CancellationToken);
        var second = await rewriter.RewriteAsync(input, ct: TestContext.Current.CancellationToken);

        Assert.Single(client.Requests);
        Assert.False(first.Pages[0].FromCache);
        Assert.True(second.Pages[0].FromCache);
        Assert.Equal(1, estimate.CachedPages);
        Assert.Equal(0m, estimate.EstimatedCostUsd);
        Assert.Equal(RewriteStatus.Kept, second.Pages[0].Findings[0].Status);
    }

    [Fact]
    public async Task ScanOutputOfTheFixtureShopIsRewrittenAndReported()
    {
        var directory = Directory.CreateTempSubdirectory("rewrite-test-").FullName;
        try
        {
            await using (var scanProvider = TestServices.Create(FileSystemPageFetcher.ForSlovakFixture()))
            {
                var scan = await scanProvider.GetRequiredService<IEshopGuard>().ScanSiteAsync(
                    FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions { Country = "sk" }, ct: TestContext.Current.CancellationToken);
                foreach (var writer in scanProvider.GetServices<IReportWriter>())
                {
                    await writer.WriteAsync(scan, directory, TestContext.Current.CancellationToken);
                }
            }

            var input = ScanOutputReader.Read(directory);
            await using var provider = TestServices.Create(FileSystemPageFetcher.ForSlovakFixture());
            var result = await provider.GetRequiredService<ITextRewriter>().RewriteAsync(input, ct: TestContext.Current.CancellationToken);
            await RewriteReportWriter.WriteAsync(result, directory, TestTexts.Renderer, "cs", TestContext.Current.CancellationToken);

            Assert.Equal(0, result.Stats.Errors);
            var shampoo = result.Pages.Single(p => p.Url.EndsWith("/produkt-1.html", StringComparison.Ordinal));
            var change = Assert.Single(shampoo.Changes, c => c.Original.Contains("ekologický a šetrný k prírode", StringComparison.Ordinal));
            Assert.Equal(RewriteStatus.Resolved, change.Status);
            var markdown = await File.ReadAllTextAsync(Path.Combine(directory, RewriteReportWriter.MarkdownFile), TestContext.Current.CancellationToken);
            Assert.Contains("Původně: ", markdown, StringComparison.Ordinal);
            Assert.Contains("### Celý opravený text", markdown, StringComparison.Ordinal);
            Assert.Contains("> **Tento šampón je", markdown, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(directory, RewriteReportWriter.JsonFile)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Batches_RewriteTheSamePagesAsOneRun()
    {
        var client = new RecordingClient(_ => """{"changes":[],"kept":[]}""");
        await using var provider = Create(client);
        var urls = Enumerable.Range(1, 8).Select(i => $"{Url}-{i}").Reverse().ToList();
        var input = new RewriteInput
        {
            Pages = [.. urls.Select(u => Page(u, "Tento šampón je ekologický.")), Page(Url + "-bez-nalezu", "Text.")],
            Findings = [.. urls.Select(u => Finding("Tento šampón je ekologický.", u))],
            MaxPages = 7,
        };

        var batches = Pipeline.RewriteStep.Batches(input, batchPages: 3);
        var one = await provider.GetRequiredService<ITextRewriter>().RewriteAsync(input, ct: TestContext.Current.CancellationToken);
        var step = provider.GetRequiredService<Pipeline.RewriteStep>();
        var results = new List<RewriteResult>();
        foreach (var batch in batches)
        {
            results.Add(await step.RewriteBatchAsync(batch, null, TestContext.Current.CancellationToken));
        }

        var merged = Pipeline.RewriteStep.Merge(results);

        Assert.Equal([3, 3, 1], batches.Select(b => b.Pages.Count));
        Assert.Equal(urls.Order(StringComparer.Ordinal).Take(7), batches.SelectMany(b => b.Pages).Select(p => p.Url));
        Assert.All(batches, b => Assert.Equal(b.Pages.Select(p => p.Url), b.Findings.Select(f => f.Urls[0])));
        Assert.Equal(one.Pages.Select(p => p.Url), merged.Pages.Select(p => p.Url));
        Assert.Equal(one.Stats.Pages, merged.Stats.Pages);
        Assert.Equal(one.Stats.CheckCalls, merged.Stats.CheckCalls);
        Assert.Empty(merged.Warnings);
    }

    internal static ServiceProvider Create(IRewriteClient client, IRewriteCache? cache = null, Action<EshopGuardOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(client);
        services.AddSingleton(cache ?? new MemoryCache());
        services.AddEshopGuard(options =>
        {
            options.Jev.UseMock = true;
            options.Rules.Directory = TestServices.RulesDirectory;
            options.Rules.LabelsFile = TestServices.LabelsFile;
            options.Rules.LegalRequirementsFile = TestServices.LegalRequirementsFile;
            options.Rules.SieveFile = TestServices.SieveFile;
            options.Rules.JurisdictionsFile = TestServices.JurisdictionsFile;
            options.Rewrite.PromptFile = TestServices.RewritePromptFile;
            configure?.Invoke(options);
        });
        return services.BuildServiceProvider();
    }

    internal static RewritePageInput Page(string url, string mainText) => new() { Url = url, Type = "product", Title = "Produkt", MainText = mainText };

    private static Finding Finding(string text, string url) => new()
    {
        RuleId = "eco_generic_claim",
        Module = "eco",
        Scope = "segment",
        Text = text,
        Urls = [url],
        Sources = [SegmentSource.Main],
        Verdicts = [new JurisdictionVerdict { Jurisdiction = "sk", Severity = "high", Checkability = "text", RuleSet = "eco", RuleSetVersion = "eco-test" }],
    };

    internal sealed class RecordingClient(Func<RewriteRequest, string> answer) : IRewriteClient
    {
        public List<RewriteRequest> Requests { get; } = [];

        public Task<RewriteResponse> RewriteAsync(RewriteRequest request, CancellationToken ct)
        {
            lock (Requests)
            {
                Requests.Add(request);
            }

            return Task.FromResult(new RewriteResponse { Json = answer(request), Model = "test", InputTokens = 100, OutputTokens = 10 });
        }
    }

    private sealed class MemoryCache : IRewriteCache
    {
        private readonly Dictionary<string, (string Json, string? Model)> _items = [];

        public Task<(string Json, string? Model)?> GetAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(_items.TryGetValue(key, out var item) ? item : ((string Json, string? Model)?)null);

        public Task SetAsync(string key, string json, string? model, CancellationToken ct = default)
        {
            _items[key] = (json, model);
            return Task.CompletedTask;
        }
    }
}
