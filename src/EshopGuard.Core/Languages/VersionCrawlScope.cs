using EshopGuard.Core.Crawl;
using EshopGuard.Core.Markets;

namespace EshopGuard.Core.Languages;

/// <summary>A query parameter of an address (<c>lang=sk</c>).</summary>
public sealed record QueryParameter(string Name, string Value);

/// <summary>
/// The part of a shop that is one language version, crawled on its own (change 7, design section 4): its host, the root path
/// of the version, the root paths of the other versions on the same host (left out), the query parameter of the version,
/// its own cookies and <c>Accept-Language</c>. The versions of a shop never mix and every page carries the language of its
/// version. Cookies are kept per scope, never shared between sites or tenants.
/// </summary>
public sealed record VersionCrawlScope
{
    /// <summary>Host of the version, lower case (a leading <c>www.</c> is ignored when comparing).</summary>
    public required string Host { get; init; }

    /// <summary>Explicit port, or -1 for the default port of the scheme.</summary>
    public int Port { get; init; } = -1;

    /// <summary>Root path of the version (<c>/</c>, <c>/sk/</c>).</summary>
    public string BasePath { get; init; } = "/";

    /// <summary>Root paths of the other versions on the same host, never crawled in this one.</summary>
    public IReadOnlyList<string> ExcludedBasePaths { get; init; } = [];

    /// <summary>Query parameters every address of the version has (a version switched by <c>?lang=sk</c> in every link).</summary>
    public IReadOnlyList<QueryParameter> RequiredQuery { get; init; } = [];

    /// <summary>Query parameters of the other versions; an address with one of them belongs to another version.</summary>
    public IReadOnlyList<QueryParameter> ExcludedQuery { get; init; } = [];

    /// <summary>Cookies sent with every request of the version (<c>lang=sk</c> after the switch).</summary>
    public IReadOnlyDictionary<string, string> Cookies { get; init; } = new Dictionary<string, string>();

    /// <summary><c>Accept-Language</c> of the requests of the version; null keeps the default of the crawler.</summary>
    public string? AcceptLanguage { get; init; }

    /// <summary>Language the pages of the version should declare in <c>html lang</c>; null when not known.</summary>
    public string? ExpectedLanguage { get; init; }

    /// <summary>Language of the version, written to every page (<c>PageInfo.Language</c>).</summary>
    public required string VersionLanguage { get; init; }

    /// <summary>The address belongs to the version: its host and port, under its root path and none of the excluded ones, with its query.</summary>
    public bool Contains(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!string.Equals(UrlTools.StripWww(url.Host), UrlTools.StripWww(Host), StringComparison.OrdinalIgnoreCase)
            || (url.IsDefaultPort ? -1 : url.Port) != Port)
        {
            return false;
        }

        var path = url.AbsolutePath;
        if (!UnderPath(path, BasePath) || ExcludedBasePaths.Any(excluded => UnderPath(path, excluded)))
        {
            return false;
        }

        var query = Query(url);
        return RequiredQuery.All(p => query.Contains(p)) && !ExcludedQuery.Any(p => query.Contains(p));
    }

    /// <summary>The scope of a version whose root is <paramref name="root"/>.</summary>
    public static VersionCrawlScope ForRoot(Uri root, string language) => new()
    {
        Host = root.Host.ToLowerInvariant(),
        Port = root.IsDefaultPort ? -1 : root.Port,
        BasePath = RootPath(root.AbsolutePath),
        VersionLanguage = LanguageTags.Normalize(language),
        ExpectedLanguage = LanguageTags.Normalize(language),
    };

    /// <summary>The path as the root of a version: always ending with a slash (<c>/sk</c> gives <c>/sk/</c>).</summary>
    public static string RootPath(string path) => path.Length == 0 ? "/" : path.EndsWith('/') ? path : path + "/";

    private static bool UnderPath(string path, string root)
    {
        if (root is "" or "/")
        {
            return true;
        }

        var trimmed = root.TrimEnd('/');
        return path.Equals(trimmed, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(trimmed + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<QueryParameter> Query(Uri url) =>
        url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Select(p => new QueryParameter(Uri.UnescapeDataString(p[0]).ToLowerInvariant(), p.Length > 1 ? Uri.UnescapeDataString(p[1]).ToLowerInvariant() : ""))
            .ToHashSet();
}
