using Deskovia.Domain.Enums;

namespace Deskovia.Domain.Entities;

public sealed class OrganizationMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public MemberRole Role { get; set; } = MemberRole.Employee;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset JoinedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeactivatedAtUtc { get; set; }
    public byte[] Version { get; set; } = [];
}
