using EshopGuard.Core.Fix;
using EshopGuard.Core.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EshopGuard.Cli;

/// <summary>
/// Content of <c>config/settings.yaml</c>. Secrets are not here; they come from <c>.env</c>.
/// </summary>
internal sealed class SettingsFile
{
    public CrawlOptions Crawl { get; set; } = new();

    public SegmentationOptions Segmentation { get; set; } = new();

    public JevSettings Jev { get; set; } = new();

    public CostOptions Cost { get; set; } = new();

    public BudgetOptions Budget { get; set; } = new();

    public CacheOptions Cache { get; set; } = new();

    public RulesOptions Rules { get; set; } = new();

    /// <summary>Rewrite by an OpenAI model; the key comes from OPENAI_API_KEY, never from this file.</summary>
    public RewriteOptions Rewrite { get; set; } = new();

    /// <summary>Profiles of page templates, written by the rewrite model.</summary>
    public ProfileOptions Profiles { get; set; } = new();
}

/// <summary>
/// Non-secret Jev settings from <c>settings.yaml</c>.
/// </summary>
internal sealed class JevSettings
{
    public int RequestsPerMinute { get; set; } = 1200;

    public int Concurrency { get; set; } = 8;

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxRetries { get; set; } = 6;
}

/// <summary>
/// Configuration read by the CLI: <c>.env</c> for secrets, <c>config/settings.yaml</c> for the rest.
/// The library never reads these files itself.
/// </summary>
internal sealed class CliConfiguration
{
    public const string SettingsPath = "config/settings.yaml";

    public required SettingsFile Settings { get; init; }

    public string? ApiKey { get; init; }

    /// <summary>Name of the variable the key came from; the key itself is never printed.</summary>
    public string? ApiKeySource { get; init; }

    /// <summary>
    /// Checks that a real run has a key. Returns an error message for the user, or null when the run can start.
    /// </summary>
    public string? MissingKeyMessage(bool useMock) =>
        useMock || !string.IsNullOrWhiteSpace(ApiKey)
            ? null
            : "Chybí klíč API Jevu: nastavte JEV_API_KEY v .env nebo proměnnou prostředí TYPESAFE_API_KEY. Pro zkoušku bez API použijte --mock.";

    /// <summary>OpenAI key for <c>eshopguard rewrite</c>; never printed.</summary>
    public string? OpenAiApiKey { get; init; }

    public string? MissingOpenAiKeyMessage(bool useMock) =>
        useMock || !string.IsNullOrWhiteSpace(OpenAiApiKey)
            ? null
            : "Chybí klíč API OpenAI: nastavte OPENAI_API_KEY v .env nebo v proměnné prostředí. Pro zkoušku bez API použijte --mock.";

    public Uri? BaseUrl { get; init; }

    /// <summary>Set only by <c>--allow-private-network</c>, never by settings.yaml (protection against SSRF).</summary>
    public bool AllowPrivateNetwork { get; set; }

    public string? Model { get; init; }

    public List<string> Notes { get; } = [];

    public static CliConfiguration Load()
    {
        var environment = LoadDotEnv();
        var notes = new List<string>();

        SettingsFile settings;
        if (File.Exists(SettingsPath))
        {
            settings = ParseSettings(File.ReadAllText(SettingsPath));
        }
        else
        {
            settings = new SettingsFile();
            notes.Add($"Soubor {SettingsPath} nebyl nalezen, použijí se výchozí hodnoty.");
        }

        var baseUrl = Get(environment, "JEV_BASE_URL");
        var (apiKey, apiKeySource) = Get(environment, "JEV_API_KEY") is { } jevKey
            ? (jevKey, "JEV_API_KEY")
            : Get(environment, "TYPESAFE_API_KEY") is { } typesafeKey
                ? (typesafeKey, "TYPESAFE_API_KEY")
                : ReadUserEnvironment("TYPESAFE_API_KEY") is { } userKey
                    ? (userKey, "TYPESAFE_API_KEY (uživatelské prostředí Windows)")
                    : ((string?)null, (string?)null);
        var configuration = new CliConfiguration
        {
            Settings = settings,
            ApiKey = apiKey,
            ApiKeySource = apiKeySource,
            OpenAiApiKey = Get(environment, "OPENAI_API_KEY") ?? ReadUserEnvironment("OPENAI_API_KEY"),
            BaseUrl = Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed) ? parsed : null,
            Model = Get(environment, "JEV_MODEL"),
        };
        configuration.Notes.AddRange(notes);
        if (baseUrl is not null && configuration.BaseUrl is null)
        {
            configuration.Notes.Add($"JEV_BASE_URL „{baseUrl}“ není platná adresa, použije se výchozí.");
        }

        return configuration;
    }

    /// <summary>Reads and checks the content of settings.yaml; an invalid value fails with the name of its key.</summary>
    /// <exception cref="InvalidOperationException">The YAML or a value is invalid.</exception>
    internal static SettingsFile ParseSettings(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        SettingsFile settings;
        try
        {
            settings = deserializer.Deserialize<SettingsFile?>(yaml) ?? new SettingsFile();
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new InvalidOperationException($"Soubor {SettingsPath} je neplatný (řádek {ex.Start.Line}): {ex.InnerException?.Message ?? ex.Message}", ex);
        }

        if (Commands.SettingsValidation.ValidateSettings(settings) is { } error)
        {
            throw new InvalidOperationException($"Soubor {SettingsPath} je neplatný: {error}");
        }

        return settings;
    }

    public void Apply(EshopGuardOptions options, bool useMock, bool noCache)
    {
        options.Crawl = Settings.Crawl;
        options.Crawl.AllowPrivateNetwork = AllowPrivateNetwork;
        options.Segmentation = Settings.Segmentation;
        options.Cost = Settings.Cost;
        options.Budget = Settings.Budget;
        options.Cache = Settings.Cache;
        options.Rules = Settings.Rules;
        options.Rewrite = Settings.Rewrite;
        options.Profiles = Settings.Profiles;
        options.Cache.Enabled = !noCache;
        options.Rewrite.ApiKey = OpenAiApiKey;
        options.Rewrite.UseMock = useMock;

        options.Jev.ApiKey = ApiKey;
        options.Jev.UseMock = useMock;
        options.Jev.RequestsPerMinute = Settings.Jev.RequestsPerMinute;
        options.Jev.Concurrency = Settings.Jev.Concurrency;
        options.Jev.TimeoutSeconds = Settings.Jev.TimeoutSeconds;
        options.Jev.MaxRetries = Settings.Jev.MaxRetries;
        if (BaseUrl is not null)
        {
            options.Jev.BaseUrl = BaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(Model))
        {
            options.Jev.Model = Model;
        }
    }

    private static Dictionary<string, string> LoadDotEnv()
    {
        if (!File.Exists(".env"))
        {
            return [];
        }

        return DotNetEnv.Env.Load(".env", DotNetEnv.LoadOptions.NoEnvVars())
            .GroupBy(pair => pair.Key)
            .ToDictionary(g => g.Key, g => g.Last().Value);
    }

    private static string? Get(Dictionary<string, string> dotEnv, string name)
    {
        var value = dotEnv.TryGetValue(name, out var fromFile) && !string.IsNullOrWhiteSpace(fromFile)
            ? fromFile
            : Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? ReadUserEnvironment(string name)
    {
        // A key saved in the Windows user environment is not inherited by every shell.
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
