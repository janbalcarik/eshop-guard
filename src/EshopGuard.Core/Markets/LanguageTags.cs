namespace EshopGuard.Core.Markets;

/// <summary>
/// Language tags (BCP 47) as shops write them: <c>sk</c>, <c>sk-SK</c>, <c>en_GB</c>. Only the shape of the tag is read;
/// no list of languages is kept in code.
/// </summary>
public static class LanguageTags
{
    /// <summary>Lower case with hyphens (<c>en_GB</c> gives <c>en-gb</c>); empty for nothing.</summary>
    public static string Normalize(string? tag) => (tag ?? "").Trim().Replace('_', '-').ToLowerInvariant();

    /// <summary>The primary subtag (<c>en-gb</c> gives <c>en</c>); empty for nothing.</summary>
    public static string Primary(string? tag)
    {
        var normalized = Normalize(tag);
        var dash = normalized.IndexOf('-');
        return dash > 0 ? normalized[..dash] : normalized;
    }

    /// <summary>The same primary subtag (<c>cs</c> and <c>cs-CZ</c>).</summary>
    public static bool SamePrimary(string? a, string? b)
    {
        var primary = Primary(a);
        return primary.Length > 0 && primary == Primary(b);
    }

    /// <summary>
    /// The shape of a language tag: a primary subtag of two or three letters, optionally a region or script
    /// (<c>sk</c>, <c>en-gb</c>, <c>zh-hant</c>). <c>x-default</c> of hreflang is not a language.
    /// </summary>
    public static bool IsLanguageTag(string? tag)
    {
        var normalized = Normalize(tag);
        var parts = normalized.Split('-');
        return parts[0].Length is 2 or 3
            && parts[0].All(char.IsAsciiLetterLower)
            && parts.Skip(1).All(p => p.Length is >= 2 and <= 8 && p.All(char.IsAsciiLetterOrDigit))
            && parts.Length <= 3;
    }
}
