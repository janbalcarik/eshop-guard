using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using EshopGuard.Core.Storage;
using EshopGuard.Storage;

namespace EshopGuard.Jobs.Runs.Storage;

/// <summary>
/// HTML and extractions of the pages of a run in the file store (<see cref="IBlobStore"/>), under
/// <c>tenants/{tenant}/shops/{shop}/runs/{run}/pages/{hash}.html.gz</c> and <c>….extract.json.gz</c>, where the hash is of
/// the final URL. The tenant, e-shop and run are those of the running job (<see cref="RunAmbient"/>); the versions of the
/// pages (<c>content.page_versions.html_blob_key</c>) point to these files, so a later run never overwrites them.
/// </summary>
public sealed class BlobPageContentStore(IBlobStore blobs) : IPageContentStore
{
    public async Task PutHtmlAsync(PageContentKey key, byte[] gzipHtml, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(gzipHtml);
        await using var stream = new MemoryStream(gzipHtml, writable: false);
        await blobs.PutAsync(HtmlKey(RunAmbient.Required, key.Url), stream, "application/gzip", ct).ConfigureAwait(false);
    }

    public async Task<byte[]?> GetHtmlAsync(PageContentKey key, CancellationToken ct = default)
    {
        await using var stream = await blobs.OpenReadAsync(HtmlKey(RunAmbient.Required, key.Url), ct).ConfigureAwait(false);
        if (stream is null)
        {
            return null;
        }

        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, ct).ConfigureAwait(false);
        return copy.ToArray();
    }

    public async Task PutExtractAsync(PageContentKey key, string extractJson, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(extractJson);
        await RunFiles.PutTextAsync(blobs, ExtractKey(RunAmbient.Required, key.Url), extractJson, ct).ConfigureAwait(false);
    }

    public Task<string?> GetExtractAsync(PageContentKey key, CancellationToken ct = default) =>
        RunFiles.GetTextAsync(blobs, ExtractKey(RunAmbient.Required, key.Url), ct);

    /// <summary>Key of the HTML of a page of the run.</summary>
    public static BlobKey HtmlKey(RunAmbientScope scope, string url) =>
        BlobKey.ForShop(scope.TenantId, scope.ShopId, "runs", scope.RunId.ToString("D"), "pages", Hash(url) + ".html.gz");

    /// <summary>Key of the extraction of a page of the run.</summary>
    public static BlobKey ExtractKey(RunAmbientScope scope, string url) =>
        BlobKey.ForShop(scope.TenantId, scope.ShopId, "runs", scope.RunId.ToString("D"), "pages", Hash(url) + ".extract.json.gz");

    private static string Hash(string url) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..40];
}

/// <summary>
/// Work files of a run in the file store, gzip-compressed JSON under <c>tenants/{t}/shops/{s}/runs/{r}/{folder}/{name}</c>:
/// <c>discovery</c> (robots.txt and sitemaps as the run read them), <c>work</c> (inputs and outputs of the batches of Jev,
/// segments, profiles) and the result. Writing the same file again replaces it, so a repeated batch is harmless.
/// </summary>
internal static class RunFiles
{
    public const string Discovery = "discovery";
    public const string Work = "work";

    public static BlobKey Key(RunAmbientScope scope, string folder, string name) =>
        BlobKey.ForShop(scope.TenantId, scope.ShopId, "runs", scope.RunId.ToString("D"), folder, name);

    public static async Task PutTextAsync(IBlobStore blobs, BlobKey key, string text, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await gzip.WriteAsync(bytes, ct).ConfigureAwait(false);
        }

        buffer.Position = 0;
        await blobs.PutAsync(key, buffer, "application/gzip", ct).ConfigureAwait(false);
    }

    public static async Task<string?> GetTextAsync(IBlobStore blobs, BlobKey key, CancellationToken ct)
    {
        await using var stream = await blobs.OpenReadAsync(key, ct).ConfigureAwait(false);
        if (stream is null)
        {
            return null;
        }

        await using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return await reader.ReadToEndAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Writes a record of the pipeline (<c>PipelineJson</c>, the same form as the CLI's).</summary>
    public static Task PutAsync<T>(IBlobStore blobs, RunAmbientScope scope, string folder, string name, T value, CancellationToken ct) =>
        PutTextAsync(blobs, Key(scope, folder, name + ".json.gz"), Core.Pipeline.PipelineJson.Serialize(value), ct);

    /// <summary>Reads a value written by <see cref="PutAsync"/>; null when the file does not exist.</summary>
    public static async Task<T?> GetAsync<T>(IBlobStore blobs, RunAmbientScope scope, string folder, string name, CancellationToken ct)
        where T : class
    {
        var text = await GetTextAsync(blobs, Key(scope, folder, name + ".json.gz"), ct).ConfigureAwait(false);
        return text is null ? null : System.Text.Json.JsonSerializer.Deserialize<T>(text, Core.Pipeline.PipelineJson.Options);
    }

    /// <summary>Reads a value that an earlier step of the run must have written.</summary>
    public static async Task<T> RequireAsync<T>(IBlobStore blobs, RunAmbientScope scope, string folder, string name, CancellationToken ct)
        where T : class =>
        await GetAsync<T>(blobs, scope, folder, name, ct).ConfigureAwait(false)
        ?? throw new InvalidOperationException($"Work file {folder}/{name} of run {scope.RunId} is missing.");
}
