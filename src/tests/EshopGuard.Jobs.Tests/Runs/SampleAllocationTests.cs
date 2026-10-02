using EshopGuard.Jobs.Tests.Runs.Support;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>
/// The free sample of a shop with a Slovak, a Czech and a Polish version (design of change 8, task 3.5): at most 100 planned
/// pages, only of the Slovak and the Czech version, the terms of both among them; the Polish version (an unsupported market)
/// is never downloaded beyond the probe of its home page and is stored as unsupported.
/// </summary>
public sealed class SampleAllocationTests(JobsTestDatabase database) : JobsTestBase(database)
{
    [Fact]
    public async Task FreeSample_OfThreeVersions_PlansOnlyTheSupportedOnes_WithTheirMandatoryPages()
    {
        var shop = await RunTests.CreateShopAsync();
        var fetcher = new TrilingualShopFetcher(shop.BaseUrl);
        var storage = Directory.CreateTempSubdirectory("eshopguard-allocation-").FullName;
        var worker = await Workers.StartAsync("w0", RunTests.WorkerSettings(storage), RunTests.Services(fetcher, new DeterministicTestJevClient()));

        var runId = (await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFreeSampleAsync(shop.ShopId, null, Ct))).RunId!.Value;
        var status = await RunTests.WaitForStatusAsync(Db, shop, runId, TimeSpan.FromSeconds(120), [worker], RunTests.Final);

        Assert.True(status is "finished" or "partial", status);
        var planned = (await RunTests.RowsAsync(Db, shop.TenantId,
            "SELECT url, language, queue FROM checks.run_urls WHERE run_id = $1 AND queue LIKE 'sample%'", runId))
            .Select(r => (Url: (string)r[0]!, Language: ((string?)r[1])?[..2], Queue: (string)r[2]!))
            .ToList();
        Assert.InRange(planned.Count, 2, 100);
        Assert.Equal(["cs", "sk"], planned.Select(p => p.Language).Distinct().Order());
        Assert.DoesNotContain(planned, p => p.Url.Contains("/pl/", StringComparison.Ordinal));
        Assert.Contains(planned, p => p is { Language: "sk", Queue: "sample_mandatory" } && p.Url.EndsWith("/obchodne-podmienky", StringComparison.Ordinal));
        Assert.Contains(planned, p => p is { Language: "cs", Queue: "sample_mandatory" } && p.Url.EndsWith("/cz/obchodni-podminky", StringComparison.Ordinal));
        Assert.All(planned.Where(p => p.Queue == "sample_random"), p => Assert.Contains("/p/caj-", p.Url, StringComparison.Ordinal));

        // The Polish version: stored as unsupported, its home page probed once at most, nothing else of it downloaded.
        Assert.Equal("unsupported", await RunTests.ScalarAsync<string>(Db, shop.TenantId,
            "SELECT status FROM shop.shop_languages WHERE shop_id = $1 AND language LIKE 'pl%'", shop.ShopId));
        Assert.DoesNotContain(fetcher.Fetched, u => u.AbsolutePath.StartsWith("/pl/", StringComparison.Ordinal) && u.AbsolutePath != "/pl/");
        Assert.Equal(0L, await RunTests.ScalarAsync<long>(Db, shop.TenantId,
            "SELECT count(*) FROM content.pages WHERE shop_id = $1 AND url LIKE '%/pl/%'", shop.ShopId));
    }
}
