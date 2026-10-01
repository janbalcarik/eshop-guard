using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Ops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Ops;

internal sealed class WorkerNodeConfiguration : IEntityTypeConfiguration<WorkerNode>
{
    public void Configure(EntityTypeBuilder<WorkerNode> builder)
    {
        builder.ToTable("workers", Schemas.Ops);
        builder.HasKey(x => x.Id);
    }
}
