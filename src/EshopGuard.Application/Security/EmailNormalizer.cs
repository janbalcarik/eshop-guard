using System.Globalization;

namespace EshopGuard.Application.Security;

/// <summary>
/// One form of an e-mail address for comparison and storage: trimmed, lower case, the domain in punycode (IDN), at most 254
/// characters; <c>null</c> when it is not an address (code <c>email.invalid_format</c>).
/// </summary>
public static class EmailNormalizer
{
    public const int MaxLength = 254;
    private const int MaxLocalLength = 64;
    private const string LocalSpecials = "!#$%&'*+/=?^_`{|}~-.";
    private static readonly IdnMapping Idn = new() { AllowUnassigned = false, UseStd3AsciiRules = true };

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var at = trimmed.LastIndexOf('@');
        if (at <= 0 || at == trimmed.Length - 1 || trimmed.IndexOf('@') != at)
        {
            return null;
        }

        var local = trimmed[..at].ToLowerInvariant();
        string domain;
        try
        {
            domain = Idn.GetAscii(trimmed[(at + 1)..].TrimEnd('.')).ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (local.Length > MaxLocalLength || local.StartsWith('.') || local.EndsWith('.') || local.Contains("..", StringComparison.Ordinal)
            || local.Any(c => !(char.IsAsciiLetterOrDigit(c) || LocalSpecials.Contains(c, StringComparison.Ordinal))))
        {
            return null;
        }

        var labels = domain.Split('.');
        if (labels.Length < 2 || labels.Any(l => l.Length is 0 or > 63 || l.StartsWith('-') || l.EndsWith('-') || l.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-')))
            || labels[^1].Length < 2 || labels[^1].All(char.IsAsciiDigit))
        {
            return null;
        }

        var normalized = local + "@" + domain;
        return normalized.Length > MaxLength ? null : normalized;
    }
}
