using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Content;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Fixes;

internal sealed class EvidenceLinkConfiguration : IEntityTypeConfiguration<EvidenceLink>
{
    public void Configure(EntityTypeBuilder<EvidenceLink> builder)
    {
        builder.ToTable("evidence_links", Schemas.Fixes);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasTenantForeignKey<EvidenceItem>(nameof(EvidenceLink.EvidenceId));
        builder.HasTenantForeignKey<Shop>(nameof(EvidenceLink.ShopId));
        builder.HasPartitionedTenantForeignKey<Page>(nameof(EvidenceLink.PageId));
        builder.HasTenantForeignKey<Finding>(nameof(EvidenceLink.FindingId));
    }
}
