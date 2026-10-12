using Deskovia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deskovia.Infrastructure.Persistence.Configurations;

public sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("OrganizationMembers", table =>
        {
            table.HasCheckConstraint("CK_OrganizationMembers_Role", "[Role] IN ('Employee','Administrator','Owner')");
        });
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(40);
        builder.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        builder.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.OrganizationId).IsUnique().HasFilter("[Role] = 'Owner' AND [IsActive] = 1");
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Version).IsRowVersion();
    }
}
