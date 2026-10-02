using System.Collections.Concurrent;
using System.Text;
using EshopGuard.Core.Crawl;

namespace EshopGuard.Jobs.Tests.Shops;

/// <summary>Answers of the tests by address; an address without an answer is 404 (robots.txt then allows all). Records every request.</summary>
internal sealed class ShopPageFetcher : IPageFetcher
{
    private readonly ConcurrentDictionary<string, Func<CancellationToken, Task<FetchResponse>>> _answers = new(StringComparer.Ordinal);

    public ConcurrentQueue<string> Requested { get; } = new();

    public ShopPageFetcher Html(string url, string html, IReadOnlyList<KeyValuePair<string, string>>? headers = null, IReadOnlyList<string>? cookies = null)
    {
        _answers[url] = _ => Task.FromResult(new FetchResponse
        {
            Url = new Uri(url), StatusCode = 200, MediaType = "text/html", Charset = "utf-8", Body = Encoding.UTF8.GetBytes(html),
            Headers = headers ?? [], SetCookies = cookies ?? [],
        });
        return this;
    }

    public ShopPageFetcher Text(string url, string text)
    {
        _answers[url] = _ => Task.FromResult(new FetchResponse
        {
            Url = new Uri(url), StatusCode = 200, MediaType = "text/plain", Charset = "utf-8", Body = Encoding.UTF8.GetBytes(text),
        });
        return this;
    }

    public ShopPageFetcher Redirect(string url, string location)
    {
        _answers[url] = _ => Task.FromResult(new FetchResponse { Url = new Uri(url), StatusCode = 301, RedirectLocation = new Uri(location) });
        return this;
    }

    /// <summary>The address never answers (until the request is canceled).</summary>
    public ShopPageFetcher Hang(string url)
    {
        _answers[url] = async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        };
        return this;
    }

    public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct) => FetchAsync(new FetchRequest(url), ct);

    public Task<FetchResponse> FetchAsync(FetchRequest request, CancellationToken ct)
    {
        var key = request.Url.AbsoluteUri;
        Requested.Enqueue(key);
        return _answers.TryGetValue(key, out var answer)
            ? answer(ct)
            : Task.FromResult(new FetchResponse { Url = request.Url, StatusCode = 404, MediaType = "text/html" });
    }
}
