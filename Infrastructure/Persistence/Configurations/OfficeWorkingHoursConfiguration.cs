using Deskovia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deskovia.Infrastructure.Persistence.Configurations;

public sealed class OfficeWorkingHoursConfiguration : IEntityTypeConfiguration<OfficeWorkingHours>
{
    public void Configure(EntityTypeBuilder<OfficeWorkingHours> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("OfficeWorkingHours", table =>
        {
            table.HasCheckConstraint("CK_OfficeWorkingHours_Day", "[DayOfWeek] BETWEEN 0 AND 6");
            table.HasCheckConstraint("CK_OfficeWorkingHours_Hours", "[ClosesAt] > [OpensAt]");
        });
        builder.HasIndex(x => new { x.OfficeId, x.DayOfWeek }).IsUnique();
        builder.HasOne<Office>().WithMany().HasForeignKey(x => x.OfficeId).OnDelete(DeleteBehavior.Restrict);
    }
}
