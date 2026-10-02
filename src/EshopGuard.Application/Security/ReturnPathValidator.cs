namespace EshopGuard.Application.Security;

/// <summary>
/// A path of the application to return to after Google (AD 9): starts with <c>/</c>, not <c>//</c> or <c>/\</c>, no scheme
/// (no <c>:</c> before the query), no backslash or control character, at most 512 characters. Anything else could be an open
/// redirect (<c>return_path.invalid</c>).
/// </summary>
public static class ReturnPathValidator
{
    public const int MaxLength = 512;

    public static bool IsValid(string? path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > MaxLength || path[0] != '/')
        {
            return false;
        }

        if ((path.Length > 1 && path[1] is '/' or '\\') || path.Any(c => char.IsControl(c) || c == '\\'))
        {
            return false;
        }

        var end = path.IndexOfAny(['?', '#']);
        var pathOnly = end < 0 ? path : path[..end];
        return !pathOnly.Contains(':', StringComparison.Ordinal) && Uri.TryCreate(path, UriKind.Relative, out _);
    }
}
