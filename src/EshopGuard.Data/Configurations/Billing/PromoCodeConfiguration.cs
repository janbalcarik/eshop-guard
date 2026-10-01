using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Entities.Iam;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Billing;

internal sealed class PromoCodeConfiguration : IEntityTypeConfiguration<PromoCode>
{
    public void Configure(EntityTypeBuilder<PromoCode> builder)
    {
        builder.ToTable("promo_codes", Schemas.Billing);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Percent).HasPrecision(5, 2);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
