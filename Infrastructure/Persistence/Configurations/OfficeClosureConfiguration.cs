using Deskovia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deskovia.Infrastructure.Persistence.Configurations;

public sealed class OfficeClosureConfiguration : IEntityTypeConfiguration<OfficeClosure>
{
    public void Configure(EntityTypeBuilder<OfficeClosure> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("OfficeClosures", table =>
        {
            table.HasCheckConstraint("CK_OfficeClosures_Interval", "[EndsAtUtc] > [StartsAtUtc]");
        });
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.OfficeId, x.StartsAtUtc, x.EndsAtUtc });
        builder.HasOne<Office>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.OfficeId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Room>().WithMany().HasForeignKey(x => new { x.OfficeId, x.RoomId }).HasPrincipalKey(x => new { x.OfficeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationMember>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.CreatedByMemberId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
