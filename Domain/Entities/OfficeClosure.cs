namespace Deskovia.Domain.Entities;

public sealed class OfficeClosure
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid OfficeId { get; set; }
    public Guid? RoomId { get; set; }
    public Guid? CreatedByMemberId { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CancelledAtUtc { get; set; }
}
