using EshopGuard.Api.Problems;
using EshopGuard.Application.Problems;

namespace EshopGuard.Api.Contracts;

/// <summary>A draft of a price list for a market and currency, a copy of <c>CopyFrom</c> or of the active one.</summary>
public sealed record CreatePriceListRequest(string? MarketCode, string? Currency, string? Name, Guid? CopyFrom) : IValidatableRequest
{
    public void Validate(ValidationResult result)
    {
        TokenRequest.Require(result, "marketCode", MarketCode);
        if (Currency is not { Length: 3 })
        {
            result.Add("currency", ProblemCodes.Fields.ValueNotAllowed);
        }
    }
}

public sealed record PriceTierRequest(string? Code, int? MinProducts, int? MaxProducts, decimal? AnalysisPrice, decimal? MonitoringMonthly, decimal? MonitoringYearly);

public sealed record SetPriceTiersRequest(PriceTierRequest[]? Tiers, int? NoticeDays, decimal? FairUseFactor) : IValidatableRequest
{
    public void Validate(ValidationResult result)
    {
        if (Tiers is not { Length: > 0 } || Tiers.Any(t => string.IsNullOrWhiteSpace(t.Code) || t.MinProducts is null))
        {
            result.Add("tiers", ProblemCodes.Fields.ValueNotAllowed);
        }
    }
}

public sealed record VolumeDiscountRequest(int? FromShopNumber, decimal? Percent);

public sealed record SetVolumeDiscountsRequest(VolumeDiscountRequest[]? Discounts) : IValidatableRequest
{
    public void Validate(ValidationResult result)
    {
        if (Discounts is null || Discounts.Any(d => d.FromShopNumber is null || d.Percent is null))
        {
            result.Add("discounts", ProblemCodes.Fields.ValueNotAllowed);
        }
    }
}

public sealed record PublishPriceListRequest(DateTimeOffset? ValidFrom) : IValidatableRequest
{
    public void Validate(ValidationResult result)
    {
        if (ValidFrom is null)
        {
            result.Add("validFrom", ProblemCodes.Fields.Required);
        }
    }
}

/// <summary>An order from the quote the customer confirmed (its <c>scopeHash</c>) and the version of the terms he accepted.</summary>
public sealed record CreateOrderRequest(Guid? QuoteId, string? ScopeHash, string? TermsVersion) : IValidatableRequest
{
    public void Validate(ValidationResult result)
    {
        if (QuoteId is null)
        {
            result.Add("quoteId", ProblemCodes.Fields.Required);
        }

        TokenRequest.Require(result, "scopeHash", ScopeHash);
        TokenRequest.Require(result, "termsVersion", TermsVersion);
    }
}

/// <summary>
/// The billing details of the tenant (screen 8b); the fields are checked by <c>BillingDetailsService</c>, the change goes only
/// over the <c>Version</c> the client has read.
/// </summary>
public sealed record UpdateBillingDetailsRequest(
    string? LegalName, string? Ico, string? Dic, string? IcDph, string? Street, string? PostalCode, string? City, string? CountryCode,
    string? BillingEmail, uint? Version);

/// <summary>
/// Monitoring started again: without <c>Confirm</c> the answer is <c>confirm_required</c> with the amount and the date; the
/// subscription is created only with <c>Confirm = true</c> and the <c>Amount</c> shown to the customer.
/// </summary>
public sealed record StartSubscriptionRequest(bool? Confirm, decimal? Amount);
