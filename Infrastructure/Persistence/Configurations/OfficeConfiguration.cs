using Deskovia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deskovia.Infrastructure.Persistence.Configurations;

public sealed class OfficeConfiguration : IEntityTypeConfiguration<Office>
{
    public void Configure(EntityTypeBuilder<Office> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        builder.ToTable("Offices", table => table.HasCheckConstraint(
            "CK_Offices_BookingHorizonDays", "[BookingHorizonDays] >= 0"));
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
