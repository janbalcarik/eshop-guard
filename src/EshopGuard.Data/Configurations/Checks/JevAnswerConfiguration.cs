using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Iam;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Checks;

internal sealed class JevAnswerConfiguration : IEntityTypeConfiguration<JevAnswer>
{
    public void Configure(EntityTypeBuilder<JevAnswer> builder)
    {
        builder.ToTable("jev_answers", Schemas.Checks);
        builder.HasKey(x => new { x.TenantId, x.CacheKey });
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
