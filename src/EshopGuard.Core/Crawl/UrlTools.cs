using System.Text;

namespace EshopGuard.Core.Crawl;

/// <summary>
/// URL parsing, normalization and same-site checks.
/// </summary>
internal static class UrlTools
{
    private static readonly HashSet<string> TrackingParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "gclid", "fbclid", "msclkid", "mc_cid", "mc_eid", "_ga", "yclid", "dclid", "srsltid",
    };

    /// <summary>Resolves an href against the base URL; returns null for non-HTTP links (mailto:, tel:, javascript:, fragments).</summary>
    public static Uri? TryResolve(string? href, Uri baseUri)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var trimmed = href.Trim();
        if (trimmed.StartsWith('#'))
        {
            return null;
        }

        if (!Uri.TryCreate(baseUri, trimmed, out var uri))
        {
            return null;
        }

        return uri.Scheme is "http" or "https" ? uri : null;
    }

    /// <summary>Lower-case scheme and host, no default port, no fragment, no tracking parameters, non-empty path.</summary>
    public static Uri Normalize(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Fragment = "",
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.Host.ToLowerInvariant(),
        };

        if (uri.IsDefaultPort)
        {
            builder.Port = -1;
        }

        if (builder.Path.Length == 0)
        {
            builder.Path = "/";
        }

        builder.Query = RemoveTrackingParameters(uri.Query);
        return builder.Uri;
    }

    /// <summary>Same host (ignoring a leading www.) and the same explicit port.</summary>
    public static bool IsSameSite(Uri a, Uri b) =>
        string.Equals(StripWww(a.Host), StripWww(b.Host), StringComparison.OrdinalIgnoreCase)
        && (a.IsDefaultPort ? -1 : a.Port) == (b.IsDefaultPort ? -1 : b.Port);

    /// <summary>Rewrites a same-site URL to the scheme and host spelling of the scanned site, so http/https and www variants are not downloaded twice.</summary>
    public static Uri AlignWith(Uri uri, Uri site)
    {
        if (uri.Scheme == site.Scheme && uri.Host == site.Host)
        {
            return uri;
        }

        var builder = new UriBuilder(uri) { Scheme = site.Scheme, Host = site.Host, Port = site.IsDefaultPort ? -1 : site.Port };
        return builder.Uri;
    }

    /// <summary>Key for visited sets.</summary>
    public static string Key(Uri uri) => uri.AbsoluteUri;

    /// <summary>Lower-case extension of the last path segment without the dot, or empty.</summary>
    public static string Extension(Uri uri)
    {
        var path = uri.AbsolutePath;
        var slash = path.LastIndexOf('/');
        var dot = path.LastIndexOf('.');
        return dot > slash && dot < path.Length - 1 ? path[(dot + 1)..].ToLowerInvariant() : "";
    }

    /// <summary>Decoded last path segment.</summary>
    public static string FileName(Uri uri)
    {
        var path = uri.AbsolutePath.TrimEnd('/');
        var slash = path.LastIndexOf('/');
        return Uri.UnescapeDataString(slash >= 0 ? path[(slash + 1)..] : path);
    }

    private static string StripWww(string host) =>
        host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;

    private static string RemoveTrackingParameters(string query)
    {
        if (string.IsNullOrEmpty(query) || query == "?")
        {
            return "";
        }

        var kept = new StringBuilder();
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = pair.Split('=', 2)[0];
            if (name.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) || TrackingParameters.Contains(name))
            {
                continue;
            }

            kept.Append(kept.Length == 0 ? "" : "&").Append(pair);
        }

        return kept.ToString();
    }
}
