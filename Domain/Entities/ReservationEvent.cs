using Deskovia.Domain.Enums;

namespace Deskovia.Domain.Entities;

public sealed class ReservationEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid ReservationId { get; set; }
    public Guid? ActorMemberId { get; set; }
    public ReservationEventType EventType { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? Reason { get; set; }
}
