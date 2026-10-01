using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Content;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Fixes;

internal sealed class PublicationConfiguration : IEntityTypeConfiguration<Publication>
{
    public void Configure(EntityTypeBuilder<Publication> builder)
    {
        builder.ToTable("publications", Schemas.Fixes);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasEnum(x => x.Status);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasTenantForeignKey<Shop>(nameof(Publication.ShopId));
        builder.HasTenantForeignKey<Connector>(nameof(Publication.ConnectorId));
        builder.HasPartitionedTenantForeignKey<Page>(nameof(Publication.PageId));
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
