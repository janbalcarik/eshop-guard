using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Ref;

/// <summary>Market (country): what is ready for it. Table <c>ref.markets</c>.</summary>
public sealed class Market : IHasTimestamps
{
    public required string Code { get; set; }

    public required string CountryCode { get; set; }

    public required string DefaultLocale { get; set; }

    public string[] UiLocales { get; set; } = [];

    public required string Jurisdiction { get; set; }

    public required string Currency { get; set; }

    public Guid? PriceListId { get; set; }

    public MarketWebStatus WebStatus { get; set; }

    public MarketChecksStatus ChecksStatus { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
