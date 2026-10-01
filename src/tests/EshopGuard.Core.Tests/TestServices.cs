using EshopGuard.Core.Crawl;
using EshopGuard.Core.Options;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Builds the library container for tests: no network, no request delay, mock Jev, shipped rule sets.
/// </summary>
internal static class TestServices
{
    public static string RulesDirectory => Path.Combine(AppContext.BaseDirectory, "rules");

    public static string LabelsFile => Path.Combine(AppContext.BaseDirectory, "config", "labels.yaml");

    public static string LegalRequirementsFile => Path.Combine(AppContext.BaseDirectory, "config", "legal_requirements.yaml");

    public static string SieveFile => Path.Combine(AppContext.BaseDirectory, "config", "sieve.yaml");

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

        // Registered before the library, so they replace its defaults.
        register?.Invoke(services);

        services.AddEshopGuard(options =>
        {
            options.Crawl.RequestsPerSecond = 0;
            options.Jev.UseMock = true;
            options.Rules.Directory = RulesDirectory;
            options.Rules.LabelsFile = LabelsFile;
            options.Rules.LegalRequirementsFile = LegalRequirementsFile;
            options.Rules.SieveFile = SieveFile;
            options.Rewrite.PromptFile = RewritePromptFile;
            options.Rewrite.UseMock = true;
            configure?.Invoke(options);
        });
        services.AddSingleton(fetcher);
        return services.BuildServiceProvider();
    }
}
