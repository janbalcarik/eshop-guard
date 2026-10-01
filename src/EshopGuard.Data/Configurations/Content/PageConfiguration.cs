using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Content;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Content;

internal sealed class PageConfiguration : IEntityTypeConfiguration<Page>
{
    public void Configure(EntityTypeBuilder<Page> builder)
    {
        builder.ToTable("pages", Schemas.Content);
        builder.HasKey(x => new { x.ShopId, x.Id });
        builder.HasAlternateKey(x => new { x.TenantId, x.ShopId, x.Id });
        builder.HasEnum(x => x.PageType);
        builder.HasEnum(x => x.Source);
        builder.HasEnum(x => x.Status);
        builder.HasIndex(x => new { x.ShopId, x.UrlHash }).IsUnique();
        builder.HasIndex(x => new { x.ShopId, x.NextCheckAt });
        builder.ToTable(t => t.HasCheckConstraint("ck_pages_rotation_bucket", "rotation_bucket BETWEEN 0 AND 6"));
        builder.HasTenantForeignKey<Shop>(nameof(Page.ShopId));
        builder.HasTenantForeignKey<PageProfile>(nameof(Page.ProfileId));
        builder.HasPartitionedTenantForeignKey<PageVersion>(nameof(Page.CurrentVersionId));
    }
}
