namespace EshopGuard.Core.Crawl;

/// <summary>
/// Cookies of one crawl scope (a site or a language version): names and values from <c>Set-Cookie</c>, sent back in the
/// <c>Cookie</c> header. The HTTP client keeps no cookies of its own (<c>UseCookies = false</c>), so no cookie ever passes
/// from one site, version or tenant to another. Path, domain and expiry are not tracked: a scope is one site.
/// </summary>
internal static class CookieJar
{
    /// <summary>Stores the cookies of the answer; a cookie with <c>Max-Age=0</c> or an expiry in the past is removed.</summary>
    public static void Apply(IDictionary<string, string> jar, IEnumerable<string> setCookies, DateTimeOffset now)
    {
        foreach (var header in setCookies)
        {
            var parts = header.Split(';');
            var pair = parts[0].Split('=', 2);
            var name = pair[0].Trim();
            if (name.Length == 0)
            {
                continue;
            }

            var expired = parts.Skip(1).Select(p => p.Split('=', 2)).Any(a =>
                (a[0].Trim().Equals("max-age", StringComparison.OrdinalIgnoreCase) && a.Length > 1 && int.TryParse(a[1].Trim(), out var age) && age <= 0)
                || (a[0].Trim().Equals("expires", StringComparison.OrdinalIgnoreCase) && a.Length > 1
                    && DateTimeOffset.TryParse(a[1].Trim(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var expires)
                    && expires < now));
            if (expired)
            {
                jar.Remove(name);
            }
            else
            {
                jar[name] = pair.Length > 1 ? pair[1].Trim() : "";
            }
        }
    }

    /// <summary>The value of the <c>Cookie</c> header, or null without cookies.</summary>
    public static string? Header(IReadOnlyDictionary<string, string>? cookies) =>
        cookies is null || cookies.Count == 0 ? null : string.Join("; ", cookies.Select(c => $"{c.Key}={c.Value}"));
}
