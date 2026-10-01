using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Content;

internal sealed class PageVersionConfiguration : IEntityTypeConfiguration<PageVersion>
{
    public void Configure(EntityTypeBuilder<PageVersion> builder)
    {
        builder.ToTable("page_versions", Schemas.Content);
        builder.HasKey(x => new { x.ShopId, x.Id });
        builder.HasAlternateKey(x => new { x.TenantId, x.ShopId, x.Id });
        builder.HasIndex(x => new { x.ShopId, x.PageId }).IsUnique().HasFilter("is_current");
        builder.HasIndex(x => new { x.ShopId, x.PageId, x.FetchedAt });
        builder.HasIndex(x => x.SegmentHashes).HasFilter("is_current").HasMethod("gin");
        builder.HasPartitionedTenantForeignKey<Page>(nameof(PageVersion.PageId));
        builder.HasTenantForeignKey<Run>(nameof(PageVersion.RunId));
    }
}
