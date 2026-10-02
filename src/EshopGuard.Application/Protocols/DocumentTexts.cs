using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EshopGuard.Application.Protocols;

/// <summary>
/// Texts of the documents the backend writes itself (change 11): the export of findings (CSV) and the protocol (PDF), from
/// the embedded files <c>Protocols/Texts/{locale}.yaml</c>. The texts of the findings come from the texts of the rules; the
/// legal references stay in the language of the law.
/// </summary>
public sealed class DocumentTexts
{
    private static readonly ConcurrentDictionary<string, DocumentTexts?> Loaded = new(StringComparer.Ordinal);

    public string Locale { get; set; } = "";

    public string Culture { get; set; } = "";

    public CsvTexts Csv { get; set; } = new();

    public Dictionary<string, string> Groups { get; set; } = [];

    public Dictionary<string, string> Severities { get; set; } = [];

    public Dictionary<string, string> Statuses { get; set; } = [];

    public string WholeSite { get; set; } = "";

    public ProtocolTexts Protocol { get; set; } = new();

    [YamlIgnore]
    public CultureInfo CultureInfo => CultureInfo.GetCultureInfo(string.IsNullOrEmpty(Culture) ? Locale : Culture);

    /// <summary>Languages with texts.</summary>
    public static IReadOnlyList<string> Locales { get; } = typeof(DocumentTexts).Assembly.GetManifestResourceNames()
        .Where(n => n.Contains(".Protocols.Texts.", StringComparison.Ordinal) && n.EndsWith(".yaml", StringComparison.Ordinal))
        .Select(n => n[(n.LastIndexOf(".Texts.", StringComparison.Ordinal) + 7)..^5])
        .Order(StringComparer.Ordinal)
        .ToList();

    /// <summary>The texts of a language, or null when there are none.</summary>
    public static DocumentTexts? For(string locale) => Loaded.GetOrAdd(locale, Load);

    public string Group(string group) => Groups.GetValueOrDefault(group, group);

    public string Severity(string severity) => Severities.GetValueOrDefault(severity, severity);

    public string Status(string status) => Statuses.GetValueOrDefault(status, status);

    private static DocumentTexts? Load(string locale)
    {
        var assembly = typeof(DocumentTexts).Assembly;
        var name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(".Protocols.Texts." + locale + ".yaml", StringComparison.Ordinal));
        if (name is null)
        {
            return null;
        }

        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).IgnoreUnmatchedProperties().Build()
            .Deserialize<DocumentTexts>(reader);
    }
}

/// <summary>Headers of the columns of the export of findings.</summary>
public sealed class CsvTexts
{
    public Dictionary<string, string> Columns { get; set; } = [];
}

/// <summary>Texts of the protocol (headings, labels, the statement).</summary>
public sealed class ProtocolTexts
{
    public Dictionary<string, string> Labels { get; set; } = [];

    public string Statement { get; set; } = "";

    public string Label(string key) => Labels.GetValueOrDefault(key, key);
}
