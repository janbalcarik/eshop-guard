using System.Net;
using System.Security.Cryptography;
using EshopGuard.Application.Email;
using EshopGuard.Application.Localization;
using EshopGuard.Data.Connections;
using EshopGuard.Tests.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace EshopGuard.Api.Tests;

/// <summary>
/// The API in environment <c>Testing</c> (user-secrets <c>eshopguard-api</c> with the development database are not loaded),
/// with an explicit <c>ConnectionStrings:App</c>, a temporary file store, captured logs and e-mails, a random key of the
/// hashes (so buckets of earlier runs do not count), a fast in-memory limiter and, optionally, a fake clock. The client
/// address comes from the header <see cref="ClientIpHeader"/> (TestServer has none).
/// </summary>
internal sealed class ApiFactory(
    string? appConnectionString,
    bool withStartupGuard = true,
    bool withStorageRoot = true,
    string? storageRoot = null,
    FakeTimeProvider? time = null,
    string? ipHashKey = null,
    IReadOnlyDictionary<string, string?>? settings = null,
    Action<IServiceCollection>? services = null) : WebApplicationFactory<Program>
{
    public const string ClientIpHeader = "X-Test-Client-Ip";
    public const string FrontendBaseUrl = "https://app.eshopguard.test";

    private readonly DirectoryInfo _blobs = Directory.CreateTempSubdirectory("eshopguard-api-");

    public InMemoryLoggerProvider Logs { get; } = new();

    public CapturingEmailTransport Emails { get; } = new();

    public FakeTimeProvider? Time { get; } = time;

    public string IpHashKey { get; } = ipHashKey ?? NewKey();

    public static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>A client over HTTPS (the cookies are <c>Secure</c>) that keeps cookies and does not follow redirects.</summary>
    public ApiClient CreateApiClient(string? ip = null) => new(CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true,
    }), ip ?? ApiClient.RandomIp());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:App", appConnectionString ?? string.Empty);
        builder.UseSetting("Storage:Provider", "FileSystem");
        builder.UseSetting("Storage:FileSystem:Root", withStorageRoot ? storageRoot ?? _blobs.FullName : string.Empty);
        builder.UseSetting("Frontend:BaseUrl", FrontendBaseUrl);
        builder.UseSetting("Security:IpHashKey", IpHashKey);
        builder.UseSetting("Legal:TermsVersion", "test-terms-1");
        builder.UseSetting("Legal:PrivacyVersion", "test-privacy-1");
        builder.UseSetting("RateLimiting:AuthPerMinute", "100000");
        builder.UseSetting("RateLimiting:PerMinute", "100000");
        builder.UseSetting("DataProtection:KeysPath", Path.Combine(_blobs.FullName, "keys"));
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(s =>
        {
            s.AddSingleton<ILoggerProvider>(Logs);
            s.Replace(ServiceDescriptor.Singleton<IEmailTransport>(Emails));
            s.Replace(ServiceDescriptor.Singleton<IRefCatalog>(sp => new EnabledLocalesCatalog(ActivatorUtilities.CreateInstance<RefCatalog>(sp))));
            if (Time is not null)
            {
                s.Replace(ServiceDescriptor.Singleton<TimeProvider>(Time));
            }

            s.AddSingleton<IStartupFilter, ClientIpStartupFilter>();
            if (!withStartupGuard)
            {
                s.Remove(s.Single(d => d.ImplementationType == typeof(DatabaseStartupGuard)));
            }

            services?.Invoke(s);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _blobs.Exists)
        {
            _blobs.Delete(recursive: true);
        }
    }

    /// <summary>
    /// The real catalog with <c>sk</c> and <c>cs</c> enabled, as they will be once the texts of the frontend are complete; the
    /// shared test database keeps them off as seeded (other test projects check the seed).
    /// </summary>
    private sealed class EnabledLocalesCatalog(IRefCatalog inner) : IRefCatalog
    {
        public async Task<IReadOnlyList<LocaleInfo>> GetLocalesAsync(CancellationToken ct = default) =>
            (await inner.GetLocalesAsync(ct)).Select(l => l.Code is "sk" or "cs" ? l with { Enabled = true } : l).ToList();

        public Task<IReadOnlyList<MarketInfo>> GetMarketsAsync(CancellationToken ct = default) => inner.GetMarketsAsync(ct);
    }

    /// <summary>The address of the client from <see cref="ClientIpHeader"/>, before everything else.</summary>
    private sealed class ClientIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (IPAddress.TryParse(context.Request.Headers[ClientIpHeader].ToString(), out var ip))
                {
                    context.Connection.RemoteIpAddress = ip;
                }

                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
