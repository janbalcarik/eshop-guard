using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Shops;

internal sealed class FreeSampleClaimConfiguration : IEntityTypeConfiguration<FreeSampleClaim>
{
    public void Configure(EntityTypeBuilder<FreeSampleClaim> builder)
    {
        builder.ToTable("free_sample_claims", Schemas.Shops);
        builder.HasKey(x => x.Domain);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasTenantForeignKey<Shop>(nameof(FreeSampleClaim.ShopId));
    }
}
