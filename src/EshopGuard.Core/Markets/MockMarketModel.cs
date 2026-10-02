using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Core.Rules;

namespace EshopGuard.Core.Markets;

/// <summary>
/// A fake model without network (<c>--mock</c>, tests). Answers given to it (by kind of call, e.g. from the test files of a
/// fixture shop) are returned as they are. Without them it picks the first footer links, finds no country (the result is then
/// only the technical signs and the home country from the domain to confirm) and labels every text with the language the
/// version declares. Its answers are never shown as findings about a real shop: the result carries <c>model_mock</c>.
/// </summary>
public sealed class MockMarketModel(IReadOnlyDictionary<MarketCall, string>? answers = null) : IMarketModel
{
    /// <summary>Marks the input of the language of texts with the declared language, which the fake model returns.</summary>
    internal const string DeclaredLanguagePrefix = "DECLARED LANGUAGE: ";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public string? UnavailableReason => EngineCodes.ModelMock;

    public Task<MarketModelResponse> AskAsync(MarketModelRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var json = answers is not null && answers.TryGetValue(request.Call, out var given) ? given : request.Call switch
        {
            MarketCall.Pick => Pick(request.Input),
            MarketCall.Sales => """{"home_country":"","home_evidence":[],"countries":[],"eu_wide_delivery":{"stated":false,"quote":"","source":""},"delivery_terms":{"found":false,"quote":"","source":""},"language_versions":[],"uncertain":"mock"}""",
            _ => Label(request.Input),
        };
        var input = (long)Math.Ceiling((request.Instructions.Length + request.Input.Length) / MarketAnalysisEstimate.CharsPerToken);
        return Task.FromResult(new MarketModelResponse(json, "mock", input, 0, 0, 0m));
    }

    /// <summary>The first footer links (the lines after "FOOTER LINKS:"), at most 4.</summary>
    private static string Pick(string input)
    {
        var lines = input.Split('\n');
        var footer = Array.FindIndex(lines, l => l.StartsWith(SalesPageSelector.FooterHeading, StringComparison.Ordinal));
        var urls = lines.Skip(footer < 0 ? 0 : footer + 1)
            .Select(l => l.Split(" | "))
            .Where(p => p.Length == 2 && Uri.IsWellFormedUriString(p[1].Trim(), UriKind.Absolute))
            .Select(p => p[1].Trim())
            .Distinct()
            .Take(4)
            .ToList();
        return JsonSerializer.Serialize(new { urls, reason = "mock" }, Json);
    }

    /// <summary>Every text labeled with the declared language of the version (first line of the input).</summary>
    private static string Label(string input)
    {
        var lines = input.Split('\n');
        var declared = lines.FirstOrDefault(l => l.StartsWith(DeclaredLanguagePrefix, StringComparison.Ordinal))?[DeclaredLanguagePrefix.Length..].Trim();
        var labels = new JsonArray();
        foreach (var line in lines)
        {
            var dot = line.IndexOf(". ", StringComparison.Ordinal);
            if (dot > 0 && int.TryParse(line[..dot], out var id))
            {
                labels.Add(new JsonObject { ["id"] = id, ["language"] = string.IsNullOrEmpty(declared) ? "und" : LanguageTags.Primary(declared) });
            }
        }

        return new JsonObject { ["labels"] = labels }.ToJsonString();
    }
}
