using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace EshopGuard.Worker.Tests;

/// <summary>A worker that could harm a foreign site or leak a key does not start (design of change 8, tasks 12.3 and 12.5).</summary>
public sealed class StartupChecksTests
{
    [Fact]
    public void PrivateNetwork_IsRefused_InEveryEnvironment()
    {
        foreach (var environment in new[] { "Development", "Testing", "Production" })
        {
            Assert.Contains(Check(environment, ("EshopGuard:Crawl:AllowPrivateNetwork", "true")), f => f.StartsWith("config.private_network_not_allowed", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void UserAgentOtherThanEshopGuard_IsRefused_InEveryEnvironment()
    {
        foreach (var environment in new[] { "Development", "Testing", "Production" })
        {
            Assert.Contains(Check(environment, ("EshopGuard:Crawl:UserAgent", "Mozilla/5.0 (+mailto:provoz@eshopguard.sk)")), f => f.StartsWith("config.user_agent_invalid", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void UserAgentWithoutContact_IsRefused_OutsideDevelopment()
    {
        Assert.Contains(Check("Production", ("EshopGuard:Crawl:UserAgent", "EshopGuard/0.1 (+mailto:doplnte-kontakt@example.cz)")), f => f.StartsWith("config.user_agent_contact_missing", StringComparison.Ordinal));
        Assert.Contains(Check("Production"), f => f.StartsWith("config.user_agent_contact_missing", StringComparison.Ordinal));
        Assert.Empty(Check("Development"));
        Assert.Empty(Check("Production", ("EshopGuard:Crawl:UserAgent", "EshopGuard/0.1 (+mailto:provoz@eshopguard.sk)")));
    }

    [Fact]
    public void MockClients_AreRefused_OutsideDevelopmentAndTesting()
    {
        var agent = ("EshopGuard:Crawl:UserAgent", "EshopGuard/0.1 (+mailto:provoz@eshopguard.sk)");
        Assert.Contains(Check("Production", agent, ("EshopGuard:Jev:UseMock", "true")), f => f.StartsWith("config.mock_not_allowed", StringComparison.Ordinal));
        Assert.Contains(Check("Staging", agent, ("EshopGuard:Rewrite:UseMock", "true")), f => f.StartsWith("config.mock_not_allowed", StringComparison.Ordinal));
        Assert.Empty(Check("Testing", ("EshopGuard:Jev:UseMock", "true")));
    }

    [Fact]
    public void KeysInTheConfiguration_AreRefused()
    {
        var failures = Check("Development", ("EshopGuard:Jev:ApiKey", "secret-value"));

        Assert.Contains(failures, f => f.StartsWith("config.key_in_configuration", StringComparison.Ordinal));
        Assert.DoesNotContain(failures, f => f.Contains("secret-value", StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> Check(string environment, params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings.Select(s => KeyValuePair.Create(s.Key, (string?)s.Value))).Build();
        return StartupChecks.Validate(configuration, new TestEnvironment(environment));
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "EshopGuard.Worker";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
