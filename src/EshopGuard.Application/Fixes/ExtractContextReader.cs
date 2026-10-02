using System.IO.Compression;
using System.Text;
using System.Text.Json;
using EshopGuard.Storage;

namespace EshopGuard.Application.Fixes;

/// <summary>
/// The text of a version of a page as the worker stored it (change 8): the extraction <c>page_versions.extract_blob_key</c>,
/// gzip-compressed JSON of the library, read through <see cref="IBlobStore"/>. Blocks are the non-empty lines of the main
/// text, numbered from 1 like the blocks <c>B1…</c> of a rewrite (<c>fix_proposals.block_index</c>). Nothing of it is copied
/// into the database.
/// </summary>
public sealed class ExtractContextReader(IBlobStore blobs)
{
    /// <summary>
    /// The text of the version, or null when the file is gone. The key must lie under the tenant (and e-shop) of the request:
    /// a key of another tenant is refused, never read.
    /// </summary>
    public async Task<PageText?> ReadAsync(Guid tenantId, Guid shopId, string? extractBlobKey, CancellationToken ct)
    {
        if (Key(tenantId, shopId, extractBlobKey) is not { } key)
        {
            return null;
        }

        await using var stream = await blobs.OpenReadAsync(key, ct).ConfigureAwait(false);
        if (stream is null)
        {
            return null;
        }

        await using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var document = await JsonDocument.ParseAsync(gzip, cancellationToken: ct).ConfigureAwait(false);
        var info = document.RootElement.TryGetProperty("info", out var i) ? i : default;
        return new PageText(
            Text(info, "title"),
            Text(info, "meta_description"),
            Text(info, "json_ld_description"),
            Blocks(Text(info, "main_text") ?? ""));
    }

    /// <summary>The blocks of a main text: its non-empty lines, trimmed (as <c>PageRewriter</c> numbers them).</summary>
    public static IReadOnlyList<string> Blocks(string mainText) =>
        mainText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>The key of a stored extraction when it lies under the tenant and e-shop; otherwise null.</summary>
    public static BlobKey? Key(Guid tenantId, Guid shopId, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var segments = value.Split('/');
        if (segments.Length < 5 || segments[0] != "tenants" || segments[1] != tenantId.ToString("D") || segments[2] != "shops" || segments[3] != shopId.ToString("D"))
        {
            return null;
        }

        try
        {
            return BlobKey.ForShop(tenantId, shopId, segments[4..]);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Writes an extraction in the shape the worker stores (tests and tools).</summary>
    public static byte[] Compress(string title, string mainText, string? metaDescription = null, string? jsonLdDescription = null)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["schema_version"] = 1,
            ["info"] = new Dictionary<string, object?>
            {
                ["title"] = title,
                ["meta_description"] = metaDescription,
                ["json_ld_description"] = jsonLdDescription,
                ["main_text"] = mainText,
            },
        });
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(Encoding.UTF8.GetBytes(json));
        }

        return buffer.ToArray();
    }
}

/// <summary>The readable text of a page version: title, descriptions and the blocks of the main text (1 = the first).</summary>
public sealed record PageText(string? Title, string? MetaDescription, string? JsonLdDescription, IReadOnlyList<string> Blocks)
{
    /// <summary>The block with a number (1-based), or null.</summary>
    public string? Block(int? index) => index is { } i && i >= 1 && i <= Blocks.Count ? Blocks[i - 1] : null;

    /// <summary>
    /// The number of the block that holds the text: the given block when it holds it, otherwise the first block with the
    /// text (whitespace normalized), otherwise null.
    /// </summary>
    public int? Locate(string? text, int? hint = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return hint is { } h && Block(h) is not null ? h : null;
        }

        var needle = Normalize(text);
        if (hint is { } i && Block(i) is { } block && Normalize(block).Contains(needle, StringComparison.Ordinal))
        {
            return i;
        }

        for (var n = 0; n < Blocks.Count; n++)
        {
            if (Normalize(Blocks[n]).Contains(needle, StringComparison.Ordinal))
            {
                return n + 1;
            }
        }

        return hint is { } fallback && Block(fallback) is not null ? fallback : null;
    }

    /// <summary>The blocks around a block: the one before and the one after (null at the edges).</summary>
    public (string? Before, string? After) Around(int? index) =>
        index is { } i ? (Block(i - 1), Block(i + 1)) : (null, null);

    /// <summary>Whitespace collapsed to single spaces.</summary>
    public static string Normalize(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
