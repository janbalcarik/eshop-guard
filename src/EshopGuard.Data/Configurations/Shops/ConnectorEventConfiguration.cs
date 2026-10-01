using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Shops;

internal sealed class ConnectorEventConfiguration : IEntityTypeConfiguration<ConnectorEvent>
{
    public void Configure(EntityTypeBuilder<ConnectorEvent> builder)
    {
        builder.ToTable("connector_events", Schemas.Shops);
        builder.HasKey(x => new { x.Id, x.ReceivedAt });
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.HasIndex(x => new { x.ConnectorId, x.DedupeKey });
        builder.HasTenantForeignKey<Connector>(nameof(ConnectorEvent.ConnectorId));
        builder.HasTenantForeignKey<Shop>(nameof(ConnectorEvent.ShopId));
    }
}
