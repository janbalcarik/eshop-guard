using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Entities.Ref;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Iam;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", Schemas.Iam);
        builder.HasKey(x => x.Id);
        builder.HasOne<Locale>().WithMany().HasForeignKey(x => x.Locale).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    }
}
