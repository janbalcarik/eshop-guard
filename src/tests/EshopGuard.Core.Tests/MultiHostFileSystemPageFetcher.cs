using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using EshopGuard.Core.Crawl;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Serves the fixture e-shops of <c>Fixtures/versions</c> without network, one folder per host (change 7, task 4.1). A
/// folder's <c>_server.json</c> says how it answers: a cookie or a query parameter that sets it, or <c>Accept-Language</c>,
/// selects the variant in <c>_lang-&lt;language&gt;/</c>; listed paths redirect. Records every request with its cookies and
/// language, so tests see what each crawl scope sent.
/// </summary>
internal sealed class MultiHostFileSystemPageFetcher : IPageFetcher
{
    private static readonly HashSet<string> TextExtensions = [".html", ".xml", ".txt"];

    private readonly Dictionary<string, string> _hosts;

    /// <param name="hosts">Host (lower case) → folder.</param>
    public MultiHostFileSystemPageFetcher(IReadOnlyDictionary<string, string> hosts)
    {
        _hosts = hosts.ToDictionary(h => h.Key.ToLowerInvariant(), h => h.Value);
    }

    /// <summary>Folder of the fixture shops.</summary>
    public static string VersionsRoot => Path.Combine(AppContext.BaseDirectory, "Fixtures", "versions");

    /// <summary>Every fixture shop on its host (<c>path-shop.cz</c>, <c>domain-shop.cz</c>, <c>domain-shop.sk</c>…).</summary>
    public static MultiHostFileSystemPageFetcher ForVersions() => new(new Dictionary<string, string>
    {
        ["path-shop.cz"] = Path.Combine(VersionsRoot, "path-shop"),
        ["domain-shop.cz"] = Path.Combine(VersionsRoot, "domain-shop-cz"),
        ["domain-shop.sk"] = Path.Combine(VersionsRoot, "domain-shop-sk"),
        ["cookie-shop.cz"] = Path.Combine(VersionsRoot, "cookie-shop"),
        ["accept-shop.cz"] = Path.Combine(VersionsRoot, "accept-shop"),
        ["script-shop.cz"] = Path.Combine(VersionsRoot, "script-shop"),
        ["widget-shop.cz"] = Path.Combine(VersionsRoot, "widget-shop"),
        ["redirect-shop.cz"] = Path.Combine(VersionsRoot, "redirect-shop"),
        ["spa-shop.cz"] = Path.Combine(VersionsRoot, "spa-shop"),
    });

    /// <summary>Requests in order: URL, the Cookie header and Accept-Language.</summary>
    public ConcurrentQueue<(Uri Url, string? Cookie, string? AcceptLanguage)> Requests { get; } = new();

    public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct) => FetchAsync(new FetchRequest(url), ct);

    public Task<FetchResponse> FetchAsync(FetchRequest request, CancellationToken ct)
    {
        var url = request.Url;
        Requests.Enqueue((url, CookieJar.Header(request.Cookies), request.AcceptLanguage));
        if (!_hosts.TryGetValue(url.Host.ToLowerInvariant(), out var root))
        {
            return Task.FromResult(new FetchResponse { Url = url, Error = "unknown host" });
        }

        var config = Config(root);
        if (config.Redirects.TryGetValue(url.AbsolutePath, out var target))
        {
            return Task.FromResult(new FetchResponse { Url = url, StatusCode = 302, RedirectLocation = new Uri(url, target) });
        }

        var setCookies = new List<string>();
        string? language = null;
        var query = url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => p.Length > 1 ? p[1] : "");
        if (config.QuerySetsCookie is { } parameter && query.TryGetValue(parameter, out var fromQuery))
        {
            language = fromQuery;
            setCookies.Add($"{config.Cookie ?? parameter}={fromQuery}; Path=/");
        }
        else if (config.Cookie is { } cookie && request.Cookies?.TryGetValue(cookie, out var fromCookie) == true)
        {
            language = fromCookie;
        }
        else if (config.AcceptLanguage && request.AcceptLanguage is { } accept)
        {
            language = accept.Split(',')[0].Split(';')[0].Split('-')[0].Trim().ToLowerInvariant();
        }

        var relative = Uri.UnescapeDataString(url.AbsolutePath).TrimStart('/');
        if (relative.Length == 0 || relative.EndsWith('/'))
        {
            relative += "index.html";
        }

        var variant = language is null ? null : Path.Combine(root, "_lang-" + language, relative);
        var path = Path.GetFullPath(variant is not null && File.Exists(variant) ? variant : Path.Combine(root, relative));
        if (!path.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) || !File.Exists(path) || relative.StartsWith('_'))
        {
            return Task.FromResult(new FetchResponse { Url = url, StatusCode = 404, SetCookies = setCookies });
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();
        var body = File.ReadAllBytes(path);
        if (TextExtensions.Contains(extension))
        {
            body = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(body).Replace("{{base_url}}", url.GetLeftPart(UriPartial.Authority)));
        }

        return Task.FromResult(new FetchResponse
        {
            Url = url,
            StatusCode = 200,
            MediaType = extension switch
            {
                ".html" => "text/html",
                ".xml" => "application/xml",
                ".txt" => "text/plain",
                _ => "application/octet-stream",
            },
            Charset = "utf-8",
            Body = body,
            SetCookies = setCookies,
        });
    }

    private static ServerConfig Config(string root)
    {
        var file = Path.Combine(root, "_server.json");
        if (!File.Exists(file))
        {
            return new ServerConfig();
        }

        using var json = JsonDocument.Parse(File.ReadAllText(file));
        var element = json.RootElement;
        return new ServerConfig
        {
            Cookie = element.TryGetProperty("cookie", out var cookie) ? cookie.GetString() : null,
            QuerySetsCookie = element.TryGetProperty("query_sets_cookie", out var query) ? query.GetString() : null,
            AcceptLanguage = element.TryGetProperty("accept_language", out var accept) && accept.GetBoolean(),
            Redirects = element.TryGetProperty("redirects", out var redirects)
                ? redirects.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!)
                : [],
        };
    }

    private sealed class ServerConfig
    {
        public string? Cookie { get; init; }

        public string? QuerySetsCookie { get; init; }

        public bool AcceptLanguage { get; init; }

        public Dictionary<string, string> Redirects { get; init; } = [];
    }
}
