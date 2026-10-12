using Deskovia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deskovia.Infrastructure.Persistence.Configurations;

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("UserSessions", table =>
        {
            table.HasCheckConstraint("CK_UserSessions_Lifetime", "[ExpiresAtUtc] > [CreatedAtUtc]");
        });
        builder.HasIndex(x => new { x.MemberId, x.ExpiresAtUtc });
        builder.HasOne<OrganizationMember>().WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
    }
}
