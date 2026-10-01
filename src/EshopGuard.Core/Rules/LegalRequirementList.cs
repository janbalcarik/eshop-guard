using System.Text.RegularExpressions;

namespace EshopGuard.Core.Rules;

/// <summary>
/// The file <c>config/legal_requirements.yaml</c> as written: requirements that the law imposes on all products of a
/// category on the EU market, and the claims that present them as a feature of the offer (SK zákon č. 108/2024 Z. z.,
/// príloha č. 1 bod 15; EU směrnice 2005/29/ES, příloha I bod 10a).
/// </summary>
public sealed class LegalRequirementFile
{
    /// <summary>Shared parts of claim expressions: <c>abs_cz</c>, <c>abs_sk</c> (absence prefix) and <c>gap</c>.</summary>
    public Dictionary<string, string> Macros { get; set; } = [];

    /// <summary>Product categories: expressions per country (<c>cz</c>, <c>sk</c>) searched in the page category text.</summary>
    public Dictionary<string, Dictionary<string, string>> Categories { get; set; } = [];

    /// <summary>Items of the list.</summary>
    public List<LegalRequirementDefinition> Claims { get; set; } = [];
}

/// <summary>
/// One item of the list: a product category, a feature ("bez BPA") and what the law says about presenting it.
/// The claim expression is either given directly (<c>claim</c>) or composed from a substance name (<c>subst</c>).
/// Fields with a <c>_cz</c> or <c>_sk</c> suffix override the shared field for that country.
/// </summary>
public sealed class LegalRequirementDefinition
{
    /// <summary>Unique id of the item, e.g. <c>lr_babybottle_bpa</c>.</summary>
    public string Id { get; set; } = "";

    /// <summary>Key in <see cref="LegalRequirementFile.Categories"/>.</summary>
    public string Category { get; set; } = "";

    /// <summary>Category text matching this expression is not in the category (e.g. nectar is not juice).</summary>
    public string? CategoryExclude { get; set; }

    /// <summary>Human-readable category, shown in findings.</summary>
    public string CategoryName { get; set; } = "";

    /// <summary>Human-readable feature, e.g. "bez BPA", shown in findings.</summary>
    public string Feature { get; set; } = "";

    /// <summary>SK (point 15): <c>text</c>, <c>verify</c>, <c>none</c> or <c>irrelevant</c>.</summary>
    public string Outcome { get; set; } = "";

    /// <summary>Czechia today; the shared outcome when missing.</summary>
    public string? OutcomeCz { get; set; }

    /// <summary>Names of the substance or material after the absence prefix ("bez", "neobsahuje").</summary>
    public string? Subst { get; set; }

    /// <summary><see cref="Subst"/> for Czech.</summary>
    public string? SubstCz { get; set; }

    /// <summary><see cref="Subst"/> for Slovak.</summary>
    public string? SubstSk { get; set; }

    /// <summary>Adjective after "bez" in one word ("bezfosfátový").</summary>
    public string? Adj { get; set; }

    /// <summary><see cref="Adj"/> for Czech.</summary>
    public string? AdjCz { get; set; }

    /// <summary><see cref="Adj"/> for Slovak.</summary>
    public string? AdjSk { get; set; }

    /// <summary>English name for "X-free" and "free from X".</summary>
    public string? En { get; set; }

    /// <summary>Further alternatives of the claim expression, without a leading <c>(?i)</c>.</summary>
    public string? Extra { get; set; }

    /// <summary><see cref="Extra"/> for Czech.</summary>
    public string? ExtraCz { get; set; }

    /// <summary><see cref="Extra"/> for Slovak.</summary>
    public string? ExtraSk { get; set; }

    /// <summary>Complete claim expression, used instead of composing one.</summary>
    public string? Claim { get; set; }

    /// <summary><see cref="Claim"/> for Czech.</summary>
    public string? ClaimCz { get; set; }

    /// <summary><see cref="Claim"/> for Slovak.</summary>
    public string? ClaimSk { get; set; }

    /// <summary>Since when the requirement applies (free text).</summary>
    public string? Since { get; set; }

    /// <summary>Legal basis, shown in findings.</summary>
    public string Basis { get; set; } = "";

