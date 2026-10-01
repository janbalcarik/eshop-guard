using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Entities.Iam;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Fixes;

internal sealed class EvidenceItemConfiguration : IEntityTypeConfiguration<EvidenceItem>
{
    public void Configure(EntityTypeBuilder<EvidenceItem> builder)
    {
        builder.ToTable("evidence_items", Schemas.Fixes);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasEnum(x => x.SubjectKind);
        builder.HasEnum(x => x.Kind);
        builder.HasEnum(x => x.Source);
        builder.HasEnum(x => x.Status);
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
