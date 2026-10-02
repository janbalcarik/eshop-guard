using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EshopGuard.Api.Tests;

/// <summary>
/// The OpenAPI document <c>v1</c> against <c>Snapshots/openapi-v1.json</c> (change 9, task 1.7): a change of a DTO or of the
/// codes of an endpoint without updating the snapshot fails with the difference. <c>ESHOPGUARD_UPDATE_SNAPSHOTS=1</c> writes it.
/// </summary>
public sealed class OpenApiSnapshotTests : ApiTestBase
{
    private static readonly string SnapshotPath = Path.Combine(RepositoryRoot.Find(), "src", "tests", "EshopGuard.Api.Tests", "Snapshots", "openapi-v1.json");

    [Fact]
    public async Task Document_MatchesTheSnapshot_AndCarriesTheProblemCodes()
    {
        await using var factory = Factory();
        using var browser = factory.CreateApiClient();
        using var response = await browser.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = Normalize(JsonNode.Parse(await response.Content.ReadAsStringAsync(Ct))!);

        var codes = document["paths"]!["/api/auth/login-link"]!["post"]!["x-problem-codes"]!.AsArray().Select(c => (string)c!).ToList();
        Assert.Contains("email.send_failed", codes);
        Assert.Contains("rate_limited", codes);
        Assert.Contains("validation.failed", codes);
        Assert.Contains("csrf.invalid", codes);

        var text = document.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
        if (Environment.GetEnvironmentVariable("ESHOPGUARD_UPDATE_SNAPSHOTS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SnapshotPath)!);
            await File.WriteAllTextAsync(SnapshotPath, text, Ct);
        }

        Assert.True(File.Exists(SnapshotPath), "Snapshot missing: run with ESHOPGUARD_UPDATE_SNAPSHOTS=1 and review it.");
        var expected = (await File.ReadAllTextAsync(SnapshotPath, Ct)).Replace("\r\n", "\n", StringComparison.Ordinal);
        if (expected != text)
        {
            var expectedLines = expected.Split('\n');
            var actualLines = text.Split('\n');
            var first = Enumerable.Range(0, Math.Min(expectedLines.Length, actualLines.Length)).FirstOrDefault(i => expectedLines[i] != actualLines[i], Math.Min(expectedLines.Length, actualLines.Length));
            Assert.Fail($"OpenAPI differs from the snapshot at line {first + 1}:\n- {expectedLines.ElementAtOrDefault(first)}\n+ {actualLines.ElementAtOrDefault(first)}");
        }
    }

    /// <summary>Objects with their keys sorted, so only real changes count.</summary>
    private static JsonNode Normalize(JsonNode node) => node switch
    {
        JsonObject o => new JsonObject(o.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, p.Value is null ? null : Normalize(p.Value)))),
        JsonArray a => new JsonArray(a.Select(i => i is null ? null : Normalize(i)).ToArray()),
        _ => node.DeepClone(),
    };
}
