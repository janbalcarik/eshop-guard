using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;

namespace EshopGuard.Billing.Texts;

/// <summary>
/// Texts of billing that leave the application (the sentence of Checkout, the items of an invoice), by the language of the
/// tenant (<c>Texts/{sk,cs}.json</c>, embedded). Placeholders <c>{name}</c> get codes, numbers and dates only. A language
/// without a file falls back to <c>sk</c>; a missing key is an error (fail-closed, never an empty text).
/// </summary>
public static class BillingTexts
{
    private static readonly FrozenDictionary<string, FrozenDictionary<string, string>> Languages = Load();

    public static IReadOnlyCollection<string> Locales => Languages.Keys;

    public static string Format(string locale, string key, params (string Name, string Value)[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var texts = Languages.GetValueOrDefault(Language(locale)) ?? Languages["sk"];
        var text = texts.TryGetValue(key, out var template) ? template : throw new KeyNotFoundException($"billing text {key}");
        foreach (var (name, value) in values)
        {
            text = text.Replace("{" + name + "}", value, StringComparison.Ordinal);
        }

        return text;
    }

    public static IReadOnlyCollection<string> Keys(string locale) => Languages[locale].Keys;

    /// <summary>An amount as people write it: <c>59 €</c>, <c>1 490 Kč</c>, <c>19,90 €</c> (two places only when there are cents).</summary>
    public static string Money(decimal amount, string currency, string locale)
    {
        var culture = Culture(locale);
        var number = amount == decimal.Truncate(amount) ? amount.ToString("#,0", culture) : amount.ToString("#,0.00", culture);
        number = number.Replace(culture.NumberFormat.NumberGroupSeparator, " ", StringComparison.Ordinal);
        return currency.ToUpperInvariant() switch
        {
            "EUR" => number + " €",
            "CZK" => number + " Kč",
            var other => number + " " + other,
        };
    }

    /// <summary>A date as <c>28. 2. 2027</c>.</summary>
    public static string Date(DateTimeOffset at, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(at, zone);
        return string.Create(CultureInfo.InvariantCulture, $"{local.Day}. {local.Month}. {local.Year}");
    }

    private static string Language(string locale) => (locale ?? "sk").Split('-')[0].ToLowerInvariant();

    private static CultureInfo Culture(string locale) => Language(locale) == "cs" ? CultureInfo.GetCultureInfo("cs-CZ") : CultureInfo.GetCultureInfo("sk-SK");

    private static FrozenDictionary<string, FrozenDictionary<string, string>> Load()
    {
        var assembly = typeof(BillingTexts).Assembly;
        var languages = new Dictionary<string, FrozenDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.Contains(".Texts.", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            var texts = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
            var language = name[..^".json".Length].Split('.')[^1];
            languages[language] = texts.ToFrozenDictionary(StringComparer.Ordinal);
        }

        return languages.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
