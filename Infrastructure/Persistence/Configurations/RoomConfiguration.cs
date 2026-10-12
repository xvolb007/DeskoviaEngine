using Deskovia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deskovia.Infrastructure.Persistence.Configurations;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.OfficeId, x.Id });
        builder.ToTable("Rooms", table => table.HasCheckConstraint(
            "CK_Rooms_Dimensions", "[Width] > 0 AND [Height] > 0"));
        builder.Property(x => x.PositionX).HasPrecision(12, 2);
        builder.Property(x => x.PositionY).HasPrecision(12, 2);
        builder.Property(x => x.Width).HasPrecision(12, 2).HasDefaultValue(100m);
        builder.Property(x => x.Height).HasPrecision(12, 2).HasDefaultValue(100m);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasOne<Office>().WithMany().HasForeignKey(x => x.OfficeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
