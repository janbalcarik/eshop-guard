using EshopGuard.Core.Crawl;
using EshopGuard.Core.Extract;
using EshopGuard.Core.Markets;
using EshopGuard.Core.Options;
using EshopGuard.Core.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Languages;

/// <summary>How a version is reached after a download: its state, switch, crawl scope, declared language and codes.</summary>
public sealed record VersionAccess(string Status, string SwitchMethod, VersionCrawlScope? Scope, string? DeclaredLanguage, IReadOnlyList<string> Codes);

/// <summary>
/// Tries how every found version is reached (change 7, design section 4), with robots.txt, the SSRF protection of the fetcher,
/// the User-Agent of the crawler and its pace:
/// - an address of its own (path, subdomain, domain): its home page must declare the language of the version;
/// - a language parameter: when the answer sets a cookie, the version is crawled with that cookie, otherwise with the parameter;
/// - no address of its own: the home page with <c>Accept-Language</c> of the version; when that does not give the version, a
///   translation service in the browser means the original text is checked (<c>version_browser_translation</c>), a switch only
///   in script means <c>needs_browser</c>, and anything else <c>mismatch</c>;
/// - a version whose address answers in another language (redirect by IP or language) is <c>mismatch</c>;
/// - a version whose home page has almost no text is reported per version (<c>version_text_not_loaded</c>).
/// </summary>
public sealed class VersionAccessProbe
{
    private readonly IPageFetcher _fetcher;
    private readonly CrawlOptions _crawl;
    private readonly ILogger _logger;
    private readonly Dictionary<string, RobotsTxt> _robots = new(StringComparer.OrdinalIgnoreCase);
    private readonly AdaptiveGate _gate;
    private readonly CrawlCounters _counters = new();

    public VersionAccessProbe(IPageFetcher fetcher, IOptions<EshopGuardOptions> options, ILogger<VersionAccessProbe> logger)
    {
        _fetcher = fetcher;
        _crawl = options.Value.Crawl;
        _logger = logger;
        _gate = new AdaptiveGate(_crawl.RequestsPerSecond, _crawl.MaxRequestsPerSecond, adaptive: true);
    }

    /// <summary>Requests sent by the probe.</summary>
    public int Requests => _counters.Requests;

    /// <summary>Tries every version that is not unsupported or excluded and gives each its scope; the main version gets its scope last.</summary>
    public async Task<IReadOnlyList<LanguageVersionCandidate>> ProbeAllAsync(
        IReadOnlyList<LanguageVersionCandidate> versions, MarketSignals signals, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(versions);
        var tried = new List<LanguageVersionCandidate>();
        foreach (var version in versions)
        {
            if (version.IsMain || version.Status is VersionStatus.Unsupported or VersionStatus.Excluded)
            {
                tried.Add(version);
                continue;
            }

            var access = await ProbeAsync(version, signals, ct);
            tried.Add(version with
            {
                Status = version.Status == VersionStatus.NeedsConfirmation && access.Status == VersionStatus.Active ? VersionStatus.NeedsConfirmation : access.Status,
                SwitchMethod = access.SwitchMethod,
                Scope = access.Scope,
                DeclaredLanguage = access.DeclaredLanguage,
                Language = version.Language ?? access.DeclaredLanguage,
                Codes = [.. version.Codes, .. access.Codes],
            });
        }

        return VersionScopes.Assign(tried, signals.HomeTextNotLoaded);
    }

