using EshopGuard.Core.Languages;

namespace EshopGuard.Core.Markets;

/// <summary>Codes of the sentence of 3c about the versions.</summary>
public static class SummaryCodes
{
    public const string VersionsSingle = "versions_single";

    /// <summary>
    /// The versions found and which one is checked for every ticked market („Pre Slovensko kontrolujeme slovenskú verziu, pre
    /// Česko českú.“), with the share of products whose description is in the language of its version.
    /// </summary>
    public const string VersionsByMarket = "versions_by_market";

    public const string VersionsUntranslatedTexts = "versions_untranslated_texts";
}

/// <summary>
/// The sentence of 3c and the notices (change 7, design section 7): the most important state first (a version to confirm,
/// a browser needed, a translation in the browser, another language, an insufficient sample, untranslated descriptions), then
/// which version is checked for which market. Every other applicable code is a notice, so nothing is left out. Unsupported
/// versions are not mentioned.
/// </summary>
internal static class MarketsSummary
{
    public static (VersionSummary Summary, IReadOnlyList<VersionSummary> Notices) Build(
        IReadOnlyList<LanguageVersionCandidate> versions, VersionPlan plan, IReadOnlyList<VersionLanguage>? languages)
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

        foreach (var version in (languages ?? []).Where(v => v.Codes.Contains(VersionCodes.SampleInsufficient)))
        {
            all.Add(new VersionSummary(VersionCodes.SampleInsufficient, Params(("language", version.Language), ("products", version.SampleProducts))));
        }

        // Untranslated descriptions of any version, the main one included (its descriptions in another language, goodie.sk).
        foreach (var version in (languages ?? []).Where(v => v.Warnings.Contains(VersionCodes.UntranslatedText)))
        {
            var share = version.LabeledProducts == 0 ? 0 : Math.Round(version.ForeignTextProducts / (double)version.LabeledProducts, 2);
            all.Add(new VersionSummary(SummaryCodes.VersionsUntranslatedTexts,
                Params(("language", version.Language), ("share", share), ("products", version.ForeignTextProducts), ("of", version.LabeledProducts),
                    ("text_language", version.ForeignTextLanguage))));
        }

        // Which version every ticked market is checked on (the price counts its products for each of them).
        var found = versions.Where(v => v.Status != VersionStatus.Unsupported).ToList();
        if (found.Count > 1 && plan.ByMarket.Count > 0)
        {
            var byKey = (languages ?? []).ToDictionary(v => v.Key, StringComparer.Ordinal);
            var shares = plan.Checked.Select(v => byKey.GetValueOrDefault(VersionMarketPlanner.Key(v.Language, v.BaseUrl))?.TranslatedShare).OfType<double>().ToList();
            all.Add(new VersionSummary(SummaryCodes.VersionsByMarket, Params(
                ("languages", plan.Checked.Select(v => v.Language).ToArray()),
                ("urls", plan.Checked.Select(v => v.BaseUrl).ToArray()),
                ("markets", plan.ByMarket.ToDictionary(m => m.Market, m => m.Language)),
                ("translated_share", shares.Count == 0 ? null : shares.Min()))));
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
