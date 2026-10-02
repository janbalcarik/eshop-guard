using System.Text.RegularExpressions;
using EshopGuard.Core.Markets;
using EshopGuard.Core.Options;

namespace EshopGuard.Core.Languages;

/// <summary>A downloaded product page of the sample: the sentences of its product description (profile) or of its main text.</summary>
public sealed record SamplePage(string Url, IReadOnlyList<string> Sentences)
{
    /// <summary>The sentences are of the product description of the profile (without reviews and other parts of the page).</summary>
    public bool FromDescription { get; init; }
}

/// <summary>The downloaded sample of one version and the language of its sentences (page URL → labels of its sentences).</summary>
public sealed record VersionSample(string Key, string Language, bool IsMain, IReadOnlyList<SamplePage> Products, int? ProductCount)
{
    /// <summary>Labels of the language of the sentences sent to the model, by page.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> PageLanguages { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>The sentences sent to the model with the language it gave them (for 3d and the check of the analysis).</summary>
    public IReadOnlyList<LabeledFragment> Fragments { get; init; } = [];
}

/// <summary>A sentence of a product page and the language the model gave it.</summary>
public sealed record LabeledFragment(string PageUrl, string Text, string Language);

/// <summary>Where the sentences of the language of the descriptions come from.</summary>
public static class SentenceSources
{
    /// <summary>The product description of the profile of every product page.</summary>
    public const string Description = "description";

    /// <summary>The main text of the pages (no profile; reviews and texts of the template included).</summary>
    public const string MainText = "main_text";

    /// <summary>The description on some pages, the main text on the others.</summary>
    public const string Mixed = "mixed";
}

/// <summary>The language of the product descriptions of one version (a row of <c>shop.shop_languages</c> and the details of 3d).</summary>
public sealed record VersionLanguage
{
    public required string Key { get; init; }

    public required string Language { get; init; }

    public bool IsMain { get; init; }

    /// <summary>Share of the sentences labeled by the model, by language.</summary>
    public IReadOnlyDictionary<string, double> LanguageShare { get; init; } = new Dictionary<string, double>();

    /// <summary>Product pages of the sample with sentences.</summary>
    public int SampleProducts { get; init; }

    public int? ProductCount { get; init; }

    /// <summary>What the analysis could not tell (<c>version_sample_insufficient</c>, <c>version_language_unknown</c>, <c>version_product_count_unknown</c>).</summary>
    public IReadOnlyList<string> Codes { get; init; } = [];

    /// <summary>Warnings that are not violations (<c>untranslated_text</c>).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Addresses of products whose description is in another language (examples for 3d).</summary>
    public IReadOnlyList<string> UntranslatedExamples { get; init; } = [];

    /// <summary>Where the sentences come from (<see cref="SentenceSources"/>).</summary>
    public string Basis { get; init; } = SentenceSources.MainText;

    /// <summary>Product pages whose sentences are of the description from the profile.</summary>
    public int DescriptionPages { get; init; }

    /// <summary>Products whose language the model labeled.</summary>
    public int LabeledProducts { get; init; }

    /// <summary>Products whose description is in the language of the version.</summary>
    public int TranslatedProducts { get; init; }

    /// <summary>Products whose description is in another language than the version.</summary>
    public int ForeignTextProducts { get; init; }

    /// <summary>The language of most of those descriptions.</summary>
    public string? ForeignTextLanguage { get; init; }

    /// <summary>Share of the labeled products with a description in the language of the version; null without a labeled product.</summary>
    public double? TranslatedShare => LabeledProducts == 0 ? null : Math.Round(TranslatedProducts / (double)LabeledProducts, 3);

    /// <summary>The sentences the model labeled, with their language.</summary>
    public IReadOnlyList<LabeledFragment> LanguageFragments { get; init; } = [];
}

