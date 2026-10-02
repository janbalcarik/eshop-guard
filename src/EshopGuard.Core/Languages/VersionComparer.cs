using System.Text.RegularExpressions;
using EshopGuard.Core.Markets;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Segmentation;

namespace EshopGuard.Core.Languages;

/// <summary>A downloaded page of the sample: its sentences of the main text, alternates group and product identifiers.</summary>
public sealed record SamplePage(string Url, IReadOnlyList<string> Sentences, string? HreflangGroup, IReadOnlyList<ProductIdentifier> ProductIds);

/// <summary>The downloaded sample of one version and the language of its sentences (page URL → labels of its sentences).</summary>
public sealed record VersionSample(string Key, string Language, bool IsMain, IReadOnlyList<SamplePage> Products, IReadOnlyList<SamplePage> Mandatory, int? ProductCount)
{
    /// <summary>Labels of the language of the sentences sent to the model, by page.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> PageLanguages { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
}

/// <summary>Kinds of difference of a pair (change 7, design section 6).</summary>
public static class PairKinds
{
    public const string Identical = "identical";
    public const string Translation = "translation";
    public const string ShortenedOrDifferent = "shortened_or_different";
    public const string Untranslated = "untranslated";
}

/// <summary>A pair of the same product in the main version and another one.</summary>
public sealed record PairComparison(string MainUrl, string OtherUrl, string Kind, double LengthRatio, int MainSentences, int OtherSentences)
{
    /// <summary>Share of the sentences of the other page found word for word on the main page (a copied, untranslated text is near 1).</summary>
    public double SharedSentenceShare { get; init; }
}

/// <summary>The comparison of one version with the others (a row of <c>shop.shop_languages</c> and the details of 3d).</summary>
public sealed record VersionComparison
{
    public required string Key { get; init; }

    public required string Language { get; init; }

    public bool IsMain { get; init; }

    /// <summary>
    /// Share of unique sentences of the main text of its product pages found in no other checked version; with at least
    /// <c>markets.min_sample_products</c> pairs, over the paired products only (sentences not on the counterpart pages).
    /// </summary>
    public double OwnTextShare { get; init; }

    /// <summary>Share of the sentences labeled by the model, by language.</summary>
    public IReadOnlyDictionary<string, double> LanguageShare { get; init; } = new Dictionary<string, double>();

    public IReadOnlyList<PairComparison> Pairs { get; init; } = [];

    /// <summary>Without pairs: share of its sentences found word for word in the main version.</summary>
    public double? SentenceOverlapShare { get; init; }

    /// <summary>Its mandatory pages (terms, delivery) have other sentences than those of the main version.</summary>
    public bool MandatoryPagesDiffer { get; init; }

    /// <summary>Mandatory pages of this version with sentences that are not in the main version.</summary>
    public IReadOnlyList<string> MandatoryPagesDifferUrls { get; init; } = [];

    /// <summary>Product pages of the sample with a main text.</summary>
    public int SampleProducts { get; init; }

    public int? ProductCount { get; init; }

    /// <summary>Counts into the price (main version: whenever its product count is known).</summary>
    public bool Counted { get; init; }

    /// <summary>Why it does not count (<c>version_sample_insufficient</c>, <c>version_product_count_unknown</c>, <c>version_language_unknown</c>).</summary>
    public IReadOnlyList<string> Codes { get; init; } = [];

    /// <summary>Warnings that are not violations (<c>untranslated_text</c>).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Addresses of pages with untranslated text (examples for 3d).</summary>
    public IReadOnlyList<string> UntranslatedExamples { get; init; } = [];
}

/// <summary>The comparison of all versions of the sample and how pairs were made.</summary>
public sealed record VersionComparisonResult(IReadOnlyList<VersionComparison> Versions, string PairingMode);

/// <summary>
/// Compares the language versions of the sample without a model (change 7, design section 6; thresholds of the research of
/// 1. 10. 2026, 44 of 46 pairs): own texts by fingerprints of sentences, the kind of difference of a pair by its length and
/// number of sentences, untranslated text by the language the model gave its sentences. A version counts into the price only
/// with enough own texts, a sufficient sample, a known number of products and a known language; when unsure it does not count.
/// </summary>
public static partial class VersionComparer
{
    /// <summary>Lengths of a faithful translation (the other text divided by the main one).</summary>
    public const double MinTranslationRatio = 0.8;

    public const double MaxTranslationRatio = 1.25;

