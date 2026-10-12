using Deskovia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deskovia.Infrastructure.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("Users", t => t.HasCheckConstraint("CK_Users_ExternalIdentity", "([MicrosoftTenantId] IS NULL AND [MicrosoftObjectId] IS NULL) OR ([MicrosoftTenantId] IS NOT NULL AND [MicrosoftObjectId] IS NOT NULL)"));
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.DisplayName).HasMaxLength(200);
        builder.HasIndex(x => new { x.MicrosoftTenantId, x.MicrosoftObjectId }).IsUnique().HasFilter("[MicrosoftTenantId] IS NOT NULL AND [MicrosoftObjectId] IS NOT NULL");
        builder.Property(x => x.NormalizedEmail).HasMaxLength(320);
        builder.HasIndex(x => x.NormalizedEmail).IsUnique();
    }
}
