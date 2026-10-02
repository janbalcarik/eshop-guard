using EshopGuard.Application.Problems;

namespace EshopGuard.Api.Http;

/// <summary>
/// Optimistic concurrency over HTTP (change 11): the version of a row (<c>xmin</c>) goes out as <c>ETag: "123"</c> and comes
/// back in <c>If-Match</c>. A change without <c>If-Match</c> is <c>400 validation.failed</c> (<c>errors.If-Match</c>, see
/// <see cref="Concurrency.Require"/>); a version that does not match is <c>409 concurrency.conflict</c> from the service.
/// </summary>
public static class ETagExtensions
{
    public static string ETag(uint version) => "\"" + version.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"";

    public static void SetETag(this HttpContext context, uint version)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.Headers.ETag = ETag(version);
    }

    /// <summary>
    /// The version from <c>If-Match</c> (also a weak tag), or null when it is missing or unreadable. The service refuses null
    /// only after it found the object, so another tenant's object is <c>404</c> whatever the headers.
    /// </summary>
    public static uint? IfMatch(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var header = context.Request.Headers.IfMatch.ToString().Trim();
        if (header.StartsWith("W/", StringComparison.Ordinal))
        {
            header = header[2..];
        }

        header = header.Trim('"');
        return uint.TryParse(header, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var version) ? version : null;
    }
}
