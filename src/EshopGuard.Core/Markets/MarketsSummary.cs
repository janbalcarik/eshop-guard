using EshopGuard.Core.Languages;

namespace EshopGuard.Core.Markets;

/// <summary>Codes of the sentence of 3c about the versions.</summary>
public static class SummaryCodes
{
    public const string VersionsSingle = "versions_single";
    public const string VersionsBothOwnTexts = "versions_both_own_texts";
    public const string VersionsSameTextsMenuOnly = "versions_same_texts_menu_only";
    public const string VersionsUntranslatedTexts = "versions_untranslated_texts";
}

/// <summary>
/// The sentence of 3c and the notices (change 7, design section 7): the most important state first (a version to confirm,
/// a browser needed, a translation in the browser, another language, an insufficient sample), then what the comparison of the
/// texts shows. Every other applicable code is a notice, so nothing is left out. Unsupported versions are not mentioned.
/// </summary>
internal static class MarketsSummary
{
    public static (VersionSummary Summary, IReadOnlyList<VersionSummary> Notices) Build(
        IReadOnlyList<LanguageVersionCandidate> versions, VersionPlan plan, VersionComparisonResult? comparison, double threshold)
    {
        var all = new List<VersionSummary>();
        foreach (var version in versions.Where(v => !v.IsMain && v.Status != VersionStatus.Unsupported))
        {
            if (version.Status == VersionStatus.NeedsConfirmation)
            {
                all.Add(new VersionSummary(VersionCodes.OtherDomainNeedsConfirmation, Params(("domain", new Uri(version.BaseUrl).Host), ("language", version.Language))));
            }
            else if (version.Status == VersionStatus.NeedsBrowser)
            {
                all.Add(new VersionSummary(VersionCodes.NeedsBrowser, Params(("language", version.Language))));
            }
            else if (version.SwitchMethod == SwitchMethods.BrowserTranslation)
            {
                all.Add(new VersionSummary(VersionCodes.BrowserTranslation, Params(("language", version.Language))));
            }
            else if (version.Status == VersionStatus.Mismatch)
            {
                all.Add(new VersionSummary(version.Codes.FirstOrDefault() ?? VersionCodes.LanguageMismatch, Params(("language", version.Language), ("url", version.BaseUrl))));
            }
        }

        var compared = comparison?.Versions.Where(v => !v.IsMain).ToList() ?? [];
        foreach (var version in compared.Where(v => v.Codes.Contains(VersionCodes.SampleInsufficient)))
        {
            all.Add(new VersionSummary(VersionCodes.SampleInsufficient, Params(("language", version.Language), ("products", version.SampleProducts))));
        }

        // Untranslated texts of any version, the main one included (its descriptions in another language, goodie.sk).
        foreach (var version in (comparison?.Versions ?? []).Where(v => v.Warnings.Contains(VersionCodes.UntranslatedText)))
        {
            var products = version.IsMain ? version.LabeledProducts : Math.Max(version.Pairs.Count, version.LabeledProducts);
            var count = version.IsMain ? version.ForeignTextProducts : Math.Max(version.Pairs.Count(p => p.Kind == PairKinds.Untranslated), version.ForeignTextProducts);
            var share = products == 0 ? 0 : Math.Round(count / (double)products, 2);
            all.Add(new VersionSummary(SummaryCodes.VersionsUntranslatedTexts,
                Params(("language", version.Language), ("share", share), ("products", count), ("of", products), ("text_language", version.ForeignTextLanguage))));
        }

        var main = comparison?.Versions.FirstOrDefault(v => v.IsMain);
        var sameTexts = compared.Where(v => !v.Codes.Contains(VersionCodes.SampleInsufficient) && v.OwnTextShare < threshold).ToList();
        foreach (var version in sameTexts)
        {
            all.Add(new VersionSummary(SummaryCodes.VersionsSameTextsMenuOnly, Params(("language", version.Language), ("other", main?.Language))));
        }

        var own = compared.Where(v => v.OwnTextShare >= threshold && !v.Codes.Contains(VersionCodes.SampleInsufficient)).ToList();
        if (own.Count > 0 && main is not null)
        {
            all.Add(new VersionSummary(SummaryCodes.VersionsBothOwnTexts,
                Params(("languages", new[] { main.Language }.Concat(own.Select(v => v.Language)).ToArray()), ("share", own.Min(v => v.OwnTextShare)))));
        }

        if (all.Count == 0)
        {
            return (new VersionSummary(SummaryCodes.VersionsSingle, Params(("language", plan.Checked.FirstOrDefault()?.Language ?? versions.First(v => v.IsMain).Language))), []);
        }

        return (all[0], all.Skip(1).ToList());
    }

    private static IReadOnlyDictionary<string, object> Params(params (string Name, object? Value)[] values) =>
        values.Where(v => v.Value is not null).ToDictionary(v => v.Name, v => v.Value!);
}
