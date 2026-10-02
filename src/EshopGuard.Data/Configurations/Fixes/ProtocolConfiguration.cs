using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Fixes;

internal sealed class ProtocolConfiguration : IEntityTypeConfiguration<Protocol>
{
    public void Configure(EntityTypeBuilder<Protocol> builder)
    {
        builder.ToTable("protocols", Schemas.Fixes);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
        builder.HasEnum(x => x.Status);
        builder.HasTenantForeignKey<Shop>(nameof(Protocol.ShopId));
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.GeneratedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
