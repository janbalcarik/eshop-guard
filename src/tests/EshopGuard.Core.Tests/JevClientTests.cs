using System.Net;
using System.Text;
using System.Text.Json;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Rules;
using Microsoft.Extensions.Logging.Abstractions;

namespace EshopGuard.Core.Tests;

/// <summary>
/// The Jev client against a stubbed HTTP handler: request shape, retries and error handling.
/// </summary>
public class JevClientTests
{
    private const string Success = """
        {"id":"gen-1","provider":"TypeSafe","model":"jev-1.13.0",
         "answers":{"eco_claim":{"type":"noul","noul":0.97},"eco_generic":{"type":"noul","noul":0.91}},
         "usage":{"input_tokens":180,"output_tokens":10,"cost":0.0000076}}
        """;

    private static readonly Dictionary<string, JevQuestion> Questions = new()
    {
        ["eco_claim"] = new() { Type = "noul", Instructions = "Does the sentence claim …?" },
        ["eco_generic"] = new() { Type = "noul", Instructions = "Does the sentence use …?" },
    };

    [Fact]
    public async Task SendsModelStateAndTypedQuestionsWithBearerKey()
    {
        var handler = new StubHandler((_, _) => Json(HttpStatusCode.OK, Success));
        var client = CreateClient(handler);

        var result = await client.EvaluateAsync(
            new SentenceState("Naše kosmetika je ekologická.", "Vyrábíme ručně.", ""), Questions, TestContext.Current.CancellationToken);

        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://jev.example/v1/systemone", request.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer test-key", request.Headers.Authorization!.ToString());

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal("jev-1.13.0", root.GetProperty("model").GetString());
        Assert.Equal("Naše kosmetika je ekologická.", root.GetProperty("state").GetProperty("sentence").GetString());
        Assert.Equal("Vyrábíme ručně.", root.GetProperty("state").GetProperty("context_before").GetString());
        var claim = root.GetProperty("questions").GetProperty("eco_claim");
        Assert.Equal("noul", claim.GetProperty("type").GetString());
        Assert.False(claim.TryGetProperty("criteria", out _));

        Assert.Equal(0.97, result.Answers["eco_claim"].Noul);
        Assert.Equal(180, result.Usage.InputTokens);
        Assert.Equal("jev-1.13.0", result.Model);
    }

    [Fact]
    public async Task RetriesAfterRateLimitAndHonoursRetryAfterMs()
    {
        var handler = new StubHandler((_, attempt) => attempt == 1
            ? Json((HttpStatusCode)429, """{"error":"rate limited"}""", ("retry-after-ms", "1"))
            : Json(HttpStatusCode.OK, Success));

        var result = await CreateClient(handler).EvaluateAsync("text", Questions, TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(0.91, result.Answers["eco_generic"].Noul);
    }

    [Fact]
    public async Task GivesUpAfterMaxAttemptsOnOverload()
    {
        var handler = new StubHandler((_, _) => Json((HttpStatusCode)529, """{"error":"overloaded"}"""));

        var error = await Assert.ThrowsAsync<JevApiException>(
            () => CreateClient(handler, maxRetries: 3).EvaluateAsync("text", Questions, TestContext.Current.CancellationToken));

        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(529, error.StatusCode);
        Assert.False(error.IsFatal);
    }

    [Fact]
    public async Task ValidationErrorIsNotRetried()
    {
        var handler = new StubHandler((_, _) => Json((HttpStatusCode)422, """{"detail":"questions.eco_claim.type"}"""));

        var error = await Assert.ThrowsAsync<JevApiException>(
            () => CreateClient(handler).EvaluateAsync("text", Questions, TestContext.Current.CancellationToken));

        Assert.Single(handler.Requests);
        Assert.Equal(422, error.StatusCode);
        Assert.Contains("questions.eco_claim.type", error.ResponseBody);
        Assert.False(error.IsFatal);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(402)]
    public async Task RejectedKeyOrMissingCreditIsFatal(int status)
    {
        var handler = new StubHandler((_, _) => Json((HttpStatusCode)status, """{"error":"no"}"""));

        var error = await Assert.ThrowsAsync<JevApiException>(
            () => CreateClient(handler).EvaluateAsync("text", Questions, TestContext.Current.CancellationToken));

        Assert.True(error.IsFatal);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task MissingKeyFailsWithoutRequest()
    {
        var handler = new StubHandler((_, _) => Json(HttpStatusCode.OK, Success));

        var error = await Assert.ThrowsAsync<JevApiException>(
            () => CreateClient(handler, apiKey: " ").EvaluateAsync("text", Questions, TestContext.Current.CancellationToken));

        Assert.True(error.IsFatal);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task MissingAnswerIsAnError()
    {
        var handler = new StubHandler((_, _) => Json(HttpStatusCode.OK,
            """{"model":"jev-1.13.0","answers":{"eco_claim":{"type":"noul","noul":0.5}},"usage":{"input_tokens":10,"output_tokens":1}}"""));

        var error = await Assert.ThrowsAsync<JevApiException>(
            () => CreateClient(handler).EvaluateAsync("text", Questions, TestContext.Current.CancellationToken));

        Assert.Contains("eco_generic", error.Message);
    }

    private static JevClient CreateClient(StubHandler handler, string apiKey = "test-key", int maxRetries = 6) =>
        new(new StubHttpClientFactory(handler),
            Microsoft.Extensions.Options.Options.Create(new JevOptions
            {
                ApiKey = apiKey,
                BaseUrl = new Uri("https://jev.example/v1/systemone"),
                MaxRetries = maxRetries,
                RetryBaseDelayMilliseconds = 1,
            }),
            NullLogger<JevClient>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode status, string body, params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        foreach (var (name, value) in headers)
        {
            response.Headers.TryAddWithoutValidation(name, value);
        }

        return response;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request, body));
            return respond(request, Requests.Count);
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
