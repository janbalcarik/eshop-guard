namespace EshopGuard.Core.Markets;

/// <summary>Strength of the evidence of a country (<c>shop.shop_markets.evidence_level</c>).</summary>
public static class EvidenceLevels
{
    /// <summary>The shop targets the country: own version or domain, currency, seat, local authority in the terms.</summary>
    public const string Strong = "strong";

    /// <summary>Concrete delivery terms for the country (price, carrier).</summary>
    public const string Delivery = "delivery";

    /// <summary>Only a general statement, e.g. delivery to the whole EU.</summary>
    public const string Generic = "generic";
}

/// <summary>Where the home country comes from.</summary>
public static class HomeBases
{
    /// <summary>A verified quote of the seat or address of the operator.</summary>
    public const string Quote = "quote";

    /// <summary>The top-level domain of the site; the client confirms it on 3c.</summary>
    public const string DomainTld = "domain_tld";
}

/// <summary>Codes of the analysis of the places of sale.</summary>
public static class MarketCodes
{
    public const string DeliveryTermsNotFound = "delivery_terms_not_found";
    public const string HomeCountryUnknown = "home_country_unknown";
    public const string HomeCountryFromDomain = "home_country_from_domain";
    public const string QuotesDropped = "quotes_dropped";
    public const string AnalysisFailed = "market_analysis_failed";
    public const string AnalysisNotConfirmed = "market_analysis_not_confirmed";

    /// <summary>The price of new profiles for the comparison of versions was not confirmed; their main text was compared.</summary>
    public const string ProfilesNotConfirmed = "version_profiles_not_confirmed";
}

/// <summary>A country where the shop sells, with its evidence (a row of <c>shop.shop_markets</c>).</summary>
public sealed record CountryEvidence
{
    /// <summary>ISO 3166-1 alpha-2.</summary>
    public required string Country { get; init; }

    /// <summary>Code of the market in the rules (<c>sk</c>), null when the tool does not know the country.</summary>
    public string? Market { get; init; }

    public required string EvidenceLevel { get; init; }

    /// <summary>The tool checks this country (<see cref="MarketCatalog.Supported"/>); others are kept as unsupported and not shown.</summary>
    public bool Supported { get; init; }

    /// <summary>Ticked on 3c in advance: supported with strong or delivery evidence.</summary>
    public bool Preselected { get; init; }

    public bool IsHome { get; init; }

    /// <summary>Verified quotes and signals, in the language of the shop.</summary>
    public IReadOnlyList<VerifiedQuote> Evidence { get; init; } = [];

    /// <summary>A language version or domain of the shop raised the strength to strong.</summary>
    public string? RaisedBy { get; init; }

    /// <summary>The reason of the model (internal audit only, never shown to the client).</summary>
    public string InternalReason { get; init; } = "";
}

/// <summary>The places of sale of a shop: countries with evidence, the home country, rejected countries and codes.</summary>
public sealed record PlacesOfSaleResult
{
    /// <summary>ISO alpha-2 of the home country, or null when it is not known.</summary>
    public string? HomeCountry { get; init; }

    /// <summary><see cref="HomeBases"/>, null without a home country.</summary>
    public string? HomeBasis { get; init; }

    /// <summary>The client confirms the home country on 3c (it comes from the domain).</summary>
    public bool HomeNeedsConfirmation { get; init; }

    public IReadOnlyList<VerifiedQuote> HomeEvidence { get; init; } = [];

    public IReadOnlyList<CountryEvidence> Countries { get; init; } = [];

    public IReadOnlyList<RejectedCountry> Rejected { get; init; } = [];

    /// <summary>Verified quote of a general delivery to the EU, or null.</summary>
    public VerifiedQuote? EuWideDelivery { get; init; }

    /// <summary>Verified quote of the delivery terms, or null.</summary>
    public VerifiedQuote? DeliveryTerms { get; init; }

    public int QuotesVerified { get; init; }

    public int QuotesDropped { get; init; }

    /// <summary>Codes for the client and the failure of the analysis (<see cref="MarketCodes"/>, <c>model_mock</c>, <c>model_missing_key</c>).</summary>
    public IReadOnlyList<string> Codes { get; init; } = [];

    /// <summary>What the model could not decide (internal audit only).</summary>
    public string InternalUncertain { get; init; } = "";
}
