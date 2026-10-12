using Deskovia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deskovia.Infrastructure.Persistence.Configurations;

public sealed class OrganizationIdentityConnectionConfiguration : IEntityTypeConfiguration<OrganizationIdentityConnection>
{
    public void Configure(EntityTypeBuilder<OrganizationIdentityConnection> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("OrganizationIdentityConnections");
        builder.HasIndex(x => x.OrganizationId).IsUnique();
        builder.HasIndex(x => x.MicrosoftTenantId).IsUnique();
        builder.Property(x => x.DeltaLink).HasMaxLength(8000);
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
