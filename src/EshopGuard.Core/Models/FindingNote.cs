using System.Text.Json;
using System.Text.Json.Serialization;

namespace EshopGuard.Core.Models;

/// <summary>
/// A note of the tool on a verdict as a code with parameters; the sentence is composed in the language of the reader from
/// <c>rules/texts/&lt;locale&gt;/_engine.yaml</c>. A parameter is a text, a number, a date, a list of texts, another note
/// (a part of the sentence that is there only sometimes) or null (that part is left out).
/// </summary>
public sealed record FindingNote(string Code, IReadOnlyDictionary<string, object?> Params)
{
    /// <summary>Equal when the code and the parameters are the same, also after a round trip through JSON.</summary>
    public bool Equals(FindingNote? other) =>
        other is not null && Code == other.Code && NoteParams.Canonical(Params) == NoteParams.Canonical(other.Params);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Code, NoteParams.Canonical(Params));
}

/// <summary>
/// A warning of a run (an unreadable sitemap, pages whose text was not loaded…) as a code with parameters, composed like
/// <see cref="FindingNote"/>.
/// </summary>
public sealed record ScanWarning(string Code, IReadOnlyDictionary<string, object?> Params)
{
    /// <summary>Equal when the code and the parameters are the same.</summary>
    public bool Equals(ScanWarning? other) =>
        other is not null && Code == other.Code && NoteParams.Canonical(Params) == NoteParams.Canonical(other.Params);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Code, NoteParams.Canonical(Params));
}

/// <summary>Builds and compares the parameters of notes and warnings.</summary>
public static class NoteParams
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>No parameters.</summary>
    public static IReadOnlyDictionary<string, object?> None { get; } = new Dictionary<string, object?>();

    /// <summary>Parameters from pairs of name and value.</summary>
    public static IReadOnlyDictionary<string, object?> Of(params (string Name, object? Value)[] values) =>
        values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal);

    /// <summary>JSON of the parameters with names in order; the same before and after a round trip through JSON.</summary>
    public static string Canonical(IReadOnlyDictionary<string, object?> values) =>
        JsonSerializer.Serialize(new SortedDictionary<string, object?>(values.ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal), Json);
}
