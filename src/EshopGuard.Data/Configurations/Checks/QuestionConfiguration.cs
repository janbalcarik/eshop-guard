using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Checks;

internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("questions", Schemas.Checks);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasEnum(x => x.Scope);
        builder.HasEnum(x => x.Status);
        builder.HasIndex(x => new { x.TenantId, x.ShopId, x.Status });
        builder.HasTenantForeignKey<Shop>(nameof(Question.ShopId));
        builder.HasTenantForeignKey<Finding>(nameof(Question.FindingId));
        builder.HasTenantForeignKey<EvidenceItem>(nameof(Question.EvidenceId));
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.AnsweredBy).OnDelete(DeleteBehavior.Restrict);
    }
}
