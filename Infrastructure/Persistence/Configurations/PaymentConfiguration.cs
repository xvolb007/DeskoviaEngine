using Deskovia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deskovia.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("Payments", table =>
        {
            table.HasCheckConstraint("CK_Payments_Amount", "[Amount] >= 0");
            table.HasCheckConstraint("CK_Payments_Currency", "LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%'");
            table.HasCheckConstraint("CK_Payments_Period", "[PeriodEndsAtUtc] > [PeriodStartsAtUtc]");
            table.HasCheckConstraint("CK_Payments_Status", "[Status] IN ('Pending','Succeeded','Failed','Refunded')");
            table.HasCheckConstraint("CK_Payments_Paid", "[Status] NOT IN ('Succeeded','Refunded') OR [PaidAtUtc] IS NOT NULL");
        });
        builder.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Amount).HasPrecision(12, 2);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Provider).HasMaxLength(50);
        builder.Property(x => x.ProviderPaymentId).HasMaxLength(200);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200);
        builder.HasIndex(x => new { x.Provider, x.ProviderPaymentId }).IsUnique().HasFilter("[ProviderPaymentId] IS NOT NULL");
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasOne<OrganizationSubscription>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.SubscriptionId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Version).IsRowVersion();
    }
}
