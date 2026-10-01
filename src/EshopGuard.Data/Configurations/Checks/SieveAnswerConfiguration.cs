using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Iam;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Checks;

internal sealed class SieveAnswerConfiguration : IEntityTypeConfiguration<SieveAnswer>
{
    public void Configure(EntityTypeBuilder<SieveAnswer> builder)
    {
        builder.ToTable("sieve_answers", Schemas.Checks);
        builder.HasKey(x => new { x.TenantId, x.QuestionSetHash, x.ChunkHash });
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
