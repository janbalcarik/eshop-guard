using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Content;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Checks;

internal sealed class PageChangeConfiguration : IEntityTypeConfiguration<PageChange>
{
    public void Configure(EntityTypeBuilder<PageChange> builder)
    {
        builder.ToTable("page_changes", Schemas.Checks);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.HasEnum(x => x.Source);
        builder.HasEnum(x => x.ChangeKind);
        builder.HasEnum(x => x.Result);
        builder.HasIndex(x => new { x.TenantId, x.ShopId, x.DetectedAt }).IsDescending(false, false, true);
        builder.HasTenantForeignKey<Shop>(nameof(PageChange.ShopId));
        builder.HasPartitionedTenantForeignKey<Page>(nameof(PageChange.PageId));
        builder.HasTenantForeignKey<Run>(nameof(PageChange.RunId));
    }
}
