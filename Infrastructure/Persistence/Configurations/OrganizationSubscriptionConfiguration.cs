using Deskovia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deskovia.Infrastructure.Persistence.Configurations;

public sealed class OrganizationSubscriptionConfiguration : IEntityTypeConfiguration<OrganizationSubscription>
{
    public void Configure(EntityTypeBuilder<OrganizationSubscription> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("OrganizationSubscriptions", table =>
        {
            table.HasCheckConstraint("CK_OrganizationSubscriptions_Price", "[MonthlyPrice] >= 0");
            table.HasCheckConstraint("CK_OrganizationSubscriptions_Currency", "LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%'");
            table.HasCheckConstraint("CK_OrganizationSubscriptions_Period", "([CurrentPeriodStartsAtUtc] IS NULL AND [CurrentPeriodEndsAtUtc] IS NULL) OR ([CurrentPeriodStartsAtUtc] IS NOT NULL AND [CurrentPeriodEndsAtUtc] IS NOT NULL AND [CurrentPeriodEndsAtUtc] > [CurrentPeriodStartsAtUtc])");
            table.HasCheckConstraint("CK_OrganizationSubscriptions_Status", "[Status] IN ('PendingPayment','Active','Expired','Cancelled')");
            table.HasCheckConstraint("CK_OrganizationSubscriptions_ActivePeriod", "[Status] <> 'Active' OR ([CurrentPeriodStartsAtUtc] IS NOT NULL AND [CurrentPeriodEndsAtUtc] IS NOT NULL)");
        });
        builder.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        builder.HasIndex(x => x.OrganizationId).IsUnique();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.MonthlyPrice).HasPrecision(12, 2);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.ProviderCustomerId).HasMaxLength(200);
        builder.Property(x => x.ProviderSubscriptionId).HasMaxLength(200);
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Version).IsRowVersion();
    }
}