    /// <summary>Remark of the research, shown in findings.</summary>
    public string? Note { get; set; }
}

/// <summary>
/// One item of the list compiled for one country.
/// </summary>
public sealed class LegalRequirement
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    /// <summary>Id of the item.</summary>
    public required string Id { get; init; }

    /// <summary><c>text</c>, <c>verify</c>, <c>none</c> or <c>irrelevant</c> for the country.</summary>
    public required string Outcome { get; init; }

    /// <summary>Human-readable category.</summary>
    public required string CategoryName { get; init; }

    /// <summary>Human-readable feature, e.g. "bez BPA".</summary>
    public required string Feature { get; init; }

    /// <summary>Legal basis.</summary>
    public required string Basis { get; init; }

    /// <summary>Since when the requirement applies (free text).</summary>
    public string? Since { get; init; }

    /// <summary>Remark of the research.</summary>
    public string? Note { get; init; }

    /// <summary>The claim expression as composed (tests compare it with the research).</summary>
    public required string ClaimPattern { get; init; }

    internal Regex ClaimRegex { get; init; } = null!;

    internal Regex CategoryRegex { get; init; } = null!;

    internal Regex? CategoryExcludeRegex { get; init; }

    /// <summary>The sentence makes the claim of this item.</summary>
    public bool MatchesClaim(string text) => IsMatch(ClaimRegex, text);

    /// <summary>The page category text belongs to the category of this item.</summary>
    public bool MatchesCategory(string category) =>
        IsMatch(CategoryRegex, category) && !(CategoryExcludeRegex is { } exclude && IsMatch(exclude, category));

    internal static Regex Create(string pattern) => new(pattern, RegexOptions.CultureInvariant, Timeout);

    // Short texts and bounded expressions; a timeout still must not stop the scan.
    private static bool IsMatch(Regex regex, string text)
    {
        try
        {
            return regex.IsMatch(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}

/// <summary>
/// The list compiled for Czechia and Slovakia.
/// </summary>
public sealed class LegalRequirementList
{
    /// <summary>Name of the list in <c>claim_list_match</c> checks.</summary>
    public const string ListName = "legal_requirement_claims";

    /// <summary>Allowed outcomes of an item.</summary>
    public static readonly IReadOnlySet<string> Outcomes = new HashSet<string>(StringComparer.Ordinal) { "text", "verify", "none", "irrelevant" };

    private static readonly string[] CountryCodes = ["cz", "sk"];

    private readonly IReadOnlyDictionary<string, IReadOnlyList<LegalRequirement>> _byCountry;

    private LegalRequirementList(IReadOnlyDictionary<string, IReadOnlyList<LegalRequirement>> byCountry) => _byCountry = byCountry;

    /// <summary>No items (the file is missing).</summary>
    public static LegalRequirementList Empty { get; } = new(new Dictionary<string, IReadOnlyList<LegalRequirement>>());

    /// <summary>True when no item compiled for any country.</summary>
    public bool IsEmpty => _byCountry.Values.All(items => items.Count == 0);

    /// <summary>Items for the country; empty for an unknown country.</summary>
    public IReadOnlyList<LegalRequirement> For(string country) => _byCountry.GetValueOrDefault(country) ?? [];

    /// <summary>Compiles the file; mistakes are added to <paramref name="errors"/> and the item is left out.</summary>
    public static LegalRequirementList Compile(LegalRequirementFile file, string fileName, List<string> errors)
    {
        var byCountry = CountryCodes.ToDictionary(c => c, _ => new List<LegalRequirement>());
        var macroMissing = new[] { "abs_cz", "abs_sk", "gap" }.Where(m => !file.Macros.ContainsKey(m)).ToList();
        foreach (var macro in macroMissing)
        {
            errors.Add($"{fileName}: chybí makro „{macro}“.");
        }

        foreach (var id in file.Claims.GroupBy(c => c.Id).Where(g => g.Count() > 1).Select(g => g.Key))
        {
            errors.Add($"{fileName}: položka „{id}“ je v seznamu dvakrát.");
        }

        foreach (var item in file.Claims)
        {
            var label = $"{fileName}, položka „{item.Id}“";
            if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.CategoryName)
                || string.IsNullOrWhiteSpace(item.Feature) || string.IsNullOrWhiteSpace(item.Basis))
            {
                errors.Add($"{label}: id, category_name, feature a basis jsou povinné.");
                continue;
            }

            if (!Outcomes.Contains(item.Outcome) || (item.OutcomeCz is not null && !Outcomes.Contains(item.OutcomeCz)))
            {
                errors.Add($"{label}: outcome a outcome_cz musí být text, verify, none nebo irrelevant.");
                continue;
            }

            if (!file.Categories.TryGetValue(item.Category, out var categories))
            {
                errors.Add($"{label}: kategorie „{item.Category}“ není v categories.");
                continue;
            }

            if (macroMissing.Count > 0)
            {
                continue;
            }

            foreach (var country in CountryCodes)
            {
                var compiled = CompileFor(item, country, categories, file.Macros, label, errors);
                if (compiled is not null)
                {
                    byCountry[country].Add(compiled);
                }
            }
        }

        return new LegalRequirementList(byCountry.ToDictionary(p => p.Key, p => (IReadOnlyList<LegalRequirement>)p.Value));
    }

    /// <summary>
    /// Composes the claim expression the same way as the research does:
    /// <c>(?i)(?:\b{abs}\s+{gap}(?:{subst})|\bbez-?(?:{adj})|\b(?:{en})\s*-?\s*free\b|\bfree\s+(?:from|of)\s+(?:{en})|\b0\s*%\s*(?:{subst})|{extra})</c>;
    /// a missing adjective, English name or extra leaves its alternatives out.
    /// </summary>
    public static string ComposeClaim(string absence, string gap, string subst, string? adj, string? en, string? extra)
    {
        var parts = new List<string> { $@"\b{absence}\s+{gap}(?:{subst})" };
        if (!string.IsNullOrEmpty(adj))
        {
            parts.Add($@"\bbez-?(?:{adj})");
        }

        if (!string.IsNullOrEmpty(en))
        {
            parts.Add($@"\b(?:{en})\s*-?\s*free\b");
            parts.Add($@"\bfree\s+(?:from|of)\s+(?:{en})");
        }

        parts.Add($@"\b0\s*%\s*(?:{subst})");
        if (!string.IsNullOrEmpty(extra))
        {
            parts.Add(extra);
        }

        return "(?i)(?:" + string.Join("|", parts) + ")";
    }

    private static LegalRequirement? CompileFor(LegalRequirementDefinition item, string country,
        Dictionary<string, string> categories, Dictionary<string, string> macros, string label, List<string> errors)
    {
        var sk = country == "sk";
        var subst = (sk ? item.SubstSk : item.SubstCz) ?? item.Subst;
        var claim = (sk ? item.ClaimSk : item.ClaimCz) ?? item.Claim;
        if (claim is null && subst is null)
        {
            errors.Add($"{label}: chybí claim nebo subst pro {country}.");
            return null;
        }

        if (!categories.TryGetValue(country, out var categoryPattern))
        {
            errors.Add($"{label}: kategorie „{item.Category}“ nemá výraz pro {country}.");
            return null;
        }

        var pattern = claim ?? ComposeClaim(
            macros[sk ? "abs_sk" : "abs_cz"], macros["gap"], subst!,
            (sk ? item.AdjSk : item.AdjCz) ?? item.Adj, item.En, (sk ? item.ExtraSk : item.ExtraCz) ?? item.Extra);
        try
        {
            return new LegalRequirement
            {
                Id = item.Id,
                Outcome = sk ? item.Outcome : item.OutcomeCz ?? item.Outcome,
                CategoryName = item.CategoryName,
                Feature = item.Feature,
                Basis = item.Basis,
                Since = item.Since,
                Note = item.Note,
                ClaimPattern = pattern,
                ClaimRegex = LegalRequirement.Create(pattern),
                CategoryRegex = LegalRequirement.Create(categoryPattern),
                CategoryExcludeRegex = item.CategoryExclude is { } exclude ? LegalRequirement.Create(exclude) : null,
            };
        }
        catch (ArgumentException ex)
        {
            errors.Add($"{label} ({country}): neplatný regulární výraz ({ex.Message}).");
            return null;
        }
    }
}
