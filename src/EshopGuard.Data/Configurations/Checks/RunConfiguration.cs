using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Checks;

internal sealed class RunConfiguration : IEntityTypeConfiguration<Run>
{
    public void Configure(EntityTypeBuilder<Run> builder)
    {
        builder.ToTable("runs", Schemas.Checks);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasEnum(x => x.Kind);
        builder.HasEnum(x => x.Trigger);
        builder.HasEnum(x => x.Status);
        builder.HasIndex(x => new { x.TenantId, x.ShopId, x.CreatedAt }).IsDescending(false, false, true);
        builder.HasTenantForeignKey<Shop>(nameof(Run.ShopId));
        builder.HasTenantForeignKey<Order>(nameof(Run.OrderId));
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
