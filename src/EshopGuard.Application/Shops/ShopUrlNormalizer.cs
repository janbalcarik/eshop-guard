using System.Globalization;
using System.Net;
using EshopGuard.Application.Problems;

namespace EshopGuard.Application.Shops;

/// <summary>The address of an e-shop after the check: domain (identity), base path and the address to download.</summary>
/// <param name="Domain">Host in lower case and punycode without <c>www.</c> (with the port only for an allowed development host).</param>
/// <param name="BasePath"><c>/</c> or <c>/path/</c>, without query and fragment.</param>
/// <param name="BaseUrl">Scheme, host as given (punycode) and the base path.</param>
public sealed record ShopAddress(string Domain, string BasePath, string BaseUrl);

/// <summary>
/// The check of the address of an e-shop before anything is stored (change 10, AD 1): only <c>http</c> and <c>https</c>,
/// port 80 or 443, no user name or password, no IP address (v4 or v6), no internal name (<c>localhost</c>, a name without
/// a dot, <c>.local</c>, <c>.internal</c>, <c>.lan</c>, <c>.home.arpa</c>), an IDN as punycode, at most 2 048 characters.
/// The API never downloads the address; the resolution of the name and the check of the target IP (also after redirects)
/// is the fetcher's in the worker (change 5). Allowed development hosts pass only in Development and Testing.
/// </summary>
public static class ShopUrlNormalizer
{
    public const int MaxLength = 2048;

    private static readonly string[] InternalSuffixes = [".local", ".internal", ".lan", ".home.arpa", ".localhost", ".localdomain"];

    /// <summary>The checked address, or <see cref="DomainException"/> <c>shop.url_invalid</c> / <c>shop.url_not_allowed</c> (400).</summary>
    public static ShopAddress Normalize(string? input, IReadOnlyCollection<string>? allowedDevHosts = null, string invalidCode = ProblemCodes.ShopUrlInvalid,
        string notAllowedCode = ProblemCodes.ShopUrlNotAllowed)
    {
        var text = input?.Trim() ?? "";
        if (text.Length == 0 || text.Length > MaxLength)
        {
            throw new DomainException(invalidCode, 400);
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            // „bylinkovo.sk“ typed without a scheme: https.
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            throw new DomainException(invalidCode, 400);
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            throw new DomainException(notAllowedCode, 400);
        }

        if (string.IsNullOrEmpty(uri.Host))
        {
            throw new DomainException(invalidCode, 400);
        }

        string host;
        try
        {
            host = uri.HostNameType == UriHostNameType.Dns ? new IdnMapping().GetAscii(uri.IdnHost.TrimEnd('.')).ToLowerInvariant() : uri.Host.ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            throw new DomainException(invalidCode, 400);
        }

        var hostWithPort = uri.IsDefaultPort ? host : $"{host}:{uri.Port}";
        var devHost = allowedDevHosts?.Contains(hostWithPort, StringComparer.OrdinalIgnoreCase) == true;
        if (!devHost)
        {
            if (!string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort || IsAddress(uri, host) || IsInternal(host))
            {
                throw new DomainException(notAllowedCode, 400);
            }
        }
        else if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new DomainException(notAllowedCode, 400);
        }

        var domain = host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
        if (devHost && !uri.IsDefaultPort)
        {
            domain = $"{domain}:{uri.Port}";
        }

        var basePath = BasePath(uri.AbsolutePath);
        var baseUrl = $"{uri.Scheme}://{hostWithPort}{basePath}";
        return new ShopAddress(domain, basePath, baseUrl);
    }

    /// <summary>
    /// <c>/</c> or <c>/path/</c>: a last segment with a dot (<c>index.php</c>) is a file, not a folder of the e-shop. The
    /// path keeps its case (paths on the web are case sensitive), percent escapes as in the address.
    /// </summary>
    private static string BasePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return "/";
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (segments.Count > 0 && segments[^1].Contains('.', StringComparison.Ordinal))
        {
            segments.RemoveAt(segments.Count - 1);
        }

        return segments.Count == 0 ? "/" : "/" + string.Join('/', segments) + "/";
    }

    private static bool IsAddress(Uri uri, string host) =>
        uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
        || IPAddress.TryParse(host.Trim('[', ']'), out _)
        // The last label all digits or hexadecimal (0x7f.1): some resolvers read it as an address.
        || host.Split('.')[^1] is var tld && (tld.All(char.IsAsciiDigit) || tld.StartsWith("0x", StringComparison.Ordinal));

    private static bool IsInternal(string host) =>
        host == "localhost" || !host.Contains('.', StringComparison.Ordinal) || InternalSuffixes.Any(s => host.EndsWith(s, StringComparison.Ordinal));
}
