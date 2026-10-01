using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Billing;

internal sealed class PriceTierConfiguration : IEntityTypeConfiguration<PriceTier>
{
    public void Configure(EntityTypeBuilder<PriceTier> builder)
    {
        builder.ToTable("price_tiers", Schemas.Billing);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AnalysisPrice).HasPrecision(12, 2);
        builder.Property(x => x.MonitoringMonthly).HasPrecision(12, 2);
        builder.Property(x => x.MonitoringYearly).HasPrecision(12, 2);
        builder.HasIndex(x => new { x.PriceListId, x.Code }).IsUnique();
        builder.HasOne<PriceList>().WithMany().HasForeignKey(x => x.PriceListId).OnDelete(DeleteBehavior.Restrict);
    }
}
