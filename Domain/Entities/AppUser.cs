namespace Deskovia.Domain.Entities;

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? MicrosoftTenantId { get; set; }
    public Guid? MicrosoftObjectId { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSignInAtUtc { get; set; }
    public DateTimeOffset? DisabledAtUtc { get; set; }
}
