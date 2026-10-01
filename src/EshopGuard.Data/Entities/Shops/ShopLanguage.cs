using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Shops;

/// <summary>Language version of an e-shop. Table <c>shop.shop_languages</c>.</summary>
public sealed class ShopLanguage : ITenantOwned, IHasTimestamps
{
    public Guid TenantId { get; set; }

    public Guid ShopId { get; set; }

    public required string Language { get; set; }

    public required string BaseUrl { get; set; }

    public LanguageSwitchMethod? SwitchMethod { get; set; }

    public LanguageSource Source { get; set; }

    public ShopLanguageStatus Status { get; set; }

    public float? OwnTextShare { get; set; }

    public JsonDocument? LanguageShare { get; set; }

    public JsonDocument? Comparison { get; set; }

    public Guid? SampleRunId { get; set; }

    public bool Counted { get; set; }

    public int? ProductCount { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
