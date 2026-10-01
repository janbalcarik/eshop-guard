using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Shops;

/// <summary>Template profile of an e-shop (which regions of a page are the shop's own text). Table <c>shop.page_profiles</c>.</summary>
public sealed class PageProfile : TenantEntity
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    public required string PromptVersion { get; set; }

    public required string Model { get; set; }

    public required JsonDocument Regions { get; set; }

    public required JsonDocument SampleUrls { get; set; }

    public Guid? CreatedRunId { get; set; }

    public DateTimeOffset? RetiredAt { get; set; }
}
