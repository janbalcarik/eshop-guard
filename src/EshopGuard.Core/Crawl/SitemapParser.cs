using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace EshopGuard.Core.Crawl;

/// <summary>
/// Reads a sitemap or a sitemap index, plain or gzip-compressed.
/// </summary>
internal static class SitemapParser
{
    private const long MaxUncompressedBytes = 100L * 1024 * 1024;

    /// <summary>A <c>loc</c> of the sitemap with its <c>lastmod</c> (null when missing or not a valid W3C date).</summary>
    public sealed record SitemapLocation(string Url, DateTimeOffset? LastModified);

    public sealed record Result(bool IsIndex, IReadOnlyList<SitemapLocation> Entries)
    {
        /// <summary>The <c>loc</c> values in document order.</summary>
        public IReadOnlyList<string> Locations => Entries.Select(e => e.Url).ToList();
    }

    /// <exception cref="XmlException">The content is not valid XML.</exception>
    /// <exception cref="InvalidDataException">The gzip content is corrupt or too large.</exception>
    public static Result Parse(byte[] body)
    {
        var data = IsGzip(body) ? Decompress(body) : body;
        using var stream = new MemoryStream(data);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
        };
        using var reader = XmlReader.Create(stream, settings);
        var document = XDocument.Load(reader);
        var isIndex = document.Root?.Name.LocalName == "sitemapindex";
        var locations = document.Descendants()
            .Where(e => e.Name.LocalName == "loc")
            .Select(e => (Url: e.Value.Trim(), Entry: e.Parent))
            .Where(v => v.Url.Length > 0)
            .Select(v => new SitemapLocation(v.Url, LastModified(v.Entry)))
            .ToList();
        return new Result(isIndex, locations);
    }

    /// <summary><c>lastmod</c> next to the <c>loc</c> (W3C datetime: a date, or a date and time with a zone).</summary>
    private static DateTimeOffset? LastModified(XElement? entry)
    {
        var text = entry?.Elements().FirstOrDefault(e => e.Name.LocalName == "lastmod")?.Value.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value)
            ? value
            : null;
    }

    private static bool IsGzip(byte[] body) => body.Length > 2 && body[0] == 0x1F && body[1] == 0x8B;

    private static byte[] Decompress(byte[] body)
    {
        using var input = new GZipStream(new MemoryStream(body), CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = input.Read(buffer)) > 0)
        {
            if (output.Length + read > MaxUncompressedBytes)
            {
                throw new InvalidDataException("Sitemap is larger than 100 MB after decompression.");
            }

            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }
}
