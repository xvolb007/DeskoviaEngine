namespace Deskovia.Domain.Entities;

public sealed class Desk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public required string Label { get; set; }
    public decimal PositionX { get; set; }
    public decimal PositionY { get; set; }
    public decimal RotationDegrees { get; set; }
    public bool IsArchived { get; set; }
}
