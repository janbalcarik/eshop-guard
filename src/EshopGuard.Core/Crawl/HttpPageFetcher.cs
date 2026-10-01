using EshopGuard.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Crawl;

/// <summary>
/// Downloads pages over HTTP with the size limit from <see cref="CrawlOptions"/>. Redirects are returned, not followed.
/// </summary>
internal sealed class HttpPageFetcher(
    IHttpClientFactory httpClientFactory,
    IOptions<EshopGuardOptions> options,
    ILogger<HttpPageFetcher> logger) : IPageFetcher
{
    public const string HttpClientName = "EshopGuard.Crawl";

    public async Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct)
    {
        var maxBytes = options.Value.Crawl.MaxPageBytes;
        var client = httpClientFactory.CreateClient(HttpClientName);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            var status = (int)response.StatusCode;
            var contentType = response.Content.Headers.ContentType;

            if (status is >= 300 and < 400)
            {
                var location = response.Headers.Location;
                return new FetchResponse
                {
                    Url = url,
                    StatusCode = status,
                    RedirectLocation = location is null ? null : location.IsAbsoluteUri ? location : new Uri(url, location),
                };
            }

            if (status is < 200 or >= 300)
            {
                var retry = response.Headers.RetryAfter;
                var retryAfter = retry?.Delta ?? (retry?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
                return new FetchResponse { Url = url, StatusCode = status, MediaType = contentType?.MediaType, RetryAfter = retryAfter };
            }

            if (response.Content.Headers.ContentLength > maxBytes)
            {
                return new FetchResponse { Url = url, StatusCode = status, Error = $"larger than {maxBytes} bytes" };
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var body = await ReadLimitedAsync(stream, maxBytes, ct);
            if (body is null)
            {
                return new FetchResponse { Url = url, StatusCode = status, Error = $"larger than {maxBytes} bytes" };
            }

            return new FetchResponse
            {
                Url = url,
                StatusCode = status,
                MediaType = contentType?.MediaType,
                Charset = contentType?.CharSet,
                Body = body,
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Timeout downloading {Url}", url);
            return new FetchResponse { Url = url, Error = "timeout" };
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Network error downloading {Url}: {Message}", url, ex.Message);
            return new FetchResponse { Url = url, Error = ex.Message };
        }
    }

    private static async Task<byte[]?> ReadLimitedAsync(Stream stream, long maxBytes, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
