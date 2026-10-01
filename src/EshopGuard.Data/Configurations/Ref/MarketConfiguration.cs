using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Entities.Ref;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Ref;

internal sealed class MarketConfiguration : IEntityTypeConfiguration<Market>
{
    public void Configure(EntityTypeBuilder<Market> builder)
    {
        builder.ToTable("markets", Schemas.Ref);
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Currency).HasColumnType("character(3)");
        builder.HasEnum(x => x.WebStatus);
        builder.HasEnum(x => x.ChecksStatus);
        builder.HasOne<PriceList>().WithMany().HasForeignKey(x => x.PriceListId).OnDelete(DeleteBehavior.Restrict);
    }
}