/// <summary>
/// The language of the product descriptions of the versions of the sample (change 7, design section 6; the texts of the
/// versions are not compared since 2. 10. 2026, the price counts the products of every country). The model labels the first
/// sentences of the descriptions of about <c>markets.language_products</c> random products of each version; a product whose
/// description is in another language than its version is an untranslated text, a warning only, in the main version too.
/// </summary>
public static partial class VersionLanguages
{
    public static IReadOnlyList<VersionLanguage> Analyze(IReadOnlyList<VersionSample> versions, MarketsOptions options)
    {
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(options);
        var results = new List<VersionLanguage>();
        foreach (var version in versions)
        {
            var labels = version.PageLanguages.Values.SelectMany(l => l).ToList();
            var languageShare = labels.GroupBy(l => l).ToDictionary(g => g.Key, g => Math.Round(g.Count() / (double)labels.Count, 3));
            var sampleProducts = version.Products.Count(p => p.Sentences.Count > 0);

            // The language of each product by its first sentences (the model). Only more than half of its sentences make a
            // product foreign; one Czech sentence of two is not a Czech description (goodie.sk, 2. 10. 2026).
            var labeled = version.Products
                .Select(p => (Page: p, Labels: version.PageLanguages.GetValueOrDefault(p.Url) ?? []))
                .Where(x => x.Labels.Any(l => l != "und"))
                .Select(x => (x.Page, Language: Majority(x.Labels)))
                .ToList();
            var foreign = labeled.Where(x => x.Language is not null && x.Language != "und" && !LanguageTags.SamePrimary(x.Language, version.Language)).ToList();
            var translated = labeled.Count(x => x.Language is not null && LanguageTags.SamePrimary(x.Language, version.Language));
            var descriptionPages = version.Products.Count(p => p.FromDescription && p.Sentences.Count > 0);
            var basis = descriptionPages == 0 ? SentenceSources.MainText
                : descriptionPages == sampleProducts ? SentenceSources.Description
                : SentenceSources.Mixed;

            var codes = new List<string>();
            if (sampleProducts < options.MinSampleProducts)
            {
                codes.Add(VersionCodes.SampleInsufficient);
            }

            if (labels.Count == 0 || languageShare.OrderByDescending(s => s.Value).First().Key == "und")
            {
                codes.Add(VersionCodes.LanguageUnknown);
            }

            if (version.ProductCount is null)
            {
                codes.Add(VersionCodes.ProductCountUnknown);
            }

            results.Add(new VersionLanguage
            {
                Key = version.Key,
                Language = version.Language,
                IsMain = version.IsMain,
                LanguageShare = languageShare,
                SampleProducts = sampleProducts,
                ProductCount = version.ProductCount,
                Codes = codes,
                Warnings = foreign.Count > 0 ? [VersionCodes.UntranslatedText] : [],
                UntranslatedExamples = foreign.Select(x => x.Page.Url).Take(5).ToList(),
                Basis = basis,
                DescriptionPages = descriptionPages,
                LabeledProducts = labeled.Count,
                TranslatedProducts = translated,
                ForeignTextProducts = foreign.Count,
                ForeignTextLanguage = foreign.GroupBy(x => x.Language).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key,
                LanguageFragments = version.Fragments,
            });
        }

        return results;
    }

    /// <summary>Sentences of a main text: blocks split at the end of a sentence.</summary>
    public static IReadOnlyList<string> Sentences(string mainText) =>
        mainText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(block => SentenceEnd().Split(block.Trim()))
            .Select(s => s.Trim())
            .Where(s => s.Length >= 3)
            .ToList();

    /// <summary>
    /// The sentences of a version for the model: at most <paramref name="count"/> sentences of the given length from the first
    /// <paramref name="products"/> product pages that have one (the sample is random), one sentence of each page in turn, so
    /// that every such product gets its language (two sentences each with the default 20 products and 40 sentences).
    /// </summary>
    public static IReadOnlyList<LanguageFragment> Fragments(VersionSample version, int products, int count, int minChars, int maxChars)
    {
        ArgumentNullException.ThrowIfNull(version);
        var fragments = new List<LanguageFragment>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queues = version.Products
            .Select(p => (p.Url, Queue: new Queue<string>(p.Sentences.Where(s => s.Length >= minChars && s.Length <= maxChars))))
            .Where(q => q.Queue.Count > 0)
            .Take(products)
            .ToList();
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

    /// <summary>The language of more than half of the labels of a page; null without such a majority (a tie is no language).</summary>
    private static string? Majority(IReadOnlyList<string>? labels) =>
        labels is null || labels.Count == 0 ? null : labels.GroupBy(l => l).FirstOrDefault(g => g.Count() * 2 > labels.Count)?.Key;

    [GeneratedRegex(@"(?<=[.!?…])\s+(?=[\p{Lu}\d„""])")]
    private static partial Regex SentenceEnd();
}
