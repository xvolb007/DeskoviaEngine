namespace Deskovia.Domain.Entities;

public sealed class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
