using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Core.Models;
using EshopGuard.Core.Pipeline;
using EshopGuard.Core.Profiles;
using EshopGuard.Core.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Every input and output of the steps, filled with the data of the Slovak fixture shop, goes through JSON and back
/// unchanged, so a worker can store it between jobs; an unknown schema version is refused (task 2.3).
/// </summary>
public sealed class StepContractSerializationTests
{
    [Fact]
    public async Task EveryContract_RoundTripsThroughJson()
    {
        var records = await BuildRecordsAsync();

        Assert.True(records.Count >= 19, $"{records.Count} records");
        foreach (var (name, record, type) in records)
        {
            var json = JsonSerializer.Serialize(record, type, PipelineJson.Options);
            var back = JsonSerializer.Deserialize(json, type, PipelineJson.Options);
            Assert.True(json == JsonSerializer.Serialize(back, type, PipelineJson.Options), $"{name} se po JSON změnil");
            Assert.True(json.Length > 40, $"{name} je prázdný: {json}");
        }
    }

    [Fact]
    public async Task UnknownSchemaVersion_IsRefused()
    {
        var records = await BuildRecordsAsync();
        var (_, frontier, _) = records.Single(r => r.Name == nameof(UrlFrontierState));
        var node = JsonNode.Parse(PipelineJson.Serialize((UrlFrontierState)frontier))!.AsObject();
        node["schema_version"] = 2;

        var error = Assert.Throws<PipelineSchemaException>(() => PipelineJson.Deserialize<UrlFrontierState>(node.ToJsonString()));

        Assert.Equal(2, error.Version);
        Assert.StartsWith("pipeline.schema_version", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_UsesSnakeCaseNamesAndEnumValues()
    {
        var json = PipelineJson.Serialize(new FetchedPage(new Uri("http://shop.test/a"), new Uri("http://shop.test/b"), FetchOutcome.NotModified, true, 2));

        Assert.Contains("\"requested_url\":\"http://shop.test/a\"", json, StringComparison.Ordinal);
        Assert.Contains("\"outcome\":\"not_modified\"", json, StringComparison.Ordinal);
    }

    /// <summary>The steps run over the fixture shop one by one; every record they take or give is kept.</summary>
    private static async Task<List<(string Name, object Record, Type Type)>> BuildRecordsAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = TestServices.Create(FileSystemPageFetcher.ForSlovakFixture());
        var catalog = await provider.GetRequiredService<IRuleSetProvider>().LoadAsync(ct);
        var ruleSets = RulesStep.SelectRuleSets(catalog, [], "sk", []);
        var sieve = catalog.Sieve!;
        var sieveModules = RulesStep.SieveModules(sieve, ruleSets);
        var records = new List<(string, object, Type)>();
        void Add<T>(T record) where T : notnull => records.Add((typeof(T).Name, record, typeof(T)));

        var site = new SiteScope(FileSystemPageFetcher.DefaultBaseUrl);
        var discoveryInput = new DiscoveryInput(site, new CrawlLimits(200, 100, ["/produkt"], ["/kosik"], 2.5));
        var discovery = await provider.GetRequiredService<DiscoveryStep>().DiscoverAsync(discoveryInput with { Limits = new CrawlLimits(200, 100, [], [], null) }, null, ct);
        Add(discoveryInput);
        Add(discovery);
        Add(discovery.Frontier);

        var profile = new PageProfile
        {
            Id = "fixture.test#1",
            Site = "fixture.test",
            CreatedAt = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero),
            Model = "gpt-6.1-sol",
            PromptVersion = "1",
            SampleUrls = ["http://fixture.test/produkt-1.html"],
            Regions = [new ProfileRegion { Role = "cookie_bar", Action = ProfileRegion.Skip, Selector = ".cookie", Example = "Súhlasím", Reason = "lišta" }],
        };
        var fetchInput = new FetchBatchInput(site, discovery.Robots, discovery.Frontier, discovery.Pace, 100, TimeSpan.FromSeconds(60), ExtractInline: true)
        {
            Validators = new Dictionary<string, ConditionalHeaders> { ["http://fixture.test/"] = new("\"abc\"", new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero)) },
            StoredProfiles = [profile],
        };
        Add(fetchInput);
        var fetched = await provider.GetRequiredService<FetchStep>().FetchBatchAsync(fetchInput with { Validators = new Dictionary<string, ConditionalHeaders>(), StoredProfiles = [] }, null, ct);
        Add(fetched);
        var pages = fetched.Pages.Where(p => p.Extract is not null).Select(p => p.Extract!).ToList();
        pages[1].Fit = new ProfileFit(profile.Id, new Dictionary<string, int> { ["cookie_bar"] = 120 });
        Add(pages[1]);
        Add(new ExtractInput(site, fetched.Pages) { StoredProfiles = [profile] });
        Add(new ExtractResult(pages));
        pages[1].Fit = null;

        var planInput = ProfileStep.PlanInput(site, pages, [profile]);
        Add(planInput);
        Add(new ProfilePlan(site.SiteKey, false, [new PlannedProfile("http://fixture.test/produkt-1.html", 11, 0.09m) { SampleUrls = ["http://fixture.test/", "http://fixture.test/produkt-1.html"] }], "mock"));
        Add(new ProfileCreateResult([profile], 1, 12_000, 3_100, 0.08m, ["Profil šablony stránky http://fixture.test/o-nas.html se nepodařilo vytvořit."]));

        var segmentInput = new SegmentInput(pages, sieve.MaxChunkChars);
        Add(segmentInput);
        var segmented = provider.GetRequiredService<SegmentStep>().Segment(segmentInput);
        segmented.Segments[0].SkippedModules.Add("eco");
        Add(segmented);
        var plan = await provider.GetRequiredService<ProfileStep>().PlanAsync(planInput, ct);
        var estimateInput = new EstimateInput(segmented, sieveModules, "en", plan);
        Add(estimateInput);
        Add(await provider.GetRequiredService<EstimateStep>().EstimateAsync(estimateInput, ruleSets, sieve, ct));

        var sieveInput = new SieveBatchInput(segmented.SieveChunks, sieveModules, "en", 4);
        Add(sieveInput);
        var sieved = await provider.GetRequiredService<SieveStep>().SieveAsync(sieveInput, sieve, null, ct);
        Add(sieved);
        var evaluateInput = new EvaluateBatchInput(SieveStep.States(segmented.Segments, ruleSets, sieve, sieveModules, sieved.Chunks), "en", 4);
        Add(evaluateInput);
        var evaluated = await provider.GetRequiredService<EvaluateStep>().EvaluateAsync(evaluateInput, ruleSets, null, ct);
        Add(evaluated);
        EvaluateStep.Apply(evaluated, segmented.Segments);
        Add(RulesStep.ForScan("sk", segmented.Segments, pages, discovery.Frontier.Counters.UncheckedDocuments, [pages[0].Info], false));
        return records;
    }
}
