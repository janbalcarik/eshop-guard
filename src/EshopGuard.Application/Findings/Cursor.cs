using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using EshopGuard.Application.Problems;

namespace EshopGuard.Application.Findings;

/// <summary>
/// An opaque cursor of a list: the sort key of the last item (base64url JSON). A cursor that does not decode is
/// <c>400 validation.failed</c> with <c>errors.cursor</c>.
/// </summary>
public static class Cursor
{
    public static string Encode(params object?[] key) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(key)));

    public static JsonElement[]? Decode(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
        {
            return null;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor));
            return JsonSerializer.Deserialize<JsonElement[]>(json) ?? throw Invalid();
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            throw Invalid();
        }
    }

    public static DomainException Invalid() =>
        DomainException.Validation(new ValidationResult().Add("cursor", ProblemCodes.Fields.ValueNotAllowed));

    /// <summary>The limit of a page: 1–100, default 25.</summary>
    public static int Limit(int? limit) => limit switch
    {
        null => 25,
        < 1 or > 100 => throw DomainException.Validation(new ValidationResult().Add("limit", ProblemCodes.Fields.ValueNotAllowed)),
        _ => limit.Value,
    };
}
