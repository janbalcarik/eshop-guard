using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Shops;

internal sealed class ConnectorConfiguration : IEntityTypeConfiguration<Connector>
{
    public void Configure(EntityTypeBuilder<Connector> builder)
    {
        builder.ToTable("connectors", Schemas.Shops);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasEnum(x => x.Platform);
        builder.HasEnum(x => x.Status);
        builder.HasEnum(x => x.Access);
        builder.HasIndex(x => new { x.TenantId, x.ShopId }).IsUnique();
        builder.HasIndex(x => new { x.Platform, x.ExternalShopId });
        builder.HasTenantForeignKey<Shop>(nameof(Connector.ShopId));
    }
}
