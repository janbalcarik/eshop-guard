namespace EshopGuard.Core.Markets;

/// <summary>A quote that was found: on its page (<see cref="Source"/> = URL), or a technical signal (<c>signal</c>).</summary>
/// <param name="SourceCorrected">The quote is not on the page the model named but word for word on another page given to it.</param>
public sealed record VerifiedQuote(string Quote, string Source, bool SourceCorrected = false);

/// <summary>A country with its verified evidence, as the model leveled it.</summary>
public sealed record VerifiedCountry(string Country, string EvidenceLevel, IReadOnlyList<VerifiedQuote> Evidence, string Reason);

/// <summary>A country the model named without any verified evidence.</summary>
public sealed record RejectedCountry(string Country, string Code);

/// <summary>The answer of the model after the verification of every quote.</summary>
public sealed record VerifiedSales
{
    public string? HomeCountry { get; init; }

    public IReadOnlyList<VerifiedQuote> HomeEvidence { get; init; } = [];

    public IReadOnlyList<VerifiedCountry> Countries { get; init; } = [];

    public IReadOnlyList<RejectedCountry> Rejected { get; init; } = [];

    /// <summary>The verified quote of a general delivery to the EU, or null.</summary>
    public VerifiedQuote? EuWideDelivery { get; init; }

    /// <summary>The verified quote of delivery terms, or null when no page shows them.</summary>
    public VerifiedQuote? DeliveryTerms { get; init; }

    /// <summary>Versions named by the model with a verified quote or signal.</summary>
    public IReadOnlyList<VersionAnswer> LanguageVersions { get; init; } = [];

    public int QuotesVerified { get; init; }

    public int QuotesDropped { get; init; }

    /// <summary>What the model could not decide (internal audit only).</summary>
    public string Uncertain { get; init; } = "";
}

/// <summary>
/// Verifies every quote of the model (change 7, design section 3.4): a passage must be in the text of its page after spaces,
/// letter case and outer quotation marks are unified; a passage found word for word on another page given to the model is
/// accepted with the corrected source; a signal <c>name=value</c> must be among the technical signs. Anything else is dropped,
/// and a country left without evidence is rejected with <c>no_verified_evidence</c>.
/// </summary>
public static class QuoteVerifier
{
    /// <summary>Code of a country without verified evidence.</summary>
    public const string NoVerifiedEvidence = "no_verified_evidence";

    private const string SignalSource = "signal";

    private static readonly char[] OuterQuotes = ['"', '\'', '„', '“', '”', '‚', '‘', '’', '«', '»', '‹', '›', ' '];

    /// <summary>Verifies the answer against the texts of the pages (URL → text) and the technical signs.</summary>
    public static VerifiedSales Verify(SalesAnswer answer, IReadOnlyDictionary<string, string> pages, MarketSignals signals)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var normalized = pages.ToDictionary(p => p.Key, p => Normalize(p.Value), StringComparer.Ordinal);
        var signalValues = signals.Values().Select(v => $"{v.Name}={v.Value}".ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        var verified = 0;
        var dropped = 0;

        VerifiedQuote? Check(ModelQuote? quote)
        {
            if (quote is null || quote.Quote.Trim().Length == 0)
            {
                return null;
            }

            var result = VerifyOne(quote, normalized, signalValues);
            if (result is null)
            {
                dropped++;
            }
            else
            {
                verified++;
            }

            return result;
        }

        List<VerifiedQuote> CheckAll(IEnumerable<ModelQuote> quotes) => quotes.Select(Check).OfType<VerifiedQuote>().ToList();

        var homeEvidence = CheckAll(answer.HomeEvidence);
        var countries = new List<VerifiedCountry>();
        var rejected = new List<RejectedCountry>();
        foreach (var group in answer.Countries.GroupBy(c => c.Country.Trim().ToUpperInvariant()))
        {
            var evidence = CheckAll(group.SelectMany(c => c.Evidence));
            var level = group.Select(c => c.EvidenceLevel).OrderBy(LevelRank).First();
            if (group.Key.Length != 2 || !group.Key.All(char.IsAsciiLetterUpper) || evidence.Count == 0)
            {
                rejected.Add(new RejectedCountry(group.Key, NoVerifiedEvidence));
                continue;
            }

            countries.Add(new VerifiedCountry(group.Key, level, evidence, string.Join(" ", group.Select(c => c.Reason).Where(r => r.Length > 0))));
        }

        var home = answer.HomeCountry.Trim().ToUpperInvariant();
        var versions = answer.LanguageVersions.Where(v => v.Evidence is not null && Check(v.Evidence) is not null).ToList();
        return new VerifiedSales
        {
            HomeCountry = home.Length == 2 && homeEvidence.Count > 0 ? home : null,
            HomeEvidence = homeEvidence,
            Countries = countries,
            Rejected = rejected,
            EuWideDelivery = answer.EuWideDelivery.Value ? Check(answer.EuWideDelivery.Evidence) : null,
            DeliveryTerms = answer.DeliveryTerms.Value ? Check(answer.DeliveryTerms.Evidence) : null,
            LanguageVersions = versions,
            QuotesVerified = verified,
            QuotesDropped = dropped,
            Uncertain = answer.Uncertain,
        };
    }

    /// <summary>Spaces unified, lower case, outer quotation marks removed (as the research scripts of 1. 10. 2026).</summary>
    public static string Normalize(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim(OuterQuotes).ToLowerInvariant();

    /// <summary>Order of the levels from the strongest (unknown levels count as general).</summary>
    public static int LevelRank(string level) => level switch
    {
        "strong" => 0,
        "delivery" => 1,
        _ => 2,
    };

    private static VerifiedQuote? VerifyOne(ModelQuote quote, Dictionary<string, string> pages, HashSet<string> signals)
    {
        var text = quote.Quote.Trim();
        if (string.Equals(quote.Source, SignalSource, StringComparison.OrdinalIgnoreCase) || text.StartsWith("signal:", StringComparison.OrdinalIgnoreCase))
        {
            var value = (text.StartsWith("signal:", StringComparison.OrdinalIgnoreCase) ? text[7..] : text).Trim().ToLowerInvariant();
            return signals.Contains(value) ? new VerifiedQuote(value, SignalSource) : null;
        }

        var needle = Normalize(text);
        if (needle.Length == 0)
        {
            return null;
        }

        var source = Key(quote.Source);
        if (source is not null && pages.TryGetValue(source, out var page) && page.Contains(needle, StringComparison.Ordinal))
        {
            return new VerifiedQuote(text, source);
        }

        var other = pages.FirstOrDefault(p => p.Value.Contains(needle, StringComparison.Ordinal));
        return other.Key is null ? null : new VerifiedQuote(text, other.Key, SourceCorrected: true);
    }

    private static string? Key(string url) =>
        Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ? Crawl.UrlTools.Normalize(uri).AbsoluteUri : null;
}
