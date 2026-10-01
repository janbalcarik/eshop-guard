using EshopGuard.Core.Options;

namespace EshopGuard.Cli.Tests;

/// <summary>Values of settings.yaml are checked when it is read (task 8.1); the internal network is never allowed there.</summary>
public sealed class SettingsFileTests
{
    [Theory]
    [InlineData("fetch_batch_max_pages", "0")]
    [InlineData("fetch_batch_max_seconds", "-5")]
    [InlineData("extract_timeout_seconds", "0")]
    public void NonPositiveBatchOrTimeout_FailsWithTheKey(string key, string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => CliConfiguration.ParseSettings($"crawl:\n  {key}: {value}\n"));

        Assert.Contains($"crawl.{key}", error.Message, StringComparison.Ordinal);
        Assert.Contains(CliConfiguration.SettingsPath, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AllowPrivateNetwork_CannotBeSetInTheFile()
    {
        var error = Assert.Throws<InvalidOperationException>(() => CliConfiguration.ParseSettings("crawl:\n  allow_private_network: true\n"));

        Assert.Contains("--allow-private-network", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShippedSettings_ReadTheBatchKeys_AndTheFlagSetsThePrivateNetwork()
    {
        var settings = CliConfiguration.ParseSettings(File.ReadAllText(Path.Combine(SourceRoot(), "config", "settings.yaml")));

        Assert.Equal(30, settings.Crawl.ExtractTimeoutSeconds);
        Assert.Equal(100, settings.Crawl.FetchBatchMaxPages);
        Assert.Equal(60, settings.Crawl.FetchBatchMaxSeconds);

        var configuration = new CliConfiguration { Settings = settings, AllowPrivateNetwork = true };
        var options = new EshopGuardOptions();
        configuration.Apply(options, useMock: true, noCache: true);
        Assert.True(options.Crawl.AllowPrivateNetwork);
        configuration.AllowPrivateNetwork = false;
        configuration.Apply(options, useMock: true, noCache: true);
        Assert.False(options.Crawl.AllowPrivateNetwork);
    }

    private static string SourceRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "EshopGuard.sln")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("EshopGuard.sln not found above the test output.");
    }
}