    public static VersionComparisonResult Compare(IReadOnlyList<VersionSample> versions, IReadOnlyList<SamplePair> plannedPairs, MarketsOptions options)
    {
        ArgumentNullException.ThrowIfNull(versions);
        var main = versions.FirstOrDefault(v => v.IsMain) ?? versions[0];
        var fingerprints = versions.ToDictionary(v => v.Key, v => v.Products.SelectMany(p => p.Sentences).Select(SentenceFingerprint.Of).ToHashSet());
        var mainMandatory = main.Mandatory.SelectMany(p => p.Sentences).Select(SentenceFingerprint.Of).ToHashSet();
        var modes = new HashSet<string>();
        var paired = versions.Where(v => v.Key != main.Key).ToDictionary(v => v.Key, v => PairPages(main, v, plannedPairs, modes));
        var results = new List<VersionComparison>();
        foreach (var version in versions)
        {
            var own = fingerprints[version.Key];
            var versionPairs = version.Key == main.Key ? paired.Values.SelectMany(p => p).ToList() : paired[version.Key];
            double ownShare;
            if (versionPairs.Count >= options.MinSampleProducts)
            {
                // The same products in both versions: own sentences are those not on the counterpart pages. Over different
                // products (random samples) every product would look like an own text, a copied catalog too.
                var mine = SentencePrints(version.Key == main.Key ? versionPairs.Select(p => p.Main) : versionPairs.Select(p => p.Other));
                var theirs = SentencePrints(version.Key == main.Key ? versionPairs.Select(p => p.Other) : versionPairs.Select(p => p.Main));
                ownShare = mine.Count == 0 ? 0 : mine.Count(f => !theirs.Contains(f)) / (double)mine.Count;
            }
            else
            {
                var others = versions.Where(v => v.Key != version.Key).SelectMany(v => fingerprints[v.Key]).ToHashSet();
                ownShare = own.Count == 0 ? 0 : own.Count(f => !others.Contains(f)) / (double)own.Count;
            }

            var labels = version.PageLanguages.Values.SelectMany(l => l).ToList();
            var languageShare = labels.GroupBy(l => l).ToDictionary(g => g.Key, g => Math.Round(g.Count() / (double)labels.Count, 3));
            var pairs = version.Key == main.Key ? []
                : paired[version.Key].Select(p => Classify(p.Main, p.Other, main.Language, version.Language, Majority(version.PageLanguages.GetValueOrDefault(p.Other.Url)))).ToList();
            var sampleProducts = version.Products.Count(p => p.Sentences.Count > 0);
            var mandatoryDiffer = version.IsMain ? [] : version.Mandatory
                .Where(p => p.Sentences.Select(SentenceFingerprint.Of).Any(f => !mainMandatory.Contains(f)))
                .Select(p => p.Url)
                .ToList();
            var untranslated = pairs.Where(p => p.Kind == PairKinds.Untranslated).Select(p => p.OtherUrl).ToList();

            var codes = new List<string>();
            if (!version.IsMain)
            {
                if (sampleProducts < options.MinSampleProducts)
                {
                    codes.Add(VersionCodes.SampleInsufficient);
                }

                if (labels.Count == 0 || languageShare.OrderByDescending(s => s.Value).First().Key == "und")
                {
                    codes.Add(VersionCodes.LanguageUnknown);
                }
            }

            if (version.ProductCount is null)
            {
                codes.Add(VersionCodes.ProductCountUnknown);
            }

            var counted = version.IsMain
                ? version.ProductCount is not null
                : codes.Count == 0 && ownShare >= options.CountedMinOwnShare;
            results.Add(new VersionComparison
            {
                Key = version.Key,
                Language = version.Language,
                IsMain = version.IsMain,
                OwnTextShare = Math.Round(ownShare, 3),
                LanguageShare = languageShare,
                Pairs = pairs,
                SentenceOverlapShare = version.IsMain || pairs.Count > 0 || own.Count == 0
                    ? null
                    : Math.Round(own.Count(fingerprints[main.Key].Contains) / (double)own.Count, 3),
                MandatoryPagesDiffer = mandatoryDiffer.Count > 0,
                MandatoryPagesDifferUrls = mandatoryDiffer,
                SampleProducts = sampleProducts,
                ProductCount = version.ProductCount,
                Counted = counted,
                Codes = codes,
                Warnings = untranslated.Count > 0 ? [VersionCodes.UntranslatedText] : [],
                UntranslatedExamples = untranslated.Take(5).ToList(),
            });
        }

        var mode = modes.Contains(VersionSamplePlanner.ModeHreflang) ? VersionSamplePlanner.ModeHreflang
            : modes.Contains(VersionSamplePlanner.ModeIdentifiers) ? VersionSamplePlanner.ModeIdentifiers
            : VersionSamplePlanner.ModeSentenceOverlap;
        return new VersionComparisonResult(results, mode);
    }