    /// <summary>How one version is reached.</summary>
    public async Task<VersionAccess> ProbeAsync(LanguageVersionCandidate version, MarketSignals signals, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(version);
        var url = new Uri(version.BaseUrl);
        var language = version.Language;
        if (version.SwitchMethod is SwitchMethods.Path or SwitchMethods.Subdomain or SwitchMethods.Domain or SwitchMethods.Query)
        {
            var page = await DownloadAsync(url, null, null, ct);
            if (page.Code is { } failure)
            {
                return new VersionAccess(failure == VersionCodes.RobotsDisallowed ? VersionStatus.Active : VersionStatus.Mismatch, version.SwitchMethod, null, null, [failure]);
            }

            language ??= page.Language;
            var matches = language is not null && (page.Language is null || LanguageTags.SamePrimary(page.Language, language));
            if (matches && version.SwitchMethod == SwitchMethods.Query)
            {
                var root = new Uri(url.GetLeftPart(UriPartial.Authority) + "/");
                var parameter = url.Query.TrimStart('?').Split('&')[0].Split('=', 2);
                if (page.Cookies.Count > 0)
                {
                    // The switch link sets the cookie; following any switch link later would change the version of the crawl.
                    return Access(VersionStatus.Active, SwitchMethods.Cookie, VersionCrawlScope.ForRoot(root, language!) with
                    {
                        Cookies = page.Cookies,
                        ExcludedQuery = [new QueryParameter(parameter[0].ToLowerInvariant(), VersionCrawlScope.AnyValue)],
                    }, page);
                }

                return Access(VersionStatus.Active, SwitchMethods.Query,
                    VersionCrawlScope.ForRoot(root, language!) with { RequiredQuery = [new QueryParameter(parameter[0].ToLowerInvariant(), parameter.Length > 1 ? parameter[1].ToLowerInvariant() : "")] }, page);
            }

            if (matches)
            {
                return Access(VersionStatus.Active, version.SwitchMethod, VersionCrawlScope.ForRoot(page.FinalUrl.AbsolutePath == url.AbsolutePath ? url : page.FinalUrl, language!), page);
            }

            // The address of the version answers in another language: a redirect by IP or by the language of the browser.
            _logger.LogInformation("Version {Language} at {Url} answers with html lang {Declared}", language, url, page.Language);
            return new VersionAccess(VersionStatus.Mismatch, version.SwitchMethod, null, page.Language, [VersionCodes.LanguageMismatch]);
        }

        if (language is not null)
        {
            var page = await DownloadAsync(url, null, language, ct);
            if (page.Code is null && page.Language is not null && LanguageTags.SamePrimary(page.Language, language))
            {
                return Access(VersionStatus.Active, SwitchMethods.AcceptLanguage, VersionCrawlScope.ForRoot(url, language) with { AcceptLanguage = language }, page);
            }
        }

        if (signals.BrowserTranslationWidgets.Count > 0)
        {
            return new VersionAccess(VersionStatus.Active, SwitchMethods.BrowserTranslation, null, null, [VersionCodes.BrowserTranslation]);
        }

        return signals.ScriptOnlySwitchElements.Count > 0
            ? new VersionAccess(VersionStatus.NeedsBrowser, SwitchMethods.Script, null, null, [VersionCodes.NeedsBrowser])
            : new VersionAccess(VersionStatus.Mismatch, version.SwitchMethod, null, null, [VersionCodes.LanguageMismatch]);
    }

    private static VersionAccess Access(string status, string method, VersionCrawlScope scope, Download page) =>
        new(status, method, scope, page.Language, page.TextNotLoaded ? [VersionCodes.TextNotLoaded] : []);

    /// <summary>The home page of a version: robots.txt of its host, same-site redirects, the cookies it sets.</summary>
    private async Task<Download> DownloadAsync(Uri url, Dictionary<string, string>? cookies, string? acceptLanguage, CancellationToken ct)
    {
        var jar = cookies ?? [];
        var current = UrlTools.Normalize(url);
        for (var hop = 0; hop <= _crawl.MaxRedirects; hop++)
        {
            if (!(await RobotsAsync(current, ct)).IsAllowed(current))
            {
                return new Download(current, null, false, jar, VersionCodes.RobotsDisallowed);
            }

            var response = await FetchStep.FetchPacedAsync(_fetcher, _gate,
                new FetchRequest(current) { Cookies = jar.Count > 0 ? new Dictionary<string, string>(jar) : null, AcceptLanguage = acceptLanguage },
                _counters, _logger, ct);
            CookieJar.Apply(jar, response.SetCookies, DateTimeOffset.UtcNow);
            if (response.RedirectLocation is { } location)
            {
                var next = UrlTools.Normalize(location);
                if (!UrlTools.IsSameSite(next, current))
                {
                    return new Download(current, null, false, jar, VersionCodes.Unreachable);
                }

                current = next;
                continue;
            }

            if (!response.IsSuccess || !FetchStep.IsHtml(response))
            {
                return new Download(current, null, false, jar, VersionCodes.Unreachable);
            }

            var document = ParsedPage.Parse(current, HtmlDecoding.Decode(response.Body!, response.Charset)).Document;
            var text = RenderCheck.Inspect(document).VisibleChars < _crawl.MinPageTextChars;
            return new Download(current, ContentExtractor.ReadHtmlLang(document), text, jar, null);
        }

        return new Download(current, null, false, jar, VersionCodes.Unreachable);
    }

