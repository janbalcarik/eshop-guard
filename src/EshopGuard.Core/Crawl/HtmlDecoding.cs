using System.Text;
using System.Text.RegularExpressions;

namespace EshopGuard.Core.Crawl;

/// <summary>
/// Turns response bytes into text: charset from Content-Type, then BOM, then meta charset, then UTF-8.
/// Legacy Czech and Slovak sites still use windows-1250 or iso-8859-2.
/// </summary>
internal static partial class HtmlDecoding
{
    static HtmlDecoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static string Decode(byte[] body, string? charset)
    {
        var encoding = GetEncoding(charset) ?? GetEncoding(SniffMetaCharset(body)) ?? Encoding.UTF8;
        using var reader = new StreamReader(new MemoryStream(body), encoding, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static Encoding? GetEncoding(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        try
        {
            return Encoding.GetEncoding(name.Trim().Trim('"', '\''));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? SniffMetaCharset(byte[] body)
    {
        var head = Encoding.Latin1.GetString(body, 0, Math.Min(body.Length, 4096));
        var match = MetaCharset().Match(head);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex("""<meta[^>]+charset\s*=\s*["']?\s*([A-Za-z0-9_\-:]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex MetaCharset();
}
