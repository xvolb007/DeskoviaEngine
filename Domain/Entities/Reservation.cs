using Deskovia.Domain.Enums;

namespace Deskovia.Domain.Entities;

public sealed class Reservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid OfficeId { get; set; }
    public Guid RoomId { get; set; }
    public Guid DeskId { get; set; }
    public Guid MemberId { get; set; }
    public Guid? BatchId { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }
    public bool IsFullDay { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Reserved;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public CancellationReason? CancellationReason { get; set; }
    public byte[] Version { get; set; } = [];
}
