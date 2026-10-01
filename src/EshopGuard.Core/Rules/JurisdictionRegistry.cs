namespace EshopGuard.Core.Rules;

/// <summary>
/// A jurisdiction the tool knows, from <c>config/jurisdictions.yaml</c>.
/// </summary>
public sealed class JurisdictionInfo
{
    /// <summary>Language of the laws of the jurisdiction (e.g. <c>cs</c> for <c>cz</c>); EU references of its verdicts are shown in it.</summary>
    public string LawLanguage { get; set; } = "";
}

/// <summary>
/// The jurisdictions the tool knows. A new market is a new line in <c>config/jurisdictions.yaml</c> plus rule sets and texts;
/// no code knows the list. Rule sets, effective dates, overrides and texts may name only these jurisdictions, so a typo never
/// silently becomes another country.
/// </summary>
public sealed class JurisdictionRegistry
{
    /// <summary>No jurisdictions.</summary>
    public static JurisdictionRegistry Empty { get; } = new(new Dictionary<string, JurisdictionInfo>());

    /// <summary>Creates the registry.</summary>
    public JurisdictionRegistry(IReadOnlyDictionary<string, JurisdictionInfo> items)
    {
        Items = items;
    }

    /// <summary>Jurisdictions by code.</summary>
    public IReadOnlyDictionary<string, JurisdictionInfo> Items { get; }

    /// <summary>Codes in alphabetical order.</summary>
    public IReadOnlyList<string> Codes => Items.Keys.Order(StringComparer.Ordinal).ToList();

    /// <summary>True when the jurisdiction is known.</summary>
    public bool Contains(string code) => Items.ContainsKey(code);

    /// <summary>Language of the laws of the jurisdiction, or null when it is not known.</summary>
    public string? LawLanguage(string code) => Items.TryGetValue(code, out var info) && info.LawLanguage.Length > 0 ? info.LawLanguage : null;
}
