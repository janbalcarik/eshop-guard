using EshopGuard.Application.Problems;
using Microsoft.Net.Http.Headers;

namespace EshopGuard.Api.Http;

/// <summary>
/// Optimistic concurrency over HTTP (change 11): the version of a row (<c>xmin</c>) goes out as <c>ETag: "123"</c> and comes
/// back in <c>If-Match</c>. A change without <c>If-Match</c> is <c>400 validation.failed</c> (<c>errors.If-Match</c>); a version
/// that does not match is <c>409 concurrency.conflict</c> from the service.
/// </summary>
public static class ETagExtensions
{
    public static string ETag(uint version) => "\"" + version.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"";

    public static void SetETag(this HttpContext context, uint version)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.Headers.ETag = ETag(version);
    }

    /// <summary>The version from <c>If-Match</c> (also a weak tag); a missing or unreadable header is a validation error.</summary>
    public static uint RequireIfMatch(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var header = context.Request.Headers.IfMatch.ToString().Trim();
        if (header.StartsWith("W/", StringComparison.Ordinal))
        {
            header = header[2..];
        }

        header = header.Trim('"');
        return uint.TryParse(header, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var version)
            ? version
            : throw DomainException.Validation(new ValidationResult().Add(HeaderNames.IfMatch, ProblemCodes.Fields.Required));
    }
}
