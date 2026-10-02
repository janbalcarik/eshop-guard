using EshopGuard.Core.Profiles;
using EshopGuard.Jobs.Tests.Runs.Support;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>
/// The comparison of language versions in the free sample on the product description of the profile (2. 10. 2026): the
/// analysis writes the profile of the product template (priced in its estimate, usage <c>profile</c>), the reviews shared by
/// both versions do not count, and the check of the sample uses the same profile without asking the model again.
/// </summary>
public sealed class VersionProfilesTests(JobsTestDatabase database) : JobsTestBase(database)
{
    [Fact]
    public async Task FreeSample_ComparesTheVersionsOnTheDescription_AndReusesTheProfileOfTheProducts()
    {
        var id = Guid.NewGuid().ToString("N")[..10];
        var shop = await RunTests.CreateShopAsync($"pair-{id}.sk");
        var profileModel = new ProductTemplateModel();
        var storage = Directory.CreateTempSubdirectory("eshopguard-version-profiles-").FullName;
        var services = RunTests.Services(new BilingualShopFetcher(shop.Domain, $"pair-{id}.cz"), new DeterministicTestJevClient());
        var worker = await Workers.StartAsync("w0", RunTests.WorkerSettings(storage), s =>
        {
            s.AddSingleton<IProfileModel>(profileModel);
            services(s);
        });

        var runId = (await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFreeSampleAsync(shop.ShopId, null, Ct))).RunId!.Value;
        var status = await RunTests.WaitForStatusAsync(Db, shop, runId, TimeSpan.FromSeconds(120), [worker], RunTests.Final);

        Assert.True(status is "finished" or "partial", status);
        var cz = (await RunTests.RowsAsync(Db, shop.TenantId,
            "SELECT comparison ->> 'basis', own_text_share, (comparison ->> 'own_product_share')::float8 FROM shop.shop_languages WHERE shop_id = $1 AND language = 'cs-cz'",
            shop.ShopId)).Single();
        Assert.Equal("description", cz[0]);
        Assert.True((float)cz[1]! > 0.9f, $"own {cz[1]}");
        Assert.Equal(1.0, cz[2]);
        Assert.True(await RunTests.ScalarAsync<long>(Db, shop.TenantId,
            "SELECT coalesce(sum(calls), 0)::bigint FROM usage.usage_records WHERE run_id = $1 AND operation = 'profile'", runId) >= 1);
        var estimate = await RunTests.RunJsonAsync(Db, shop, runId, "estimate");
        Assert.True((decimal)estimate["internal"]!["openai"]!["market_usd"]! >= ProductTemplateModel.Price, "the estimate of the analysis includes the profile");

        // The product template got its profile once, in the analysis; the check of the sample fits its products to it.
        Assert.Equal(1, profileModel.Requests.Count(r => r.All(s => s.Url.Contains("/p/caj-", StringComparison.Ordinal))));
        Assert.True(await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM shop.page_profiles WHERE shop_id = $1", shop.ShopId) >= 1);
    }

    [Fact]
    public async Task ProfilesOverTheCapOfTheSample_AreLeftOut_AndTheVersionsAreComparedOnTheMainText()
    {
        var id = Guid.NewGuid().ToString("N")[..10];
        var shop = await RunTests.CreateShopAsync($"pair-{id}.sk");
        var profileModel = new ProductTemplateModel(price: 5m);
        var storage = Directory.CreateTempSubdirectory("eshopguard-version-profiles-").FullName;
        var services = RunTests.Services(new BilingualShopFetcher(shop.Domain, $"pair-{id}.cz"), new DeterministicTestJevClient());
        var worker = await Workers.StartAsync("w0", RunTests.WorkerSettings(storage), s =>
        {
            s.AddSingleton<IProfileModel>(profileModel);
            services(s);
        });

        var runId = (await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFreeSampleAsync(shop.ShopId, null, Ct))).RunId!.Value;
        await RunTests.WaitForStatusAsync(Db, shop, runId, TimeSpan.FromSeconds(120), [worker], RunTests.Final);

        // The analysis went on without the profile: the versions are compared and stored, and the estimate of the analysis has
        // no profile in it (the fake market model asks for no confirmation, so it stays 0).
        Assert.Equal("main_text", await RunTests.ScalarAsync<string>(Db, shop.TenantId,
            "SELECT comparison ->> 'basis' FROM shop.shop_languages WHERE shop_id = $1 AND language = 'cs-cz'", shop.ShopId));
        var estimate = await RunTests.RunJsonAsync(Db, shop, runId, "estimate");
        Assert.True((decimal)estimate["internal"]!["openai"]!["market_usd"]! < 1m);
        Assert.DoesNotContain(profileModel.Requests, r => r.All(s => s.Url.Contains("/p/caj-", StringComparison.Ordinal)));
        Assert.Equal(0L, await RunTests.ScalarAsync<long>(Db, shop.TenantId,
            "SELECT coalesce(sum(calls), 0)::bigint FROM usage.usage_records WHERE run_id = $1 AND operation = 'profile'", runId));
    }

    /// <summary>The profile model: the product template has a description (<c>.popis</c>) and customer reviews (<c>.recenzie</c>).</summary>
    private sealed class ProductTemplateModel(decimal price = ProductTemplateModel.Price) : IProfileModel
    {
        public const decimal Price = 0.05m;

        private static readonly List<ProfileRegion> Regions =
        [
            new() { Role = "header", Action = ProfileRegion.Check, Selector = "header" },
            new() { Role = "footer", Action = ProfileRegion.Check, Selector = "footer" },
            new() { Role = "product_title", Action = ProfileRegion.Check, Selector = "h1" },
            new() { Role = "main_description", Action = ProfileRegion.Check, Selector = ".popis" },
            new() { Role = "price", Action = ProfileRegion.Check, Selector = ".cena" },
            new() { Role = "reviews", Action = ProfileRegion.Check, Selector = ".recenzie" },
        ];

        public System.Collections.Concurrent.ConcurrentQueue<IReadOnlyList<(string Url, string Outline)>> Requests { get; } = new();

        public bool IsAvailable => true;

        public string? UnavailableReason => null;

        public decimal EstimateUsd(IReadOnlyList<string> outlines) => price;

        public Task<ProfileAnswer> AskAsync(IReadOnlyList<(string Url, string Outline)> samples, CancellationToken ct)
        {
            Requests.Enqueue(samples);
            return Task.FromResult(new ProfileAnswer { Regions = Regions, Model = "fake-profile", CostUsd = price });
        }
    }
}
