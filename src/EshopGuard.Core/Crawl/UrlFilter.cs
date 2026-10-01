using System.Text.RegularExpressions;

namespace EshopGuard.Core.Crawl;

/// <summary>
/// Decides which URLs are not pages worth downloading: files, cart, login, search, sorting, paging, filters,
/// and the user's include and exclude patterns.
/// </summary>
internal sealed class UrlFilter
{
    private static readonly HashSet<string> NonPageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf", "jpg", "jpeg", "png", "gif", "webp", "svg", "avif", "ico", "bmp", "tif", "tiff",
        "zip", "rar", "7z", "gz", "tar", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "odt", "ods", "csv",
        "mp4", "mp3", "avi", "mov", "webm", "wav", "css", "js", "json", "xml", "txt", "rss", "atom",
        "woff", "woff2", "ttf", "eot", "exe", "dmg", "apk", "msi",
    };

    private readonly IReadOnlyList<Regex> _defaultExclusions;
    private readonly IReadOnlyList<Regex> _include;
    private readonly IReadOnlyList<Regex> _exclude;

    public UrlFilter(IEnumerable<string> defaultExclusions, IEnumerable<string> include, IEnumerable<string> exclude)
    {
        _defaultExclusions = Compile(defaultExclusions);
        _include = Compile(include);
        _exclude = Compile(exclude);
    }

    public static bool IsPdf(Uri url) => UrlTools.Extension(url) == "pdf";

    public static bool IsNonPageFile(Uri url) => NonPageExtensions.Contains(UrlTools.Extension(url));

    /// <summary>True when the URL must not be downloaded as a page.</summary>
    public bool IsExcluded(Uri url)
    {
        if (IsNonPageFile(url))
        {
            return true;
        }

        var pathAndQuery = Uri.UnescapeDataString(url.PathAndQuery);
        if (_defaultExclusions.Any(r => Matches(r, pathAndQuery)))
        {
            return true;
        }

        var absolute = url.AbsoluteUri;
        if (_include.Count > 0 && !_include.Any(r => Matches(r, absolute)))
        {
            return true;
        }

        return _exclude.Any(r => Matches(r, absolute));
    }

    /// <summary>
    /// The time limit is wall-clock time, so a busy machine can hit it even with a simple pattern. A pattern that
    /// cannot be evaluated counts as matching, so the URL is rather skipped than the whole scan failing.
    /// </summary>
    private static bool Matches(Regex regex, string input)
    {
        try
        {
            return regex.IsMatch(input);
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
    }

    private static List<Regex> Compile(IEnumerable<string> patterns) =>
        patterns
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(Create)
            .ToList();

    /// <summary>Linear-time engine when the pattern allows it (no lookarounds or backreferences), otherwise the default one.</summary>
    private static Regex Create(string pattern)
    {
        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        try
        {
            return new Regex(pattern, options | RegexOptions.NonBacktracking, TimeSpan.FromSeconds(2));
        }
        catch (NotSupportedException)
        {
            return new Regex(pattern, options, TimeSpan.FromSeconds(2));
        }
    }
}
