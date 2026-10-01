using EshopGuard.Core.Crawl;
using EshopGuard.Core.Options;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Builds the library container for tests: no network, no request delay, mock Jev, shipped rule sets.
/// </summary>
internal static class TestServices
{
    /// <summary>Date of every test run: the day change 6 was written.</summary>
    public static DateOnly Today { get; } = new(2026, 10, 1);

    public static string RulesDirectory => Path.Combine(AppContext.BaseDirectory, "rules");

    public static string LabelsFile => Path.Combine(AppContext.BaseDirectory, "config", "labels.yaml");

    public static string LegalRequirementsFile => Path.Combine(AppContext.BaseDirectory, "config", "legal_requirements.yaml");

    public static string SieveFile => Path.Combine(AppContext.BaseDirectory, "config", "sieve.yaml");

    public static string JurisdictionsFile => Path.Combine(AppContext.BaseDirectory, "config", "jurisdictions.yaml");

    public static string RewritePromptFile => Path.Combine(AppContext.BaseDirectory, "config", "rewrite.yaml");

    public static ServiceProvider Create(IPageFetcher fetcher, Action<EshopGuardOptions>? configure = null, Jev.IJevClient? client = null,
        Action<IServiceCollection>? register = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (client is not null)
        {
            services.AddSingleton(client);
        }

        // Registered before the library, so they replace its defaults. The date is fixed, so effective dates of rules
        // (e.g. the Czech withdrawal button from 1. 1. 2027) do not change the results of the tests over time.
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Today));
        register?.Invoke(services);

        services.AddEshopGuard(options =>
        {
            options.Crawl.RequestsPerSecond = 0;
            options.Jev.UseMock = true;
            options.Rules.Directory = RulesDirectory;
            options.Rules.LabelsFile = LabelsFile;
            options.Rules.LegalRequirementsFile = LegalRequirementsFile;
            options.Rules.SieveFile = SieveFile;
            options.Rules.JurisdictionsFile = JurisdictionsFile;
            options.Rewrite.PromptFile = RewritePromptFile;
            options.Rewrite.UseMock = true;
            configure?.Invoke(options);
        });
        services.AddSingleton(fetcher);
        return services.BuildServiceProvider();
    }
}

/// <summary>A clock that always shows noon of one day.</summary>
internal sealed class FixedTimeProvider(DateOnly day) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
}
