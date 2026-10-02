using EshopGuard.Core.Markets;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Pipeline;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Jobs.Runs;
using EshopGuard.Jobs.Runs.Handlers;
using Finding = EshopGuard.Core.Models.Finding;
using FindingBand = EshopGuard.Core.Models.FindingBand;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>The pure parts of the runs (no database): state machine, order of findings, unchecked, scope basis, estimates.</summary>
public sealed class RunUnitTests
{
    [Theory]
    [InlineData(RunStatus.Queued, RunStatus.Discovering, true)]
    [InlineData(RunStatus.Discovering, RunStatus.AwaitingPayment, true)]
    [InlineData(RunStatus.Discovering, RunStatus.Crawling, true)]
    [InlineData(RunStatus.AwaitingPayment, RunStatus.Crawling, true)]
    [InlineData(RunStatus.Crawling, RunStatus.Profiling, true)]
    [InlineData(RunStatus.Rewriting, RunStatus.Partial, true)]
    [InlineData(RunStatus.Evaluating, RunStatus.Failed, true)]
    [InlineData(RunStatus.Crawling, RunStatus.Canceled, true)]
    [InlineData(RunStatus.Crawling, RunStatus.Ruling, false)]
    [InlineData(RunStatus.Queued, RunStatus.Crawling, false)]
    [InlineData(RunStatus.Finished, RunStatus.Failed, false)]
    [InlineData(RunStatus.Canceled, RunStatus.Queued, false)]
    [InlineData(RunStatus.Failed, RunStatus.Canceled, false)]
    public void StateMachine_AllowsOnlyTheTransitionsOfTheDesign(RunStatus from, RunStatus to, bool allowed) =>
        Assert.Equal(allowed, RunStateMachine.IsAllowed(from, to));

    [Fact]
    public void SampleOrder_TakesTheStrictestVerdictFirst_ThenScoreOccurrencesAndRule()
    {
        var violation = Finding("eco.b", ("sk", "text", "high", 0.7), urls: 1);
        var assess = Finding("eco.a", ("cz", "assess", "high", 0.99), urls: 9);
        var higherScore = Finding("eco.c", ("sk", "text", "high", 0.9), urls: 1);
        var moreOccurrences = Finding("eco.d", ("sk", "text", "high", 0.7), urls: 3);
        var sameButLaterRule = Finding("eco.e", ("sk", "text", "high", 0.7), urls: 1);

        var sorted = SampleFindingOrder.Sort([assess, sameButLaterRule, violation, moreOccurrences, higherScore]);

        Assert.Equal(["eco.c", "eco.d", "eco.b", "eco.e", "eco.a"], sorted.Select(f => f.RuleId));
    }

    [Fact]
    public void Unchecked_FailedAndTooLargePages_MakeTheRunPartial()
    {
        var urls = new List<RunUrlRow>
        {
            Url("a", RunUrlState.Extracted), Url("b", RunUrlState.Failed), Url("c", RunUrlState.Failed), Url("d", RunUrlState.TooLarge),
            Url("e", RunUrlState.RobotsBlocked),
        };

        var report = UncheckedReport.Build(urls, [new CrawlCounters()], notEvaluated: 0, sieveUnanswered: 2, pagesWithoutProfile: 1);

        Assert.True(report.IsPartial);
        Assert.Equal(2, report.Counts["failed"]);
        Assert.Equal(1, report.Counts["too_large"]);
        Assert.Equal(1, report.Counts["robots_blocked"]);
        Assert.Equal(1, report.PagesChecked);
    }

    [Fact]
    public void Unchecked_OnlyRobotsAndTheFilter_IsNotPartial_ButListed()
    {
        var counters = new CrawlCounters { ExcludedByFilter = 3 };
        counters.RobotsBlocked.Add("http://x.test/private");
        var report = UncheckedReport.Build([Url("a", RunUrlState.Extracted)], [counters], 0, 0, 0);

        Assert.False(report.IsPartial);
        Assert.Equal(1, report.Counts["robots_blocked"]);
        Assert.Equal(3, report.Counts["excluded"]);
        Assert.Contains(report.Items, i => i.Url == "http://x.test/private");
    }

    [Fact]
    public void Unchecked_NothingChecked_NamesTheReason()
    {
        var robots = UncheckedReport.Build([Url("a", RunUrlState.RobotsBlocked)], [new CrawlCounters()], 0, 0, 0);
        var down = UncheckedReport.Build([Url("a", RunUrlState.Failed)], [new CrawlCounters()], 0, 0, 0);

        Assert.Equal(RunCodes.RobotsDisallowAll, robots.FailureCode([Url("a", RunUrlState.RobotsBlocked)]));
        Assert.Equal(RunCodes.SiteUnreachable, down.FailureCode([Url("a", RunUrlState.Failed)]));
    }

