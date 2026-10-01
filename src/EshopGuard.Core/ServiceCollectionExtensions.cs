using System.Net;
using EshopGuard.Core.Cache;
using EshopGuard.Core.Classify;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Extract;
using EshopGuard.Core.Fix;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Options;
using EshopGuard.Core.Profiles;
using EshopGuard.Core.Report;
using EshopGuard.Core.Rules;
using EshopGuard.Core.Segmentation;
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
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            });

        // The Jev client and cache are chosen when the container is built: the mock needs no key and no network,
        // and its made-up answers must never reach the real cache.
        var probe = new EshopGuardOptions();
        configure(probe);
        if (probe.Jev.UseMock)
        {
            services.TryAddSingleton<IJevClient, MockJevClient>();
            services.TryAddSingleton<IJevCache, NullJevCache>();
        }
        else
        {
            services.AddHttpClient(JevClient.HttpClientName, (provider, client) =>
                client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<JevOptions>>().Value.TimeoutSeconds));
            services.TryAddSingleton<IJevClient, JevClient>();
            if (probe.Cache.Enabled)
            {
                services.TryAddSingleton<IJevCache, SqliteJevCache>();
            }
            else
            {
                services.TryAddSingleton<IJevCache, NullJevCache>();
            }
        }

        // The rewrite model follows the same pattern: the mock pays nothing and never fills the real cache.
        if (probe.Rewrite.UseMock)
        {
            services.TryAddSingleton<IRewriteClient, MockRewriteClient>();
            services.TryAddSingleton<IRewriteCache, NullRewriteCache>();
        }
        else
        {
            services.AddHttpClient(OpenAiRewriteClient.HttpClientName, (provider, client) =>
                client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<RewriteOptions>>().Value.TimeoutSeconds));
            services.TryAddSingleton<IRewriteClient, OpenAiRewriteClient>();
            if (probe.Cache.Enabled)
            {
                services.TryAddSingleton<IRewriteCache, SqliteRewriteCache>();
            }
            else
            {
                services.TryAddSingleton<IRewriteCache, NullRewriteCache>();
            }
        }

        services.TryAddSingleton<IOptions<RewriteOptions>>(provider =>
            Microsoft.Extensions.Options.Options.Create(provider.GetRequiredService<IOptions<EshopGuardOptions>>().Value.Rewrite));

        // The Jev namespace sees only its own options, so it can later move to a separate package.
        services.TryAddSingleton<IOptions<JevOptions>>(provider =>
            Microsoft.Extensions.Options.Options.Create(provider.GetRequiredService<IOptions<EshopGuardOptions>>().Value.Jev));

        services.TryAddSingleton<IPageFetcher, HttpPageFetcher>();
        services.TryAddSingleton<IRuleSetProvider, YamlRuleSetProvider>();
        services.TryAddSingleton<ITextRewriter, PageRewriter>();
        services.TryAddSingleton<ContentExtractor>();
        services.TryAddSingleton<PageClassifier>();
        services.TryAddSingleton<SentenceSplitter>();
        services.TryAddSingleton<SegmentBuilder>();
        services.TryAddSingleton<SegmentEvaluator>();
        services.TryAddSingleton<PageSieve>();
        services.TryAddSingleton<Crawler>();
        // Profiles are kept even with --no-cache: they are the shop's template, not a cached answer.
        services.TryAddSingleton<IPageProfileStore, SqlitePageProfileStore>();
        services.TryAddSingleton<IProfileModel, OpenAiProfileModel>();
        services.TryAddSingleton<PageProfiler>();
        services.TryAddSingleton<IEshopGuard, EshopGuardService>();
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
