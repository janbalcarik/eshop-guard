using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Shops;

internal sealed class PageProfileConfiguration : IEntityTypeConfiguration<PageProfile>
{
    public void Configure(EntityTypeBuilder<PageProfile> builder)
    {
        builder.ToTable("page_profiles", Schemas.Shops);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasIndex(x => new { x.ShopId, x.Number }).IsUnique();
        builder.HasTenantForeignKey<Shop>(nameof(PageProfile.ShopId));
        builder.HasTenantForeignKey<Run>(nameof(PageProfile.CreatedRunId));
    }
}
