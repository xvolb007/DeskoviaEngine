using Deskovia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deskovia.Infrastructure.Persistence.Configurations;

public sealed class PaymentWebhookEventConfiguration : IEntityTypeConfiguration<PaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookEvent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("PaymentWebhookEvents", table =>
        {
            table.HasCheckConstraint("CK_PaymentWebhookEvents_Status", "[Status] IN ('Pending','Processed','Failed')");
        });
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Provider).HasMaxLength(50);
        builder.Property(x => x.ProviderEventId).HasMaxLength(200);
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.HasIndex(x => new { x.Provider, x.ProviderEventId }).IsUnique();
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Payment>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PaymentId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
