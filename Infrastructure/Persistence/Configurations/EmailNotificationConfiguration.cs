using Deskovia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deskovia.Infrastructure.Persistence.Configurations;

public sealed class EmailNotificationConfiguration : IEntityTypeConfiguration<EmailNotification>
{
    public void Configure(EntityTypeBuilder<EmailNotification> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("EmailNotifications", table =>
        {
            table.HasCheckConstraint("CK_EmailNotifications_Attempts", "[AttemptCount] >= 0");
            table.HasCheckConstraint("CK_EmailNotifications_Kind", "[Kind] IN ('ReservationCreated','ReservationReminder','ReservationCancelled')");
            table.HasCheckConstraint("CK_EmailNotifications_Status", "[Status] IN ('Pending','Processing','Sent','Failed','Cancelled')");
            table.HasCheckConstraint("CK_EmailNotifications_Sent", "([Status] = 'Sent' AND [SentAtUtc] IS NOT NULL) OR ([Status] <> 'Sent' AND [SentAtUtc] IS NULL)");
        });
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.RecipientEmail).HasMaxLength(320);
        builder.Property(x => x.DeduplicationKey).HasMaxLength(200);
        builder.Property(x => x.Subject).HasMaxLength(300);
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.HasIndex(x => new { x.OrganizationId, x.DeduplicationKey }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.ScheduledAtUtc });
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasOne<OrganizationMember>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.MemberId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Reservation>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ReservationId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
