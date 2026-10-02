using System.Text.Json;

namespace EshopGuard.Cli.Tests;

/// <summary><c>eshopguard markets</c> over the fixture shop with a Slovak version in a path (change 7, task 6.3).</summary>
public sealed class MarketsCommandTests
{
    [Fact]
    public async Task Mock_WritesMarketsJsonWithVersionsPlanAndSummary_AndNeverTheKey()
    {
        var folder = CliProcess.NewWorkingFolder();
        var (url, requests, stop) = CliProcess.StartFixture(Path.Combine("versions", "path-shop"));
        const string key = "sk-test-not-a-real-key-7f3a";
        try
        {
            var (exitCode, output) = await CliProcess.RunAsync(folder, new Dictionary<string, string?> { ["OPENAI_API_KEY"] = key },
                "markets", url, "--mock", "--allow-private-network", "--out", "out");

            Assert.True(exitCode == 0, output);
            var run = Assert.Single(Directory.GetDirectories(Path.Combine(folder, "out")));
            var json = await File.ReadAllTextAsync(Path.Combine(run, "markets.json"), TestContext.Current.CancellationToken);
            var log = await File.ReadAllTextAsync(Path.Combine(run, "markets.log"), TestContext.Current.CancellationToken);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
            Assert.Contains(root.GetProperty("versions").EnumerateArray(), v => v.GetProperty("language").GetString() == "sk"
                && v.GetProperty("base_url").GetString()!.EndsWith("/sk/", StringComparison.Ordinal));
            Assert.True(root.GetProperty("plan").GetProperty("checked").GetArrayLength() >= 1);
            Assert.False(string.IsNullOrEmpty(root.GetProperty("summary").GetProperty("code").GetString()));
            Assert.True(root.TryGetProperty("markets", out _));
            Assert.Contains("model_mock", root.GetProperty("codes").EnumerateArray().Select(c => c.GetString()));
            Assert.DoesNotContain(key, json, StringComparison.Ordinal);
            Assert.DoesNotContain(key, log, StringComparison.Ordinal);
            Assert.DoesNotContain(key, output, StringComparison.Ordinal);
            Assert.Contains(requests, r => r.Contains("/sk/", StringComparison.Ordinal));
        }
        finally
        {
            await stop.CancelAsync();
            Directory.Delete(folder, recursive: true);
        }
    }
}
