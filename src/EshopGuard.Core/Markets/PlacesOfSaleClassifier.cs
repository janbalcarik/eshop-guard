using EshopGuard.Core.Languages;

namespace EshopGuard.Core.Markets;

/// <summary>
/// Turns the verified answer into the places of sale (change 7, design section 3.5): the strength of the model with two
/// safeguards that never read the text (a country whose every quote is the general delivery to the EU is at most generic; an
/// own language version in the language of the market or a version on a domain of the market raises it to strong), supported
/// and preselected countries, and the home country from a quote, otherwise from the domain with a confirmation, otherwise none.
/// </summary>
public static class PlacesOfSaleClassifier
{
    /// <param name="sales">The verified answer; null when the model was not called or failed (<paramref name="failureCode"/>).</param>
    /// <param name="versions">Versions found and tried (only those not waiting for a confirmation raise the strength).</param>
    public static PlacesOfSaleResult Classify(
        VerifiedSales? sales, MarketCatalog catalog, Uri site, IReadOnlyList<LanguageVersionCandidate> versions, string? failureCode = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(site);
        var codes = new List<string>();
        if (failureCode is not null)
        {
            codes.Add(failureCode);
        }

        var countries = new List<CountryEvidence>();
        foreach (var country in sales?.Countries ?? [])
        {
            var level = country.EvidenceLevel is EvidenceLevels.Strong or EvidenceLevels.Delivery ? country.EvidenceLevel : EvidenceLevels.Generic;
            if (sales!.EuWideDelivery is { } euWide
                && country.Evidence.All(q => QuoteVerifier.Normalize(q.Quote) == QuoteVerifier.Normalize(euWide.Quote)))
            {
                level = EvidenceLevels.Generic;
            }

            var market = catalog.ByCountry(country.Country);
            var raisedBy = market is null || level == EvidenceLevels.Strong ? null : RaisingVersion(market, versions);
            countries.Add(Row(country.Country, raisedBy is null ? level : EvidenceLevels.Strong, market, catalog) with
            {
                Evidence = country.Evidence,
                RaisedBy = raisedBy,
                InternalReason = country.Reason,
            });
        }

        string? home = null;
        string? basis = null;
        var needsConfirmation = false;
        IReadOnlyList<VerifiedQuote> homeEvidence = [];
        if (sales?.HomeCountry is { } quoted)
        {
            (home, basis, homeEvidence) = (quoted, HomeBases.Quote, sales.HomeEvidence);
        }
        else if (catalog.ByHost(site.Host) is { } fromDomain)
        {
            (home, basis, needsConfirmation) = (fromDomain.Country, HomeBases.DomainTld, true);
            homeEvidence = [new VerifiedQuote("tld=" + fromDomain.Tlds.First(t => site.Host.EndsWith("." + t, StringComparison.OrdinalIgnoreCase)), "signal")];
            codes.Add(MarketCodes.HomeCountryFromDomain);
        }
        else
        {
            codes.Add(MarketCodes.HomeCountryUnknown);
        }

        if (home is not null)
        {
            var index = countries.FindIndex(c => c.Country == home);
            if (index >= 0)
            {
                countries[index] = countries[index] with { IsHome = true };
            }
            else
            {
                // The seat (or the domain) of the operator is strong evidence of its own market (architecture part 12).
                countries.Insert(0, Row(home, EvidenceLevels.Strong, catalog.ByCountry(home), catalog) with { IsHome = true, Evidence = homeEvidence });
            }
        }

        if (sales is not null && sales.DeliveryTerms is null)
        {
            codes.Add(MarketCodes.DeliveryTermsNotFound);
        }

        if (sales?.QuotesDropped > 0)
        {
            codes.Add(MarketCodes.QuotesDropped);
        }

        return new PlacesOfSaleResult
        {
            HomeCountry = home,
            HomeBasis = basis,
            HomeNeedsConfirmation = needsConfirmation,
            HomeEvidence = homeEvidence,
            Countries = countries.OrderByDescending(c => c.IsHome).ThenBy(c => QuoteVerifier.LevelRank(c.EvidenceLevel)).ThenBy(c => c.Country, StringComparer.Ordinal).ToList(),
            Rejected = sales?.Rejected ?? [],
            EuWideDelivery = sales?.EuWideDelivery,
            DeliveryTerms = sales?.DeliveryTerms,
            QuotesVerified = sales?.QuotesVerified ?? 0,
            QuotesDropped = sales?.QuotesDropped ?? 0,
            Codes = codes,
            InternalUncertain = sales?.Uncertain ?? "",
        };
    }

    /// <summary>
    /// The same result against another catalog of markets (a market added later, <c>Odteraz kontrolujeme aj …</c>): supported
    /// and preselected are decided again, nothing is downloaded and no model is called.
    /// </summary>
    public static PlacesOfSaleResult Reevaluate(PlacesOfSaleResult result, MarketCatalog catalog) => result with
    {
        Countries = result.Countries.Select(c => Row(c.Country, c.EvidenceLevel, catalog.ByCountry(c.Country), catalog) with
        {
            IsHome = c.IsHome, Evidence = c.Evidence, RaisedBy = c.RaisedBy, InternalReason = c.InternalReason,
        }).ToList(),
    };

    private static CountryEvidence Row(string country, string level, MarketDefinition? market, MarketCatalog catalog)
    {
        var supported = catalog.IsSupportedCountry(country);
        return new CountryEvidence
        {
            Country = country,
            Market = market?.Code,
            EvidenceLevel = level,
            Supported = supported,
            Preselected = supported && level is EvidenceLevels.Strong or EvidenceLevels.Delivery,
        };
    }

    /// <summary>A version that shows the shop targets the market: its own language, or a domain of the market (not waiting for a confirmation).</summary>
    private static string? RaisingVersion(MarketDefinition market, IReadOnlyList<LanguageVersionCandidate> versions)
    {
        foreach (var version in versions.Where(v => v.Status is VersionStatus.Active))
        {
            if (LanguageTags.SamePrimary(version.Language, market.Language))
            {
                return $"version={version.Language}";
            }

            if (version.SwitchMethod == SwitchMethods.Domain && Uri.TryCreate(version.BaseUrl, UriKind.Absolute, out var url)
                && market.Tlds.Any(t => url.Host.EndsWith("." + t, StringComparison.OrdinalIgnoreCase)))
            {
                return $"version_domain={url.Host}";
            }
        }

        return null;
    }
}
