using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EshopGuard.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Jev;

/// <summary>
/// Thin client of <c>POST /v1/systemone</c>: one state, all questions of a module in one request.
/// Request limit through <see cref="IRateLimiter"/>, exponential backoff with jitter for 408, 429, 529 and 5xx (honouring <c>Retry-After</c>
/// and <c>retry-after-ms</c>), fatal errors for 401, 402 and 403. The key is sent only in the header and never logged.
/// </summary>
internal sealed class JevClient : IJevClient, IDisposable
{
    public const string HttpClientName = "EshopGuard.Jev";

    /// <summary>snake_case field names (<c>input_tokens</c>); dictionary keys (question ids, options) stay unchanged.</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(60);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JevOptions _options;
    private readonly ILogger<JevClient> _logger;
    private readonly IRateLimiter _limiter;
    private readonly LocalRateLimiter? _ownLimiter;

    /// <summary>A client; without an <see cref="IRateLimiter"/> it limits its requests itself like <see cref="LocalRateLimiter"/>.</summary>
    public JevClient(IHttpClientFactory httpClientFactory, IOptions<JevOptions> options, ILogger<JevClient> logger, IRateLimiter? limiter = null)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
        _limiter = limiter ?? (_ownLimiter = new LocalRateLimiter(options));
    }

    public async Task<JevResult> EvaluateAsync(object state, IReadOnlyDictionary<string, JevQuestion> questions, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new JevApiException(
                "Chybí klíč API Jevu. Nastavte JEV_API_KEY v .env nebo proměnnou prostředí TYPESAFE_API_KEY.", 0, null, isFatal: true);
        }

        var body = JsonSerializer.Serialize(new JevRequest(_options.Model, state, questions), Json);
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var maxAttempts = Math.Max(1, _options.MaxRetries);
        for (var attempt = 1; ; attempt++)
        {
            using var lease = await _limiter.AcquireAsync(RateResource.Jev, 1, RequestPriority.P2, ct);
            TimeSpan wait;
            string failure;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
                using var response = await client.SendAsync(request, ct);
                var status = (int)response.StatusCode;
                var text = await response.Content.ReadAsStringAsync(ct);
                if (response.IsSuccessStatusCode)
                {
                    return Parse(text, questions);
                }

                if (status is 401 or 402 or 403)
                {
                    throw new JevApiException(FatalMessage(status), status, text, isFatal: true);
                }

                if (!IsRetryable(status) || attempt >= maxAttempts)
                {
                    if (status == 422)
                    {
                        _logger.LogWarning("Jev rejected the request (422): {Response}. Request body: {Request}", text, body);
                    }

                    throw new JevApiException($"Jev vrátil HTTP {status}.", status, text, isFatal: false);
                }

                wait = RetryAfter(response) ?? Backoff(attempt);
                failure = $"HTTP {status}";
            }
            catch (HttpRequestException ex)
            {
                if (attempt >= maxAttempts)
                {
                    throw new JevApiException($"Jev je nedostupný: {ex.Message}", 0, null, isFatal: false, ex);
                }

                wait = Backoff(attempt);
                failure = ex.Message;
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                if (attempt >= maxAttempts)
                {
                    throw new JevApiException("Jev neodpověděl včas.", 0, null, isFatal: false, ex);
                }

                wait = Backoff(attempt);
                failure = "timeout";
            }

            _logger.LogInformation("Jev request failed ({Failure}), attempt {Attempt} of {Max}, retrying in {Delay} ms",
                failure, attempt, maxAttempts, (int)wait.TotalMilliseconds);
            await Task.Delay(wait, ct);
        }
    }

    public void Dispose() => _ownLimiter?.Dispose();

    private JevResult Parse(string text, IReadOnlyDictionary<string, JevQuestion> questions)
    {
        JevResult? result;
        try
        {
            result = JsonSerializer.Deserialize<JevResult>(text, Json);
        }
        catch (JsonException ex)
        {
            throw new JevApiException("Odpověď Jevu není platný JSON.", 200, text, isFatal: false, ex);
        }

        if (result is null)
        {
            throw new JevApiException("Odpověď Jevu je prázdná.", 200, text, isFatal: false);
        }

        var missing = questions.Keys.Where(id => !result.Answers.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            throw new JevApiException($"V odpovědi Jevu chybí otázky: {string.Join(", ", missing)}.", 200, text, isFatal: false);
        }

        _logger.LogDebug("Jev answered: model {Model}, {InputTokens} input tokens", result.Model, result.Usage.InputTokens);
        return result;
    }

    private TimeSpan Backoff(int attempt)
    {
        var exponential = TimeSpan.FromMilliseconds(Math.Max(1, _options.RetryBaseDelayMilliseconds) * Math.Pow(2, attempt - 1));
        var capped = exponential > MaxBackoff ? MaxBackoff : exponential;
        return capped * (0.5 + Random.Shared.NextDouble() * 0.5);
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("retry-after-ms", out var values)
            && double.TryParse(values.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds))
        {
            return Clamp(TimeSpan.FromMilliseconds(milliseconds));
        }

        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return Clamp(delta);
        }

        return retryAfter?.Date is { } date ? Clamp(date - DateTimeOffset.UtcNow) : null;
    }

    private static TimeSpan Clamp(TimeSpan value) =>
        value < TimeSpan.Zero ? TimeSpan.Zero : value > MaxRetryAfter ? MaxRetryAfter : value;

    private static bool IsRetryable(int status) => status is 408 or 429 or >= 500;

    private static string FatalMessage(int status) => status switch
    {
        401 => "Jev odmítl klíč API (HTTP 401). Zkontrolujte JEV_API_KEY nebo TYPESAFE_API_KEY.",
        402 => "Jev vrátil HTTP 402: na účtu není kredit nebo chybí platba.",
        _ => "Jev odmítl přístup (HTTP 403).",
    };

    private sealed record JevRequest(string Model, object State, IReadOnlyDictionary<string, JevQuestion> Questions);
}
