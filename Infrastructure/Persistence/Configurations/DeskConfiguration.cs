using Deskovia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deskovia.Infrastructure.Persistence.Configurations;

public sealed class DeskConfiguration : IEntityTypeConfiguration<Desk>
{
    public void Configure(EntityTypeBuilder<Desk> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.RoomId, x.Id });
        builder.ToTable("Desks", table => table.HasCheckConstraint(
            "CK_Desks_Rotation", "[RotationDegrees] >= 0 AND [RotationDegrees] < 360"));
        builder.Property(x => x.RotationDegrees).HasPrecision(5, 2);
        builder.Property(x => x.Label).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PositionX).HasPrecision(12, 2);
        builder.Property(x => x.PositionY).HasPrecision(12, 2);
        builder.HasIndex(x => new { x.RoomId, x.Label }).IsUnique();
        builder.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
