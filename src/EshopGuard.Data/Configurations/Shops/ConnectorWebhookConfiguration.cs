using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Shops;

internal sealed class ConnectorWebhookConfiguration : IEntityTypeConfiguration<ConnectorWebhook>
{
    public void Configure(EntityTypeBuilder<ConnectorWebhook> builder)
    {
        builder.ToTable("connector_webhooks", Schemas.Shops);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasTenantForeignKey<Connector>(nameof(ConnectorWebhook.ConnectorId));
    }
}
