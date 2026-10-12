using Deskovia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deskovia.Infrastructure.Persistence.Configurations;

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("Reservations", table =>
        {
            table.HasCheckConstraint("CK_Reservations_Interval", "[EndsAtUtc] > [StartsAtUtc]");
            table.HasCheckConstraint("CK_Reservations_MinimumDuration", "[EndsAtUtc] >= DATEADD(minute, 30, [StartsAtUtc])");
            table.HasCheckConstraint("CK_Reservations_Status", "[Status] IN ('Reserved','Confirmed','Completed','Cancelled')");
            table.HasCheckConstraint("CK_Reservations_Cancellation", "([Status] = 'Cancelled' AND [CancelledAtUtc] IS NOT NULL AND [CancellationReason] IS NOT NULL) OR ([Status] <> 'Cancelled' AND [CancelledAtUtc] IS NULL AND [CancellationReason] IS NULL)");
            table.HasCheckConstraint("CK_Reservations_CancellationReason", "[CancellationReason] IS NULL OR [CancellationReason] IN ('Employee','Administrator','NoShow','OfficeClosed','RoomClosed','DeskArchived','AccountDisabled')");
            table.HasCheckConstraint("CK_Reservations_Completion", "([Status] = 'Completed' AND [CompletedAtUtc] IS NOT NULL AND [ConfirmedAtUtc] IS NOT NULL AND [CompletedAtUtc] >= [StartsAtUtc] AND [CompletedAtUtc] <= [EndsAtUtc]) OR ([Status] <> 'Completed' AND [CompletedAtUtc] IS NULL)");
            table.HasCheckConstraint("CK_Reservations_Confirmation", "([Status] <> 'Confirmed' OR [ConfirmedAtUtc] IS NOT NULL) AND ([Status] <> 'Reserved' OR [ConfirmedAtUtc] IS NULL) AND ([ConfirmedAtUtc] IS NULL OR ([ConfirmedAtUtc] >= DATEADD(hour, -1, [StartsAtUtc]) AND [ConfirmedAtUtc] < DATEADD(minute, 5, [StartsAtUtc])))");
        });
        builder.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.CancellationReason).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.DeskId, x.StartsAtUtc, x.EndsAtUtc });
        builder.HasIndex(x => new { x.MemberId, x.StartsAtUtc, x.EndsAtUtc });
        builder.HasIndex(x => new { x.Status, x.StartsAtUtc });
        builder.HasIndex(x => x.BatchId);
        builder.HasOne<Office>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.OfficeId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Room>().WithMany().HasForeignKey(x => new { x.OfficeId, x.RoomId }).HasPrincipalKey(x => new { x.OfficeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Desk>().WithMany().HasForeignKey(x => new { x.RoomId, x.DeskId }).HasPrincipalKey(x => new { x.RoomId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationMember>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.MemberId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
