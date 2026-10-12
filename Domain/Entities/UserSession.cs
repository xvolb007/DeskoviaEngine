namespace Deskovia.Domain.Entities;

public sealed class UserSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
