using System.Collections.Concurrent;
using System.Globalization;
using YamlDotNet.Serialization;

namespace EshopGuard.Application.Protocols;

/// <summary>
/// The texts of a protocol in one language (section <c>protocol</c> of <c>Protocols/Texts/{locale}.yaml</c>, embedded): labels, sentences with
/// <c>{placeholders}</c>, the plural of the counted nouns (1, 2–4, 0 and 5+ as in Slovak and Czech), dates
/// <c>30. 9. 2026</c> and numbers <c>5 872</c>. A missing text is an error of the file, never an empty line.
/// </summary>
public sealed class ProtocolTexts
{
    private static readonly ConcurrentDictionary<string, ProtocolTexts?> Cache = new(StringComparer.Ordinal);

    private readonly Dictionary<object, object> root;

    private ProtocolTexts(string locale, Dictionary<object, object> root)
    {
        Locale = locale;
        this.root = root;
    }

    public string Locale { get; }

    /// <summary>The texts of the language, or null when the protocol has none in it.</summary>
    public static ProtocolTexts? For(string locale) => Cache.GetOrAdd(locale, Load);

    public string Text(string path) => Find(path) as string ?? throw new InvalidDataException($"Protocols/Texts/{Locale}.yaml: {path}");

    public string? TryText(string path) => Find(path) as string;

    public IReadOnlyList<string> List(string path) =>
        (Find(path) as List<object> ?? throw new InvalidDataException($"Protocols/Texts/{Locale}.yaml: {path}")).Select(o => (string)o).ToList();

    /// <summary>The text with its placeholders filled (<c>{name}</c> → value).</summary>
    public string Format(string path, params (string Name, string Value)[] values) => Fill(Text(path), values);

    public static string Fill(string text, params (string Name, string Value)[] values)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (var (name, value) in values)
        {
            text = text.Replace("{" + name + "}", value, StringComparison.Ordinal);
        }

        return text;
    }

    /// <summary>A number with its noun (<c>5 872 stránok</c>, <c>3 produkty</c>).</summary>
    public string Count(int count, string noun)
    {
        var forms = List("plural." + noun);
        var form = count == 1 ? forms[0] : count is >= 2 and <= 4 ? forms[1] : forms[2];
        return Number(count) + " " + form;
    }

    /// <summary>Thousands separated by a no-break space, as both languages write them.</summary>
    public static string Number(long value) => value.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ');

    public static string Date(DateOnly date) => $"{date.Day}. {date.Month}. {date.Year}";

    /// <summary><c>30. 9. – 31. 10. 2026</c> (the year once when both days are in it).</summary>
    public static string Period(DateOnly from, DateOnly to) =>
        from.Year == to.Year ? $"{from.Day}. {from.Month}. – {Date(to)}" : $"{Date(from)} – {Date(to)}";

    private object? Find(string path)
    {
        object? node = root;
        foreach (var part in path.Split('.'))
        {
            node = node is Dictionary<object, object> map && map.TryGetValue(part, out var next) ? next : null;
        }

        return node;
    }

    private static ProtocolTexts? Load(string locale)
    {
        var assembly = typeof(ProtocolTexts).Assembly;
        var name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(".Protocols.Texts." + locale + ".yaml", StringComparison.Ordinal));
        if (name is null)
        {
            return null;
        }

        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        var all = new DeserializerBuilder().Build().Deserialize<Dictionary<object, object>>(reader);
        return all.TryGetValue("protocol", out var section) && section is Dictionary<object, object> root ? new ProtocolTexts(locale, root) : null;
    }
}
