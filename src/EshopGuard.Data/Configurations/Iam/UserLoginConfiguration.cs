using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Iam;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Iam;

internal sealed class UserLoginConfiguration : IEntityTypeConfiguration<UserLogin>
{
    public void Configure(EntityTypeBuilder<UserLogin> builder)
    {
        builder.ToTable("user_logins", Schemas.Iam);
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.Provider, x.ProviderKey }).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).HasPrincipalKey(x => x.Id).OnDelete(DeleteBehavior.Cascade);
    }
}