    [Fact]
    public void ScopeBasis_HasNoBandAndNoAmount_AndSaysWhyAVersionDoesNotCount()
    {
        var analysis = new MarketsAnalysisResult
        {
            Site = "https://bylinkovo.sk/",
            Versions =
            [
                new ShopLanguageRow { Language = "sk", BaseUrl = "https://bylinkovo.sk/", SwitchMethod = "path", Source = "main", Status = "active", Counted = true, ProductCount = 3120, OwnTextShare = 1.0 },
                new ShopLanguageRow { Language = "cs", BaseUrl = "https://bylinkovo.sk/cz/", SwitchMethod = "path", Source = "hreflang", Status = "active", Counted = false, ProductCount = 3090, OwnTextShare = 0.04 },
                new ShopLanguageRow { Language = "pl", BaseUrl = "https://bylinkovo.sk/pl/", SwitchMethod = "path", Source = "hreflang", Status = "unsupported", Counted = false },
            ],
        };

        var basis = ScopeBasisBuilder.Build(analysis, Guid.CreateVersion7(), 5872, 0.2, DateTimeOffset.UnixEpoch);

        var versions = basis["versions"]!.AsArray();
        Assert.Null(versions[0]!["not_counted_reason"]);
        Assert.Equal("menu_only_translation", (string)versions[1]!["not_counted_reason"]!);
        Assert.Equal("unsupported_market", (string)versions[2]!["not_counted_reason"]!);
        Assert.Equal(5872, (int)basis["sitemap_url_count"]!);
        Assert.DoesNotContain("usd", basis.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("band", basis.ToJsonString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Estimate_OfAFreeSample_IsCheckedAgainstItsCap()
    {
        var runs = new RunsOptions();
        var estimate = InternalCostEstimator.Rough(100, runs, new EshopGuardOptions(), DateTimeOffset.UnixEpoch);
        Assert.True(InternalCostEstimator.WithinSampleBudget(estimate, runs));

        InternalCostEstimator.OpenAi(estimate, "rewrite_usd", 5m, DateTimeOffset.UnixEpoch);

        Assert.False(InternalCostEstimator.WithinSampleBudget(estimate, runs));
        Assert.Equal(InternalCostEstimator.Total(estimate), (decimal)estimate["total_usd"]!);
    }

    [Theory]
    [InlineData("WWW.Vegis.sk.", "vegis.sk")]
    [InlineData("vegis.sk", "vegis.sk")]
    [InlineData(" shop.cz ", "shop.cz")]
    public void Domain_OfTheFreeSample_IsNormalized(string domain, string expected) =>
        Assert.Equal(expected, RunService.NormalizeDomain(domain));

    [Theory]
    [InlineData("B3", "block", 3)]
    [InlineData("TITLE", "name", null)]
    [InlineData("META", "short_description", null)]
    [InlineData("JSONLD", "description", null)]
    [InlineData("CHROME", "block", null)]
    public void Proposal_FieldOfABlock(string block, string field, int? index) =>
        Assert.Equal((field, index), ProposalWriter.Field(block));

    private static RunUrlRow Url(string path, RunUrlState state) => new("http://x.test/", "http://x.test/" + path, "sk", state, null, null, null, null, null);

    private static Finding Finding(string rule, (string Jurisdiction, string Checkability, string Severity, double Score) verdict, int urls) => new()
    {
        RuleId = rule,
        Module = "eco",
        Scope = "segment",
        Text = rule,
        Urls = Enumerable.Range(0, urls).Select(i => $"http://x.test/{rule}/{i}").ToList(),
        Verdicts =
        [
            new JurisdictionVerdict
            {
                Jurisdiction = verdict.Jurisdiction, Checkability = verdict.Checkability, Severity = verdict.Severity, Score = verdict.Score,
                Band = FindingBand.High, RuleSet = "eco", RuleSetVersion = "1",
            },
        ],
    };
}

/// <summary>HTML and extractions of a run in the file store, for the tenant, e-shop and run of the job only.</summary>
public sealed class BlobPageContentStoreTests
{
    [Fact]
    public async Task Content_IsKeptPerRun_AndNeverWithoutTheContextOfAJob()
    {
        var blobs = new EshopGuard.Storage.FileSystemBlobStore(Directory.CreateTempSubdirectory("eshopguard-content-").FullName);
        var store = new EshopGuard.Jobs.Runs.Storage.BlobPageContentStore(blobs);
        var key = new EshopGuard.Core.Storage.PageContentKey("shop.test", "https://shop.test/a");
        var tenant = Guid.CreateVersion7();
        var shop = Guid.CreateVersion7();
        var ct = TestContext.Current.CancellationToken;

        using (RunAmbient.Enter(new RunAmbientScope(tenant, shop, Guid.CreateVersion7(), 1)))
        {
            await store.PutHtmlAsync(key, [1, 2, 3], ct);
            await store.PutExtractAsync(key, "{\"a\":1}", ct);
            Assert.Equal([1, 2, 3], await store.GetHtmlAsync(key, ct));
            Assert.Equal("{\"a\":1}", await store.GetExtractAsync(key, ct));
        }

        using (RunAmbient.Enter(new RunAmbientScope(tenant, shop, Guid.CreateVersion7(), 2)))
        {
            Assert.Null(await store.GetHtmlAsync(key, ct));
            Assert.Null(await store.GetExtractAsync(key, ct));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetHtmlAsync(key, ct));
    }
}
