using System.Security.Cryptography;
using System.Text;

namespace EshopGuard.Core.Languages;

/// <summary>Key of a group of alternates: the same page in several language versions has the same key (<c>pages.hreflang_group</c>).</summary>
public static class HreflangGroups
{
    /// <summary>The first 16 characters of SHA-256 of the sorted distinct URLs of the alternates; null without alternates.</summary>
    public static string? Key(IEnumerable<Uri> alternates)
    {
        var urls = alternates.Select(u => u.AbsoluteUri).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return urls.Count == 0 ? null : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', urls))))[..16];
    }
}
