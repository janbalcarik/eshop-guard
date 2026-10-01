using System.Collections.Concurrent;
using System.Text;
using EshopGuard.Core.Crawl;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Serves the fixture site from disk without network and records every requested URL.
/// </summary>
internal sealed class FileSystemPageFetcher(string root, Uri baseUrl) : IPageFetcher
{
    private static readonly HashSet<string> TextExtensions = [".html", ".xml", ".txt"];

    public ConcurrentQueue<Uri> Requested { get; } = new();

    public static string FixtureRoot => Path.Combine(AppContext.BaseDirectory, "Fixtures", "site");

    public static Uri DefaultBaseUrl { get; } = new("http://fixture.test/");

    public static FileSystemPageFetcher ForFixture() => new(FixtureRoot, DefaultBaseUrl);

    /// <summary>The Slovak fixture e-shop (Fixtures/site-sk) under the same base URL.</summary>
    public static FileSystemPageFetcher ForSlovakFixture() => new(Path.Combine(AppContext.BaseDirectory, "Fixtures", "site-sk"), DefaultBaseUrl);

    /// <summary>Answers carry an ETag of their body, and a request with the same <c>If-None-Match</c> gets 304.</summary>
    public bool UseETags { get; init; }

    /// <summary>Validators of the conditional requests, in order (null for a plain request).</summary>
    public ConcurrentQueue<(Uri Url, string? IfNoneMatch)> Conditional { get; } = new();

    public async Task<FetchResponse> FetchAsync(FetchRequest request, CancellationToken ct)
    {
        Conditional.Enqueue((request.Url, request.IfNoneMatch));
        var response = await FetchAsync(request.Url, ct);
        if (!UseETags || response.Body is null)
        {
            return response;
        }

        var etag = "\"" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(response.Body))[..16] + "\"";
        return request.IfNoneMatch == etag
            ? new FetchResponse { Url = request.Url, StatusCode = 304, ETag = etag }
            : new FetchResponse
            {
                Url = response.Url,
                StatusCode = response.StatusCode,
                MediaType = response.MediaType,
                Charset = response.Charset,
                Body = response.Body,
                ETag = etag,
            };
    }

    public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct)
    {
        Requested.Enqueue(url);
        var relative = Uri.UnescapeDataString(url.AbsolutePath).TrimStart('/');
        if (relative.Length == 0)
        {
            relative = "index.html";
        }

        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            return Task.FromResult(new FetchResponse { Url = url, StatusCode = 404 });
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();
        var body = File.ReadAllBytes(path);
        if (TextExtensions.Contains(extension))
        {
            var text = Encoding.UTF8.GetString(body).Replace("{{base_url}}", baseUrl.GetLeftPart(UriPartial.Authority));
            body = Encoding.UTF8.GetBytes(text);
        }

        return Task.FromResult(new FetchResponse
        {
            Url = url,
            StatusCode = 200,
            MediaType = extension switch
            {
                ".html" => "text/html",
                ".xml" => "application/xml",
                ".txt" => "text/plain",
                _ => "application/octet-stream",
            },
            Charset = "utf-8",
            Body = body,
        });
    }
}
