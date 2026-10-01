using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;

namespace EshopGuard.Core.Storage;

/// <summary>Key of the content of one page of a site (the web application adds tenant, shop and version).</summary>
/// <param name="Site">Site key (host without "www.").</param>
/// <param name="Url">Final URL of the page.</param>
public readonly record struct PageContentKey(string Site, string Url);

/// <summary>
/// HTML and extraction of downloaded pages, so that later steps (profiles, rewrites) read them without downloading again.
/// The CLI keeps them in memory, the web application in its file store.
/// </summary>
public interface IPageContentStore
{
    /// <summary>Stores the gzip-compressed HTML (replaces an earlier one).</summary>
    Task PutHtmlAsync(PageContentKey key, byte[] gzipHtml, CancellationToken ct = default);

    /// <summary>The gzip-compressed HTML, or null.</summary>
    Task<byte[]?> GetHtmlAsync(PageContentKey key, CancellationToken ct = default);

    /// <summary>Stores the extraction as JSON (replaces an earlier one).</summary>
    Task PutExtractAsync(PageContentKey key, string extractJson, CancellationToken ct = default);

    /// <summary>The extraction as JSON, or null.</summary>
    Task<string?> GetExtractAsync(PageContentKey key, CancellationToken ct = default);
}

/// <summary>Gzip of page HTML (about a tenth of its size).</summary>
internal static class PageContent
{
    public static byte[] Compress(string html)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest))
        {
            var bytes = Encoding.UTF8.GetBytes(html);
            gzip.Write(bytes, 0, bytes.Length);
        }

        return output.ToArray();
    }

    public static string Decompress(byte[] data)
    {
        using var gzip = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

/// <summary>Page content in memory, for one CLI run.</summary>
internal sealed class InMemoryPageContentStore : IPageContentStore
{
    private readonly ConcurrentDictionary<PageContentKey, byte[]> _html = new();
    private readonly ConcurrentDictionary<PageContentKey, string> _extracts = new();

    public Task PutHtmlAsync(PageContentKey key, byte[] gzipHtml, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(gzipHtml);
        _html[key] = gzipHtml;
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetHtmlAsync(PageContentKey key, CancellationToken ct = default) => Task.FromResult(_html.GetValueOrDefault(key));

    public Task PutExtractAsync(PageContentKey key, string extractJson, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(extractJson);
        _extracts[key] = extractJson;
        return Task.CompletedTask;
    }

    public Task<string?> GetExtractAsync(PageContentKey key, CancellationToken ct = default) => Task.FromResult(_extracts.GetValueOrDefault(key));
}
