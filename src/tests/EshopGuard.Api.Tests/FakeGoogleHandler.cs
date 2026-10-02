using System.Net;
using System.Text;
using System.Text.Json;

namespace EshopGuard.Api.Tests;

/// <summary>
/// Google's token and user-info endpoints for the handler of Google (its back channel): any code gives a token, the user
/// info is <see cref="User"/>. No request leaves the test.
/// </summary>
internal sealed class FakeGoogleHandler : HttpMessageHandler
{
    public object User { get; set; } = new { sub = "google-sub", email = "nikto@example.test", email_verified = true, name = "Test" };

    public List<Uri> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        var body = request.RequestUri!.AbsolutePath.Contains("token", StringComparison.Ordinal)
            ? JsonSerializer.Serialize(new { access_token = "fake-access-token", token_type = "Bearer", expires_in = 3600 })
            : JsonSerializer.Serialize(User);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
