namespace Deskovia.Domain.Entities;

public sealed class LocalCredential
{
    public Guid UserId { get; set; }
    public required string PasswordHash { get; set; }
    public DateTimeOffset PasswordChangedAtUtc { get; set; }
}