    /// <summary>The kind of difference of a pair: identical text, untranslated (the language of the main version), translation by length and sentences, otherwise shortened or different.</summary>
    public static PairComparison Classify(SamplePage mainPage, SamplePage otherPage, string mainLanguage, string otherLanguage, string? otherPageLanguage)
    {
        var mainText = string.Join(' ', mainPage.Sentences);
        var otherText = string.Join(' ', otherPage.Sentences);
        var ratio = mainText.Length == 0 ? 0 : Math.Round(otherText.Length / (double)mainText.Length, 3);
        var (a, b) = (mainPage.Sentences.Count, otherPage.Sentences.Count);
        string kind;
        if (TextTools.NormalizeForHash(mainText) == TextTools.NormalizeForHash(otherText))
        {
            kind = PairKinds.Identical;
        }
        else if (otherPageLanguage is not null && LanguageTags.SamePrimary(otherPageLanguage, mainLanguage) && !LanguageTags.SamePrimary(otherPageLanguage, otherLanguage))
        {
            kind = PairKinds.Untranslated;
        }
        else if (ratio is >= MinTranslationRatio and <= MaxTranslationRatio && Math.Abs(a - b) <= Math.Max(2, 0.15 * Math.Max(a, b)))
        {
            kind = PairKinds.Translation;
        }
        else
        {
            kind = PairKinds.ShortenedOrDifferent;
        }

        var mainPrints = mainPage.Sentences.Select(SentenceFingerprint.Of).ToHashSet();
        var shared = b == 0 ? 0 : Math.Round(otherPage.Sentences.Count(s => mainPrints.Contains(SentenceFingerprint.Of(s))) / (double)b, 3);
        return new PairComparison(mainPage.Url, otherPage.Url, kind, ratio, a, b) { SharedSentenceShare = shared };
    }

    /// <summary>Sentences of a main text: blocks split at the end of a sentence.</summary>
    public static IReadOnlyList<string> Sentences(string mainText) =>
        mainText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(block => SentenceEnd().Split(block.Trim()))
            .Select(s => s.Trim())
            .Where(s => s.Length >= 3)
            .ToList();

    /// <summary>
    /// The sentences of a version for the model: <paramref name="count"/> sentences of the given length, from the paired pages
    /// first and then from the other product pages in turn.
    /// </summary>
    public static IReadOnlyList<LanguageFragment> Fragments(VersionSample version, IEnumerable<string> pairedUrls, int count, int minChars, int maxChars)
    {
        var paired = pairedUrls.ToHashSet(StringComparer.Ordinal);
        var queues = version.Products.OrderByDescending(p => paired.Contains(p.Url))
            .Select(p => (p.Url, Queue: new Queue<string>(p.Sentences.Where(s => s.Length >= minChars && s.Length <= maxChars))))
            .Where(q => q.Queue.Count > 0)
            .ToList();
        var fragments = new List<LanguageFragment>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (fragments.Count < count && queues.Any(q => q.Queue.Count > 0))
        {
            foreach (var (url, queue) in queues.Where(q => q.Queue.Count > 0))
            {
                var sentence = queue.Dequeue();
                if (fragments.Count < count && seen.Add(sentence))
                {
                    fragments.Add(new LanguageFragment(fragments.Count + 1, sentence, url));
                }
            }
        }

        return fragments;
    }

    private static HashSet<long> SentencePrints(IEnumerable<SamplePage> pages) =>
        pages.DistinctBy(p => p.Url).SelectMany(p => p.Sentences).Select(SentenceFingerprint.Of).ToHashSet();

    /// <summary>The same products in the main version and another one: planned pairs, then the same alternates group or identifier.</summary>
    private static List<(SamplePage Main, SamplePage Other)> PairPages(VersionSample main, VersionSample other, IReadOnlyList<SamplePair> plannedPairs, HashSet<string> modes)
    {
        var mainPages = main.Products.ToDictionary(p => p.Url);
        var otherPages = other.Products.ToDictionary(p => p.Url);
        var pairs = new List<(SamplePage Main, SamplePage Other)>();
        foreach (var planned in plannedPairs.Where(p => p.OtherKey == other.Key))
        {
            if (mainPages.TryGetValue(planned.MainUrl, out var a) && otherPages.TryGetValue(planned.OtherUrl, out var b))
            {
                pairs.Add((a, b));
                modes.Add(VersionSamplePlanner.ModeHreflang);
            }
        }

        // Pairs by the same alternates group or product identifier (EAN, SKU, MPN, productID) among the downloaded pages.
        foreach (var page in other.Products.Where(p => !pairs.Any(x => x.Other.Url == p.Url)))
        {
            var match = main.Products.FirstOrDefault(m => !pairs.Any(x => x.Main.Url == m.Url)
                && ((m.HreflangGroup is not null && m.HreflangGroup == page.HreflangGroup) || m.ProductIds.Intersect(page.ProductIds).Any()));
            if (match is not null)
            {
                pairs.Add((match, page));
                modes.Add(match.HreflangGroup is not null && match.HreflangGroup == page.HreflangGroup ? VersionSamplePlanner.ModeHreflang : VersionSamplePlanner.ModeIdentifiers);
            }
        }

        return pairs;
    }

    private static string? Majority(IReadOnlyList<string>? labels) =>
        labels is null || labels.Count == 0 ? null : labels.GroupBy(l => l).OrderByDescending(g => g.Count()).First().Key;

    [GeneratedRegex(@"(?<=[.!?…])\s+(?=[\p{Lu}\d„""])")]
    private static partial Regex SentenceEnd();
}
