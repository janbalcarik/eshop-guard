using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Entities.Ref;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Iam;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants", Schemas.Iam);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Currency).HasColumnType("character(3)");
        builder.HasEnum(x => x.Status);
        builder.HasEnum(x => x.PartnerKind);
        builder.HasEnum(x => x.TaxIdStatus).HasDefaultValueSql("'none'");
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasOne<Market>().WithMany().HasForeignKey(x => x.MarketCode).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Locale>().WithMany().HasForeignKey(x => x.Locale).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    }
}
