using Deskovia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deskovia.Infrastructure.Persistence.Configurations;

public sealed class LocalCredentialConfiguration : IEntityTypeConfiguration<LocalCredential>
{
    public void Configure(EntityTypeBuilder<LocalCredential> b)
    {
        b.ToTable("LocalCredentials");
        b.HasKey(x => x.UserId);
        b.Property(x => x.PasswordHash).HasMaxLength(1000);
        b.HasOne<AppUser>().WithOne().HasForeignKey<LocalCredential>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class DemoDirectoryConfiguration : IEntityTypeConfiguration<DemoDirectoryRecord>
{
    public void Configure(EntityTypeBuilder<DemoDirectoryRecord> b)
    {
        b.ToTable("DemoDirectoryRecords", t => t.HasCheckConstraint("CK_DemoDirectory_State", "[State] IN ('Active','Disabled','Deleted','Unavailable')"));
        b.HasKey(x => x.MemberId);
        b.Property(x => x.State).HasMaxLength(20);
        b.HasOne<OrganizationMember>().WithOne().HasForeignKey<DemoDirectoryRecord>(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class DirectoryRunConfiguration : IEntityTypeConfiguration<DirectoryRun>
{
    public void Configure(EntityTypeBuilder<DirectoryRun> b)
    {
        b.ToTable("DirectoryRuns");
        b.HasKey(x => x.OrganizationId);
        b.Property(x => x.Result).HasMaxLength(2000);
        b.HasOne<Organization>().WithOne().HasForeignKey<DirectoryRun>(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
