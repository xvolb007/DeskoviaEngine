namespace Deskovia.Domain.Entities;

public sealed class AuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid? ActorMemberId { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public Guid EntityId { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
