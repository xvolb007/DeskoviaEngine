using Deskovia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deskovia.Infrastructure.Persistence.Configurations;

public sealed class ReservationEventConfiguration : IEntityTypeConfiguration<ReservationEvent>
{
    public void Configure(EntityTypeBuilder<ReservationEvent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("ReservationEvents", table =>
        {
            table.HasCheckConstraint("CK_ReservationEvents_EventType", "[EventType] IN ('Created','Confirmed','Completed','Cancelled')");
        });
        builder.Property(x => x.EventType).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.ReservationId, x.OccurredAtUtc });
        builder.HasOne<Reservation>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ReservationId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationMember>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ActorMemberId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
