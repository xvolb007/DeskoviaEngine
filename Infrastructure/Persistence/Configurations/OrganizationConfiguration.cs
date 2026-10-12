using Deskovia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deskovia.Infrastructure.Persistence.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("Organizations");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset(7)");
    }
}
