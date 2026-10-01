using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EshopGuard.Core;

/// <summary>
/// Text helpers shared by crawling, classification and segmentation.
/// </summary>
internal static partial class TextTools
{
    /// <summary>Collapses all whitespace (including non-breaking spaces) and removes invisible characters.</summary>
    public static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var c in text)
        {
            if (c is '­' or '​' or '‌' or '‍' or '﻿')
            {
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>Normalization used for hashing and deduplication: cleaned whitespace, lower case.</summary>
    public static string NormalizeForHash(string? text) => Clean(text).ToLowerInvariant();

    /// <summary>Removes diacritics, e.g. "Obchodní podmínky" becomes "Obchodni podminky".</summary>
    public static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Lower-case ASCII slug: "Obchodní podmínky | Shop" becomes "obchodni-podminky-shop".</summary>
    public static string Slugify(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var ascii = RemoveDiacritics(text).ToLowerInvariant();
        return NonAlphanumeric().Replace(ascii, "-").Trim('-');
    }

    /// <summary>SHA-256 of the text as lower-case hex prefixed with <c>sha256:</c>.</summary>
    public static string Sha256(string text) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>Snake-case name of an enum value, the same as in JSON output.</summary>
    public static string Snake<TEnum>(TEnum value) where TEnum : struct, Enum =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
