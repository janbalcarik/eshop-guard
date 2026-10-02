using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Billing;

internal sealed class PriceQuoteConfiguration : IEntityTypeConfiguration<PriceQuote>
{
    public void Configure(EntityTypeBuilder<PriceQuote> builder)
    {
        builder.ToTable("price_quotes", Schemas.Billing);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.Property(x => x.Currency).HasColumnType("character(3)");
        builder.Property(x => x.AnalysisPrice).HasPrecision(12, 2);
        builder.Property(x => x.MonitoringMonthly).HasPrecision(12, 2);
        builder.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        builder.HasEnum(x => x.Status);

        // One record per e-shop, scope and price list; without a price list (unavailable) too (NULLS NOT DISTINCT).
        builder.HasIndex(x => new { x.ShopId, x.ScopeHash, x.PriceListId }).IsUnique().AreNullsDistinct(false);
        builder.HasTenantForeignKey<Shop>(nameof(PriceQuote.ShopId));
        builder.HasTenantForeignKey<Run>(nameof(PriceQuote.BasisRunId));
        builder.HasOne<PriceList>().WithMany().HasForeignKey(x => x.PriceListId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
