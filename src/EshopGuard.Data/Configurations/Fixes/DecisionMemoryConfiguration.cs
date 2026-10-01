using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Fixes;

internal sealed class DecisionMemoryConfiguration : IEntityTypeConfiguration<DecisionMemory>
{
    public void Configure(EntityTypeBuilder<DecisionMemory> builder)
    {
        builder.ToTable("decision_memory", Schemas.Fixes);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasEnum(x => x.Decision);
        builder.HasIndex(x => new { x.TenantId, x.ShopId, x.SegmentHash }).HasFilter("superseded_at IS NULL");
        builder.HasTenantForeignKey<Shop>(nameof(DecisionMemory.ShopId));
        builder.HasTenantForeignKey<EvidenceItem>(nameof(DecisionMemory.EvidenceId));
        builder.HasTenantForeignKey<FixProposal>(nameof(DecisionMemory.SourceProposalId));
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
