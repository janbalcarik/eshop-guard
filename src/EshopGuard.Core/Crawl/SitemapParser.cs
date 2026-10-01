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

    public sealed record Result(bool IsIndex, IReadOnlyList<string> Locations);

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
            .Select(e => e.Value.Trim())
            .Where(v => v.Length > 0)
            .ToList();
        return new Result(isIndex, locations);
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
