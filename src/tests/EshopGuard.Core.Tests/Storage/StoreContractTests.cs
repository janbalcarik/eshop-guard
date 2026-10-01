using EshopGuard.Core.Jev;
using EshopGuard.Core.Pipeline;
using EshopGuard.Core.Profiles;
using EshopGuard.Core.Storage;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Behaviour every <see cref="IJevCache"/> must have, the PostgreSQL one of change 5b too: idempotent writes (the first
/// answer stays), batch reads that leave out missing keys, and the answer stored under the key of its kind.
/// </summary>
public abstract class JevCacheContractTests
{
    protected abstract IJevCache CreateCache();

    private static JevResult Answer(double probability) => new()
    {
        Model = "jev-1.13.0",
        Answers = new Dictionary<string, JevAnswer> { ["eco_claim"] = new() { Type = "noul", Noul = probability } },
        Usage = new JevUsage { InputTokens = 150, OutputTokens = 5 },
    };

    /// <summary>A key of the detailed questions of eco, or of the sieve question of eco (its own question set and version).</summary>
    private static JevCacheKey Key(string state, JevCacheKind kind = JevCacheKind.Detail) => kind == JevCacheKind.Detail
        ? JevCacheKeys.Create(kind, "jev-1.13.0", "eco-1", "en", new Dictionary<string, JevQuestion> { ["eco_claim"] = new() { Type = "noul", Instructions = "Q?" } }, state)
        : JevCacheKeys.Create(kind, "jev-1.13.0", "sieve-1", "en", new Dictionary<string, JevQuestion> { ["sieve_eco"] = new() { Type = "noul", Instructions = "Topic?" } }, state);

    [Fact]
    public async Task SetTwice_KeepsTheFirstAnswer()
    {
        var cache = CreateCache();
        var key = Key("Věta.");

        await cache.SetAsync(key, Answer(0.2), TestContext.Current.CancellationToken);
        await cache.SetAsync(key, Answer(0.9), TestContext.Current.CancellationToken);

        var stored = await cache.GetAsync(key, TestContext.Current.CancellationToken);
        Assert.Equal(0.2, stored!.Answers["eco_claim"].Noul);
        Assert.Equal("jev-1.13.0", stored.Model);
        Assert.Equal(150, stored.Usage.InputTokens);
    }

    [Fact]
    public async Task GetMany_ReturnsOnlyStoredKeys()
    {
        var cache = CreateCache();
        var keys = Enumerable.Range(0, 700).Select(i => Key($"Věta {i}.")).ToList();
        foreach (var key in keys.Where((_, i) => i % 2 == 0))
        {
            await cache.SetAsync(key, Answer(0.5), TestContext.Current.CancellationToken);
        }

        var found = await cache.GetManyAsync(keys, TestContext.Current.CancellationToken);

        Assert.Equal(350, found.Count);
        Assert.All(found.Keys, k => Assert.Equal(0, keys.IndexOf(k) % 2));
        Assert.Null(await cache.GetAsync(Key("Jiná věta."), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SieveAndDetailOfTheSameText_AreDifferentKeys()
    {
        var cache = CreateCache();
        await cache.SetAsync(Key("Text.", JevCacheKind.Sieve), Answer(0.3), TestContext.Current.CancellationToken);

        Assert.Null(await cache.GetAsync(Key("Text."), TestContext.Current.CancellationToken));
        Assert.NotNull(await cache.GetAsync(Key("Text.", JevCacheKind.Sieve), TestContext.Current.CancellationToken));
    }
}

/// <summary>Behaviour of every <see cref="IPageStore"/>: a version only when the text changed, validators, fingerprints.</summary>
public abstract class PageStoreContractTests
{
    protected abstract IPageStore CreateStore();

    private static PageVersionRecord Version(string url, string hash, params long[] fingerprints) =>
        new("shop.sk", url, hash, fingerprints, 1000, 900, "readability", null, false, new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Version_IsAddedOnlyWhenTheTextChanged()
    {
        var store = CreateStore();
        var ct = TestContext.Current.CancellationToken;

        Assert.True(await store.AddVersionAsync(Version("https://shop.sk/a", "h1", 1, 2), ct));
        Assert.False(await store.AddVersionAsync(Version("https://shop.sk/a", "h1", 1, 2), ct));
        Assert.True(await store.AddVersionAsync(Version("https://shop.sk/a", "h2", 2, 3), ct));
        Assert.True(await store.AddVersionAsync(Version("https://shop.sk/b", "h1", 3), ct));

        var current = await store.GetCurrentVersionsAsync("shop.sk", ct);
        Assert.Equal(["h2", "h1"], current.Select(v => v.TextHash));
        Assert.Equal(["https://shop.sk/a", "https://shop.sk/b"], await store.FindByFingerprintAsync("shop.sk", 3, ct));
        Assert.Empty(await store.FindByFingerprintAsync("shop.sk", 1, ct));
    }

    [Fact]
    public async Task Validators_OfDownloadedPagesOnly_UpsertReplaces()
    {
        var store = CreateStore();
        var ct = TestContext.Current.CancellationToken;
        var at = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        await store.UpsertPageAsync(new PageRecord("shop.sk", "https://shop.sk/a", "https://shop.sk/a", "product", "\"1\"", at, "h1", at), ct);
        await store.UpsertPageAsync(new PageRecord("shop.sk", "https://shop.sk/a", "https://shop.sk/a", "product", "\"2\"", at, "h2", at), ct);

        var validators = await store.GetValidatorsAsync("shop.sk", ["https://shop.sk/a", "https://shop.sk/zadna"], ct);

        Assert.Equal(new PageValidators("\"2\"", at), Assert.Single(validators).Value);
        Assert.Empty(await store.GetValidatorsAsync("jiny.sk", ["https://shop.sk/a"], ct));
    }
}

/// <summary>Behaviour of every <see cref="IPageContentStore"/>: HTML and extraction stored, replaced and read back.</summary>
public abstract class PageContentStoreContractTests
{
    protected abstract IPageContentStore CreateStore();

    [Fact]
    public async Task HtmlAndExtract_AreStoredAndReplaced()
    {
        var store = CreateStore();
        var ct = TestContext.Current.CancellationToken;
        var key = new PageContentKey("shop.sk", "https://shop.sk/a");

        Assert.Null(await store.GetHtmlAsync(key, ct));
        await store.PutHtmlAsync(key, PageContent.Compress("<p>1</p>"), ct);
        await store.PutHtmlAsync(key, PageContent.Compress("<p>2</p>"), ct);
        await store.PutExtractAsync(key, "{\"a\":1}", ct);

        Assert.Equal("<p>2</p>", PageContent.Decompress((await store.GetHtmlAsync(key, ct))!));
        Assert.Equal("{\"a\":1}", await store.GetExtractAsync(key, ct));
        Assert.Null(await store.GetExtractAsync(key with { Site = "jiny.sk" }, ct));
    }
}

/// <summary>Behaviour of every <see cref="IUrlFrontierStore"/>: the state of the frontier is saved and restored whole.</summary>
public abstract class UrlFrontierStoreContractTests
{
    protected abstract IUrlFrontierStore CreateStore();

    [Fact]
    public async Task FrontierState_IsSavedAndRestored()
    {
        var store = CreateStore();
        var ct = TestContext.Current.CancellationToken;
        var state = new UrlFrontierState { Home = new Uri("https://shop.sk/"), MaxPages = 200, MaxProducts = 100, LinkMode = true };
        state.LegalQueue.Enqueue(new FrontierItem(new Uri("https://shop.sk/obchodne-podmienky"), 1, false));
        state.ProductQueue.Enqueue(new FrontierItem(new Uri("https://shop.sk/p1"), 0, true));
        state.Visited.Add("https://shop.sk/");
        state.Counters.Requests = 7;

        Assert.Null(await store.LoadAsync("run-1", ct));
        await store.SaveAsync("run-1", PipelineJson.Serialize(state), ct);
        var restored = PipelineJson.Deserialize<UrlFrontierState>((await store.LoadAsync("run-1", ct))!);

        Assert.Equal(PipelineJson.Serialize(state), PipelineJson.Serialize(restored));
        Assert.Equal("https://shop.sk/obchodne-podmienky", restored.LegalQueue.Peek().Url.AbsoluteUri);
        Assert.Equal(7, restored.Counters.Requests);
    }
}

/// <summary>Behaviour of every <see cref="IRewriteCache"/>: an answer with its model is stored, replaced and read back.</summary>
public abstract class RewriteCacheContractTests
{
    protected abstract IRewriteCache CreateCache();

    [Fact]
    public async Task Answer_IsStoredReplacedAndReadBack()
    {
        var cache = CreateCache();
        var ct = TestContext.Current.CancellationToken;
        var key = "rw:" + Guid.NewGuid().ToString("N");

        Assert.Null(await cache.GetAsync(key, ct));
        await cache.SetAsync(key, """{"changes": [], "kept": [{"finding_id": "F1", "reason_cs": "složení"}]}""", "gpt-6.1-sol", ct);
        var first = await cache.GetAsync(key, ct);
        await cache.SetAsync(key, """{"changes": [], "kept": []}""", null, ct);
        var second = await cache.GetAsync(key, ct);

        Assert.Equal("gpt-6.1-sol", first!.Value.Model);
        using (var json = System.Text.Json.JsonDocument.Parse(first.Value.Json))
        {
            Assert.Equal("složení", json.RootElement.GetProperty("kept")[0].GetProperty("reason_cs").GetString());
        }

        Assert.Null(second!.Value.Model);
        Assert.Equal(0, System.Text.Json.JsonDocument.Parse(second.Value.Json).RootElement.GetProperty("kept").GetArrayLength());
    }
}

/// <summary>Behaviour of every <see cref="IPageProfileStore"/>: profiles of a site are kept whole, oldest first, per site.</summary>
public abstract class PageProfileStoreContractTests
{
    protected abstract IPageProfileStore CreateStore();

    private static PageProfile Profile(string site, int number, DateTimeOffset createdAt) => new()
    {
        Id = $"{site}#{number}",
        Site = site,
        CreatedAt = createdAt,
        Model = "gpt-6.1-sol",
        PromptVersion = "profile-1",
        SampleUrls = [$"https://{site}/", $"https://{site}/produkt-1"],
        Regions =
        [
            new ProfileRegion { Role = "cookie_bar", Action = ProfileRegion.Skip, Selector = "#cookies", Example = "Súhlasím", Reason = "lišta" },
            new ProfileRegion { Role = "main_description", Action = ProfileRegion.Check, Selector = "main", RejectedBecause = null },
        ],
    };

    [Fact]
    public async Task Profiles_AreKeptWholeOldestFirstPerSite()
    {
        var store = CreateStore();
        var ct = TestContext.Current.CancellationToken;
        var site = $"shop-{Guid.NewGuid():N}.sk";
        var at = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

        Assert.Empty(await store.GetAsync(site, ct));
        await store.AddAsync(Profile(site, 2, at.AddMinutes(5)), ct);
        await store.AddAsync(Profile(site, 1, at), ct);
        await store.AddAsync(Profile(site, 1, at), ct);
        await store.AddAsync(Profile("jiny-" + site, 1, at), ct);

        var profiles = await store.GetAsync(site, ct);
        Assert.Equal([$"{site}#1", $"{site}#2"], profiles.Select(p => p.Id));
        var first = profiles[0];
        Assert.Equal(site, first.Site);
        Assert.Equal(at, first.CreatedAt);
        Assert.Equal("gpt-6.1-sol", first.Model);
        Assert.Equal("profile-1", first.PromptVersion);
        Assert.Equal([$"https://{site}/", $"https://{site}/produkt-1"], first.SampleUrls);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(Profile(site, 1, at).Regions), System.Text.Json.JsonSerializer.Serialize(first.Regions));
    }
}
