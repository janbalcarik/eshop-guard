using System.Text.Json;
using EshopGuard.Application.Contracts;

namespace EshopGuard.Application.Findings;

/// <summary>
/// The strictest verdict of a finding stored in <c>checks.findings.verdicts</c> (change 11, AD 2), in the order of
/// <c>VerdictOrder</c> of the library (change 6): a valid verdict before an upcoming one, then the group (<c>text</c>,
/// <c>assess</c>, <c>verify</c>, anything else), the severity (<c>high</c>, <c>medium</c>, <c>low</c>), the band (high before
/// review) and the jurisdiction. <see cref="Rank"/> is the same number as the SQL function <c>checks.strictness_rank</c>,
/// smaller is stricter.
/// </summary>
public static class VerdictStrictness
{
    /// <summary>Rank of a finding without any verdict (after every real one).</summary>
    public const short None = 127;

    public static short Rank(JsonElement verdict) => (short)(
        (Text(verdict, "status") == "upcoming" ? 1 : 0) * 64
        + Group(Text(verdict, "checkability")) * 16
        + Severity(Text(verdict, "severity")) * 4
        + (Text(verdict, "band") == "high" ? 0 : 1));

    /// <summary>Rank of the strictest verdict of the array (as <c>checks.strictness_rank</c>).</summary>
    public static short Rank(JsonDocument? verdicts) =>
        verdicts is null ? None : Elements(verdicts.RootElement).Select(Rank).DefaultIfEmpty(None).Min();

    /// <summary>The verdicts as returned by the API, the strictest first.</summary>
    public static List<VerdictDto> Verdicts(JsonDocument? verdicts) =>
        verdicts is null
            ? []
            : Elements(verdicts.RootElement)
                .OrderBy(Rank).ThenBy(v => Text(v, "jurisdiction"), StringComparer.Ordinal)
                .Select(ToDto)
                .ToList();

    /// <summary>The strictest verdict, or null without any.</summary>
    public static VerdictDto? Strictest(JsonDocument? verdicts) => Verdicts(verdicts).FirstOrDefault();

    public static VerdictDto ToDto(JsonElement verdict) => new(
        Text(verdict, "jurisdiction"),
        Checkability(Text(verdict, "checkability")),
        Text(verdict, "severity"),
        Text(verdict, "band") == "high" ? "high" : "review",
        verdict.TryGetProperty("legal_refs", out var refs) && refs.ValueKind == JsonValueKind.Array ? refs.Clone() : null,
        Text(verdict, "status") == "upcoming" ? "upcoming" : "finding",
        verdict.TryGetProperty("effective_from", out var from) && from.ValueKind == JsonValueKind.String && DateOnly.TryParse(from.GetString(), out var date) ? date : null);

    /// <summary>The groups of the API: text, assess, verify (a verdict that cannot be checked on the text goes to verify).</summary>
    public static string Checkability(string checkability) => checkability is "text" or "assess" ? checkability : "verify";

    private static IEnumerable<JsonElement> Elements(JsonElement root) =>
        root.ValueKind == JsonValueKind.Array ? root.EnumerateArray() : [];

    private static int Group(string checkability) => checkability switch
    {
        "text" => 0,
        "assess" => 1,
        "verify" => 2,
        _ => 3,
    };

    private static int Severity(string severity) => severity switch
    {
        "high" => 0,
        "medium" => 1,
        "low" => 2,
        _ => 3,
    };

    private static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
}
