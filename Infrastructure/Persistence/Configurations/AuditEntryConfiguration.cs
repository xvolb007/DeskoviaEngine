using Deskovia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deskovia.Infrastructure.Persistence.Configurations;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("AuditEntries");
        builder.Property(x => x.Action).HasMaxLength(100);
        builder.Property(x => x.EntityType).HasMaxLength(100);
        builder.Property(x => x.Details).HasMaxLength(4000);
        builder.HasIndex(x => new { x.OrganizationId, x.OccurredAtUtc });
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationMember>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ActorMemberId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
