namespace Deskovia.Domain.Entities;

public sealed class Office
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public required string Name { get; set; }
    public string TimeZoneId { get; set; } = "Europe/Prague";
    public int BookingHorizonDays { get; set; } = 30;
    public bool IsArchived { get; set; }
}
