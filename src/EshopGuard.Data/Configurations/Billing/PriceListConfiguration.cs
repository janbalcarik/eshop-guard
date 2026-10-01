using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Entities.Ref;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Billing;

internal sealed class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
{
    public void Configure(EntityTypeBuilder<PriceList> builder)
    {
        builder.ToTable("price_lists", Schemas.Billing);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Currency).HasColumnType("character(3)");
        builder.HasEnum(x => x.Status);
        builder.HasIndex(x => new { x.MarketCode, x.ValidFrom });
        builder.HasOne<Market>().WithMany().HasForeignKey(x => x.MarketCode).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    }
}
