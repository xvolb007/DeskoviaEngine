namespace Deskovia.Domain.Entities;

public sealed class Room
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfficeId { get; set; }
    public required string Name { get; set; }
    public decimal PositionX { get; set; }
    public decimal PositionY { get; set; }
    public decimal Width { get; set; } = 100;
    public decimal Height { get; set; } = 100;
    public bool IsArchived { get; set; }
}
