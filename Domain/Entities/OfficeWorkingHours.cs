namespace Deskovia.Domain.Entities;

public sealed class OfficeWorkingHours
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfficeId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly OpensAt { get; set; }
    public TimeOnly ClosesAt { get; set; }
}
