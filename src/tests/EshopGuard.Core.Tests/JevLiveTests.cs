using EshopGuard.Core.Options;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Scan of the fixture e-shop against the real Jev API. Runs only with a key (JEV_API_KEY or TYPESAFE_API_KEY) and
/// costs about a tenth of a cent; without a key it is skipped. Jev is not deterministic, so only bands are compared.
/// Deviations are fixed by rewording questions in YAML, not in code.
/// </summary>
public class JevLiveTests
{
    private static string? ApiKey =>
        Environment.GetEnvironmentVariable("JEV_API_KEY")
        ?? Environment.GetEnvironmentVariable("TYPESAFE_API_KEY")
        ?? (OperatingSystem.IsWindows() ? Environment.GetEnvironmentVariable("TYPESAFE_API_KEY", EnvironmentVariableTarget.User) : null);

    [Fact]
    [Trait("Category", "Jev")]
    public Task FixtureScan_FindsPlantedFindingsAndStaysQuietOnCleanPages() =>
        ScanAndCompareAsync(FileSystemPageFetcher.ForFixture(), "cz", "expected_findings.json");

    [Fact]
    [Trait("Category", "Jev")]
    public Task SlovakFixtureScan_FindsPlantedFindingsAndStaysQuietOnCleanPages() =>
        ScanAndCompareAsync(FileSystemPageFetcher.ForSlovakFixture(), "sk", "expected_findings_sk.json");

    private static async Task ScanAndCompareAsync(FileSystemPageFetcher fetcher, string country, string expectedFile)
    {
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ApiKey), "Bez klíče API Jevu (JEV_API_KEY nebo TYPESAFE_API_KEY) se test přeskočí.");

        await using var provider = TestServices.Create(fetcher, options =>
        {
            options.Jev.UseMock = false;
            options.Jev.ApiKey = ApiKey;
            options.Cache.Enabled = false;
        });
        var result = await provider.GetRequiredService<IEshopGuard>()
            .ScanSiteAsync(FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions { Country = country }, ct: TestContext.Current.CancellationToken);
        var expected = ExpectedFindings.Load(expectedFile);

        var deviations = new List<string>();
        foreach (var planted in expected.Findings)
        {
            var found = result.Findings.FirstOrDefault(f => f.RuleId == planted.RuleId && f.Text == planted.Text);
            if (found is null)
            {
                deviations.Add($"chybí {planted.RuleId} na {planted.Path}: „{planted.Text}“");
            }
        }

        var siteFindings = result.Findings.Where(f => f.Scope == "site").Select(f => f.RuleId).ToHashSet();
        deviations.AddRange(expected.SiteFindings.Except(siteFindings).Select(id => $"chybí nález za celý web {id}"));
        deviations.AddRange(siteFindings.Except(expected.SiteFindings).Select(id => $"navíc nález za celý web {id}"));
        foreach (var clean in expected.PagesWithoutFindings)
        {
            deviations.AddRange(result.Findings
                .Where(f => f.Scope == "segment" && f.Urls.Any(u => new Uri(u).AbsolutePath == clean))
                .Select(f => $"navíc {f.RuleId} na {clean}: „{f.Text}“ (skóre {f.Score:0.00})"));
        }

        Assert.Equal(0, result.Stats.JevErrors);
        Assert.True(deviations.Count == 0, "Odchylky od nastražených nálezů:" + Environment.NewLine + string.Join(Environment.NewLine, deviations));
    }
}
