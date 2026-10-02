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

    public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct) => FetchAsync(new FetchRequest(url), ct);

    public async Task<FetchResponse> FetchAsync(FetchRequest fetch, CancellationToken ct)
    {
        var url = fetch.Url;
        var crawl = options.Value.Crawl;
        var maxBytes = fetch.MaxBytes ?? crawl.MaxPageBytes;
        if (!crawl.AllowPrivateNetwork && !SsrfGuard.IsAllowedUrl(url))
        {
            // Another scheme or port, or a user name in the address: refused before any connection.
            logger.LogWarning("Not downloading {Url}: only http and https on ports 80 and 443 without credentials are allowed", Redact(url));
            return new FetchResponse { Url = url, Error = SsrfGuard.Error };
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (fetch.IfNoneMatch is { } etag && System.Net.Http.Headers.EntityTagHeaderValue.TryParse(etag, out var tag))
            {
                request.Headers.IfNoneMatch.Add(tag);
            }

            if (fetch.IfModifiedSince is { } since)
            {
                request.Headers.IfModifiedSince = since;
            }

            // The cookies and language of the crawl scope (language version); the client keeps no cookies of its own, and a
            // header of the request replaces the default one of the client.
            if (CookieJar.Header(fetch.Cookies) is { } cookie)
            {
                request.Headers.TryAddWithoutValidation("Cookie", cookie);
            }

            if (!string.IsNullOrWhiteSpace(fetch.AcceptLanguage))
            {
                request.Headers.TryAddWithoutValidation("Accept-Language", fetch.AcceptLanguage);
            }

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            var status = (int)response.StatusCode;
            var contentType = response.Content.Headers.ContentType;
            IReadOnlyList<string> setCookies = response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : [];

            if (status == 304)
            {
                // Not modified: before the redirect branch, a 304 has no Location and is not a failure.
                return new FetchResponse
                {
                    Url = url,
                    StatusCode = status,
                    ETag = response.Headers.ETag?.ToString() ?? fetch.IfNoneMatch,
                    LastModified = response.Content.Headers.LastModified ?? fetch.IfModifiedSince,
                    SetCookies = setCookies,
                };
            }

            if (status is >= 300 and < 400)
            {
                var location = response.Headers.Location;
                return new FetchResponse
                {
                    Url = url,
                    StatusCode = status,
                    RedirectLocation = location is null ? null : location.IsAbsoluteUri ? location : new Uri(url, location),
                    SetCookies = setCookies,
                };
            }

            if (status is < 200 or >= 300)
            {
                var retry = response.Headers.RetryAfter;
                var retryAfter = retry?.Delta ?? (retry?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
                return new FetchResponse { Url = url, StatusCode = status, MediaType = contentType?.MediaType, RetryAfter = retryAfter, SetCookies = setCookies };
            }

            if (response.Content.Headers.ContentLength > maxBytes)
            {
                return new FetchResponse { Url = url, StatusCode = status, Error = $"larger than {maxBytes} bytes", TooLarge = true };
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var body = await ReadLimitedAsync(stream, maxBytes, ct);
            if (body is null)
            {
                return new FetchResponse { Url = url, StatusCode = status, Error = $"larger than {maxBytes} bytes", TooLarge = true };
            }

            return new FetchResponse
            {
                Url = url,
                StatusCode = status,
                MediaType = contentType?.MediaType,
                Charset = contentType?.CharSet,
                Body = body,
                ETag = response.Headers.ETag?.ToString(),
                LastModified = response.Content.Headers.LastModified,
                SetCookies = setCookies,
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Timeout downloading {Url}", url);
            return new FetchResponse { Url = url, Error = "timeout" };
        }
        catch (HttpRequestException ex) when (FindBlocked(ex) is not null)
        {
            logger.LogWarning("Not downloading {Url}: its address leads into an internal or local network", url);
            return new FetchResponse { Url = url, Error = SsrfGuard.Error };
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Network error downloading {Url}: {Message}", url, ex.Message);
            return new FetchResponse { Url = url, Error = ex.Message };
        }
    }

    private static SsrfBlockedException? FindBlocked(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            if (ex is SsrfBlockedException blocked)
            {
                return blocked;
            }
        }

        return null;
    }

    /// <summary>The URL without a user name and password, for the log.</summary>
    private static string Redact(Uri url) =>
        string.IsNullOrEmpty(url.UserInfo) ? url.AbsoluteUri : new UriBuilder(url) { UserName = "", Password = "" }.Uri.AbsoluteUri;

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