    private async Task<RobotsTxt> RobotsAsync(Uri url, CancellationToken ct)
    {
        var key = url.GetLeftPart(UriPartial.Authority);
        if (_robots.TryGetValue(key, out var known))
        {
            return known;
        }

        var response = await FetchStep.FetchPacedAsync(_fetcher, _gate, new FetchRequest(new Uri(url, "/robots.txt")), _counters, _logger, ct);
        var robots = response.IsSuccess
            ? RobotsTxt.Parse(HtmlDecoding.Decode(response.Body!, response.Charset), DiscoveryStep.ProductToken(_crawl.UserAgent))
            : response.StatusCode is >= 400 and < 500 ? RobotsTxt.AllowAll : RobotsTxt.DisallowAll;
        _robots[key] = robots;
        return robots;
    }

    private sealed record Download(Uri FinalUrl, string? Language, bool TextNotLoaded, Dictionary<string, string> Cookies, string? Code);
}

/// <summary>The crawl scopes of the versions of one shop: the versions on the same host leave out each other's addresses.</summary>
public static class VersionScopes
{
    /// <summary>
    /// The main version gets the scope of the site without the paths and parameters of the other versions; versions on the same
    /// host without an address of their own (cookie, browser language) leave out the same paths.
    /// </summary>
    public static IReadOnlyList<LanguageVersionCandidate> Assign(IReadOnlyList<LanguageVersionCandidate> versions, bool mainTextNotLoaded = false)
    {
        var main = versions.First(v => v.IsMain);
        var home = new Uri(main.BaseUrl);
        var others = versions.Where(v => !v.IsMain).ToList();
        var paths = others
            .Where(v => v.SwitchMethod == SwitchMethods.Path && Uri.TryCreate(v.BaseUrl, UriKind.Absolute, out var u) && UrlTools.IsSameSite(u, home))
            .Select(v => VersionCrawlScope.RootPath(new Uri(v.BaseUrl).AbsolutePath))
            .Where(p => p != "/")
            .Distinct()
            .ToList();
        var queries = others.Where(v => v.Scope is not null)
            .SelectMany(v => v.Scope!.RequiredQuery.Concat(v.Scope.ExcludedQuery.Where(q => q.Value == VersionCrawlScope.AnyValue)))
            .Distinct()
            .ToList();
        return versions.Select(v =>
        {
            if (v.IsMain)
            {
                var mainScope = VersionCrawlScope.ForRoot(home, v.Language ?? "und") with { ExcludedBasePaths = paths, ExcludedQuery = queries };
                return v with { Scope = mainScope, Codes = mainTextNotLoaded && !v.Codes.Contains(VersionCodes.TextNotLoaded) ? [.. v.Codes, VersionCodes.TextNotLoaded] : v.Codes };
            }

            return v.Scope is { BasePath: "/" } scope && UrlTools.IsSameSite(new Uri(v.BaseUrl), home) && scope.RequiredQuery.Count == 0
                ? v with { Scope = scope with { ExcludedBasePaths = paths, ExcludedQuery = scope.ExcludedQuery.Concat(queries).Distinct().ToList() } }
                : v;
        }).ToList();
    }
}
