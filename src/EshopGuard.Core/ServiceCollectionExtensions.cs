using System.Net;
using EshopGuard.Core.Cache;
using EshopGuard.Core.Classify;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Extract;
using EshopGuard.Core.Fix;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Options;
using EshopGuard.Core.Pipeline;
using EshopGuard.Core.Profiles;
using EshopGuard.Core.Report;
using EshopGuard.Core.Rules;
using EshopGuard.Core.Rules.Texts;
using EshopGuard.Core.Segmentation;
using EshopGuard.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core;

/// <summary>
/// Registration of the library into a DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Records every answer of the sites into <paramref name="directory"/> (<see cref="RecordingPageFetcher"/> around the HTTP
    /// fetcher). Call before <see cref="AddEshopGuard"/>.
    /// </summary>
    public static IServiceCollection AddEshopGuardRecording(this IServiceCollection services, string directory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        return services.AddSingleton<IPageFetcher>(provider =>
            new RecordingPageFetcher(ActivatorUtilities.CreateInstance<HttpPageFetcher>(provider), directory));
    }

    /// <summary>
    /// Answers every request from the recording in <paramref name="directory"/> without network
    /// (<see cref="ReplayPageFetcher"/>). Call before <see cref="AddEshopGuard"/>.
    /// </summary>
    public static IServiceCollection AddEshopGuardReplay(this IServiceCollection services, string directory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        return services.AddSingleton<IPageFetcher>(provider =>
            new ReplayPageFetcher(directory, provider.GetService<Microsoft.Extensions.Logging.ILogger<ReplayPageFetcher>>()));
    }

    /// <summary>
    /// Only the rules and their data (<see cref="IRuleSetProvider"/>, the texts): for a host that reads the catalog of markets
    /// and modules without running analyses (the API, change 10). The host configures <see cref="EshopGuardOptions"/> itself.
    /// </summary>
    public static IServiceCollection AddEshopGuardRules(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<EshopGuardOptions>();
        services.TryAddSingleton<IRuleTextProvider, YamlRuleTextProvider>();
        services.TryAddSingleton<IRuleSetProvider, YamlRuleSetProvider>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="IEshopGuard"/> and its default implementations as singletons.
    /// Services registered by the host before this call replace the defaults.
    /// </summary>
    public static IServiceCollection AddEshopGuard(this IServiceCollection services, Action<EshopGuardOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<EshopGuardOptions>().Configure(configure);

        services.AddHttpClient(HttpPageFetcher.HttpClientName, (provider, client) =>
            {
                var crawl = provider.GetRequiredService<IOptions<EshopGuardOptions>>().Value.Crawl;
                client.Timeout = TimeSpan.FromSeconds(crawl.TimeoutSeconds);
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", crawl.UserAgent);
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml;q=0.9,*/*;q=0.5");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "cs,sk;q=0.9,en;q=0.5");
            })
            .ConfigurePrimaryHttpMessageHandler(provider =>
            {
                // Protection against SSRF: every connection goes only to addresses checked after DNS, and never through a
                // proxy (the proxy would connect instead of us, past the check).
                var resolver = provider.GetRequiredService<IHostAddressResolver>();
                var allowPrivate = provider.GetRequiredService<IOptions<EshopGuardOptions>>().Value.Crawl.AllowPrivateNetwork;
                return new SocketsHttpHandler
                {
                    // No shared cookie container: cookies are kept per crawl scope (site or language version) and sent by
                    // the request (change 7, K rozhodnutí 7), so they never pass between sites or tenants.
                    UseCookies = false,
                    AllowAutoRedirect = false,
                    AutomaticDecompression = DecompressionMethods.All,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    UseProxy = false,
                    ConnectCallback = (context, ct) => SsrfConnector.ConnectAsync(resolver, allowPrivate, context, ct),
                };
            });
        services.TryAddSingleton<IHostAddressResolver, DnsHostAddressResolver>();

        // The Jev and rewrite clients are chosen when the container is built: the mock needs no key and no network. The
        // caches are the host's (PostgreSQL, EshopGuard.Data); without one the library caches nothing, and a host must never
        // give one to the mock, whose made-up answers must never reach the real cache.
        var probe = new EshopGuardOptions();
        configure(probe);
        if (probe.Jev.UseMock)
        {
            services.TryAddSingleton<IJevClient, MockJevClient>();
        }
        else
        {
            services.AddHttpClient(JevClient.HttpClientName, (provider, client) =>
                client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<JevOptions>>().Value.TimeoutSeconds));
            services.TryAddSingleton<IJevClient, JevClient>();
        }

        if (probe.Rewrite.UseMock)
        {
            services.TryAddSingleton<IRewriteClient, MockRewriteClient>();
        }
        else
        {
            services.AddHttpClient(OpenAiRewriteClient.HttpClientName, (provider, client) =>
                client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<RewriteOptions>>().Value.TimeoutSeconds));
            services.TryAddSingleton<IRewriteClient, OpenAiRewriteClient>();
        }

        services.TryAddSingleton<IJevCache, NullJevCache>();
        services.TryAddSingleton<IRewriteCache, NullRewriteCache>();

        services.TryAddSingleton<IOptions<RewriteOptions>>(provider =>
            Microsoft.Extensions.Options.Options.Create(provider.GetRequiredService<IOptions<EshopGuardOptions>>().Value.Rewrite));

        // The Jev namespace sees only its own options, so it can later move to a separate package.
        services.TryAddSingleton<IOptions<JevOptions>>(provider =>
            Microsoft.Extensions.Options.Options.Create(provider.GetRequiredService<IOptions<EshopGuardOptions>>().Value.Jev));

        services.TryAddSingleton<IPageFetcher, HttpPageFetcher>();
        services.TryAddSingleton<IRuleTextProvider, YamlRuleTextProvider>();
        services.TryAddSingleton<IRuleSetProvider, YamlRuleSetProvider>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ITextRewriter, PageRewriter>();
        services.TryAddSingleton<ContentExtractor>();
        services.TryAddSingleton<IPageExtractor>(provider => provider.GetRequiredService<ContentExtractor>());
        services.TryAddSingleton<PageClassifier>();
        services.TryAddSingleton<SentenceSplitter>();
        services.TryAddSingleton<SegmentBuilder>();
        services.TryAddSingleton<SegmentEvaluator>();
        services.TryAddSingleton<PageSieve>();
        // Profiles are the shop's template, not a cached answer; the host keeps them (PostgreSQL), otherwise only in memory.
        services.TryAddSingleton<IPageProfileStore, InMemoryPageProfileStore>();
        services.TryAddSingleton<IProfileModel, OpenAiProfileModel>();

        // Storage of one run in memory; the web application registers its database and file store before this call.
        services.TryAddSingleton<IPageContentStore, InMemoryPageContentStore>();
        services.TryAddSingleton<IPageStore, InMemoryPageStore>();
        services.TryAddSingleton<IUrlFrontierStore, InMemoryUrlFrontierStore>();
        services.TryAddSingleton<IRateLimiter, LocalRateLimiter>();

        // The steps of the analysis; the CLI runs them in memory one after another, the worker as jobs.
        services.TryAddSingleton<DiscoveryStep>();
        services.TryAddSingleton<FetchStep>();
        services.TryAddSingleton<ExtractStep>();
        services.TryAddSingleton<ProfileStep>();
        services.TryAddSingleton<SegmentStep>();
        services.TryAddSingleton<EstimateStep>();
        services.TryAddSingleton<SieveStep>();
        services.TryAddSingleton<EvaluateStep>();
        services.TryAddSingleton<RulesStep>();
        services.TryAddSingleton<RewriteStep>();
        services.TryAddSingleton<IEshopGuard, InMemoryPipelineRunner>();

        // Places of sale and language versions (change 7); the mock model needs no key and no network.
        if (probe.Rewrite.UseMock)
        {
            services.TryAddSingleton<Markets.IMarketModel>(new Markets.MockMarketModel());
        }
        else
        {
            services.TryAddSingleton<Markets.IMarketModel, Markets.OpenAiMarketModel>();
        }

        services.TryAddTransient<Languages.VersionAccessProbe>();
        services.TryAddSingleton<Languages.VersionCrawler>();
        services.TryAddSingleton<Languages.ITextLanguageModel, Languages.TextLanguageModel>();
        services.TryAddTransient<Markets.IMarketsAnalyzer, Markets.MarketsAnalyzer>();
        services.TryAddSingleton<ReportTexts>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReportWriter, MarkdownReportWriter>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReportWriter, FindingsJsonWriter>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReportWriter, FindingsCsvWriter>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReportWriter, SegmentsCsvWriter>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReportWriter, PagesJsonlWriter>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReportWriter, SieveCsvWriter>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReportWriter, ProfilesJsonWriter>());
        return services;
    }
}
