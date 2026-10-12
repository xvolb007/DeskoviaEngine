using Deskovia.Domain.Enums;

namespace Deskovia.Domain.Entities;

public sealed class EmailNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid MemberId { get; set; }
    public Guid? ReservationId { get; set; }
    public NotificationKind Kind { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public required string RecipientEmail { get; set; }
    public required string DeduplicationKey { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public DateTimeOffset ScheduledAtUtc { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
    public DateTimeOffset? LockedUntilUtc { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public byte[] Version { get; set; } = [];
}
