using EshopGuard.Core.Options;
using Microsoft.Extensions.Options;

namespace EshopGuard.Worker;

/// <summary>
/// Options of the library in the worker: section <c>EshopGuard</c> of the configuration (the same keys as
/// <c>config/settings.yaml</c> of the CLI, PascalCase), relative paths of rules and prompts under
/// <c>EshopGuard:BaseDirectory</c> (relative to the content root of the host, default the content root), and the keys of Jev and OpenAI from environment
/// variables only (<c>TYPESAFE_API_KEY</c> or <c>JEV_API_KEY</c>, <c>OPENAI_API_KEY</c>); a key in a configuration file is
/// refused at start (<see cref="StartupChecks"/>).
/// </summary>
public static class AnalysisSettings
{
    public const string SectionName = "EshopGuard";

    /// <param name="contentRoot">Folder a relative <c>EshopGuard:BaseDirectory</c> is taken from (the content root of the host).</param>
    public static void Apply(EshopGuardOptions options, IConfiguration configuration, string contentRoot)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.GetSection(SectionName).Bind(options);
        options.Jev.ApiKey = Environment("JEV_API_KEY") ?? Environment("TYPESAFE_API_KEY");
        options.Rewrite.ApiKey = Environment("OPENAI_API_KEY");

        var root = configuration[$"{SectionName}:BaseDirectory"] is { Length: > 0 } baseDirectory
            ? Path.GetFullPath(Path.Combine(contentRoot, baseDirectory))
            : contentRoot;
        var rules = options.Rules;
        rules.Directory = Rooted(root, rules.Directory);
        rules.LabelsFile = Rooted(root, rules.LabelsFile);
        rules.LegalRequirementsFile = Rooted(root, rules.LegalRequirementsFile);
        rules.SieveFile = Rooted(root, rules.SieveFile);
        rules.JurisdictionsFile = Rooted(root, rules.JurisdictionsFile);
        rules.TextsDirectory = rules.TextsDirectory is null ? null : Rooted(root, rules.TextsDirectory);
        options.Rewrite.PromptFile = Rooted(root, options.Rewrite.PromptFile);
    }

    private static string Rooted(string root, string path) => Path.IsPathRooted(path) ? path : Path.Combine(root, path);

    private static string? Environment(string name) => System.Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value.Trim() : null;
}

/// <summary>
/// Refuses to start a worker whose configuration could harm a foreign site or leak a key (design of change 8, task 12.3):
/// the crawl allowed into the internal network or a User-Agent other than <c>EshopGuard/0.1</c> (in any environment); outside
/// Development and Testing a User-Agent without a real contact and the mock clients of Jev or OpenAI; keys written into the
/// configuration instead of the environment.
/// </summary>
public sealed class StartupChecks(IConfiguration configuration, IHostEnvironment environment) : IHostedService
{
    /// <summary>The failures of the configuration (codes and keys, never values).</summary>
    public static IReadOnlyList<string> Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        var options = new EshopGuardOptions();
        configuration.GetSection(AnalysisSettings.SectionName).Bind(options);
        var failures = new List<string>();
        if (options.Crawl.AllowPrivateNetwork)
        {
            failures.Add("config.private_network_not_allowed: EshopGuard:Crawl:AllowPrivateNetwork");
        }

        if (!string.IsNullOrEmpty(configuration[$"{AnalysisSettings.SectionName}:Jev:ApiKey"]) || !string.IsNullOrEmpty(configuration[$"{AnalysisSettings.SectionName}:Rewrite:ApiKey"]))
        {
            failures.Add("config.key_in_configuration: keys come only from TYPESAFE_API_KEY/JEV_API_KEY and OPENAI_API_KEY");
        }

        if (!options.Crawl.UserAgent.StartsWith("EshopGuard/0.1", StringComparison.Ordinal))
        {
            failures.Add("config.user_agent_invalid: EshopGuard:Crawl:UserAgent (must start with EshopGuard/0.1)");
        }

        var production = !environment.IsDevelopment() && !environment.IsEnvironment("Testing");
        if (production)
        {
            var agent = options.Crawl.UserAgent;
            if (agent.Contains("doplnte-kontakt", StringComparison.OrdinalIgnoreCase) || !agent.Contains("(+", StringComparison.Ordinal))
            {
                failures.Add("config.user_agent_contact_missing: EshopGuard:Crawl:UserAgent");
            }

            if (options.Jev.UseMock || options.Rewrite.UseMock)
            {
                failures.Add("config.mock_not_allowed: EshopGuard:Jev:UseMock, EshopGuard:Rewrite:UseMock");
            }
        }

        return failures;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var failures = Validate(configuration, environment);
        return failures.Count == 0
            ? Task.CompletedTask
            : throw new OptionsValidationException(nameof(StartupChecks), typeof(StartupChecks), failures);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
