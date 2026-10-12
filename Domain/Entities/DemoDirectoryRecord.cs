namespace Deskovia.Domain.Entities;

public sealed class DemoDirectoryRecord
{
    public Guid MemberId { get; set; }
    public required string State { get; set; }
}
public sealed class DirectoryRun
{
    public Guid OrganizationId { get; set; }
    public DateTimeOffset? LastRunAtUtc { get; set; }
    public required string Result { get; set; }
}
