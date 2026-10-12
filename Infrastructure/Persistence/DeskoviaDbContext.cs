using Deskovia.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Deskovia.Infrastructure.Persistence;

public sealed class DeskoviaDbContext : DbContext
{
    public DeskoviaDbContext(DbContextOptions<DeskoviaDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Office> Offices => Set<Office>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Desk> Desks => Set<Desk>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<OrganizationIdentityConnection> OrganizationIdentityConnections => Set<OrganizationIdentityConnection>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<OfficeWorkingHours> OfficeWorkingHours => Set<OfficeWorkingHours>();
    public DbSet<OfficeClosure> OfficeClosures => Set<OfficeClosure>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationEvent> ReservationEvents => Set<ReservationEvent>();
    public DbSet<EmailNotification> EmailNotifications => Set<EmailNotification>();
    public DbSet<OrganizationSubscription> OrganizationSubscriptions => Set<OrganizationSubscription>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<LocalCredential> LocalCredentials => Set<LocalCredential>();
    public DbSet<DemoDirectoryRecord> DemoDirectoryRecords => Set<DemoDirectoryRecord>();
    public DbSet<DirectoryRun> DirectoryRuns => Set<DirectoryRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeskoviaDbContext).Assembly);
        modelBuilder.HasDefaultSchema("dbo");
        modelBuilder.UseCollation("Latin1_General_100_BIN2");
    }
}
