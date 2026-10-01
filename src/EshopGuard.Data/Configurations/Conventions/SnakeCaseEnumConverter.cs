using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EshopGuard.Data.Configurations.Conventions;

/// <summary>Stores an enum as <c>snake_case</c> text (<c>KeptWithEvidence</c> ↔ <c>kept_with_evidence</c>).</summary>
public sealed class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(v => ToText(v), v => FromText(v))
    where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> Texts = Enum.GetValues<TEnum>().ToDictionary(v => v, v => SnakeCase.Of(v.ToString()));
    private static readonly Dictionary<string, TEnum> Values = Texts.ToDictionary(p => p.Value, p => p.Key, StringComparer.Ordinal);

    /// <summary>All stored texts in declaration order.</summary>
    public static IReadOnlyList<string> AllTexts { get; } = [.. Enum.GetValues<TEnum>().Select(v => Texts[v])];

    /// <summary>Text of a value.</summary>
    public static string ToText(TEnum value) =>
        Texts.TryGetValue(value, out var text) ? text : throw new ArgumentOutOfRangeException(nameof(value), value, $"Undefined {typeof(TEnum).Name}.");

    /// <summary>Value of a text; unknown text is an error (the database CHECK should prevent it).</summary>
    public static TEnum FromText(string text) =>
        Values.TryGetValue(text, out var value) ? value : throw new InvalidOperationException($"Unknown {typeof(TEnum).Name} value '{text}'.");
}

/// <summary>PascalCase → snake_case.</summary>
public static class SnakeCase
{
    /// <summary>Converts <c>KeptWithEvidence</c> to <c>kept_with_evidence</c>.</summary>
    public static string Of(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
