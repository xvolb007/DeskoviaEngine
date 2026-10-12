namespace Deskovia.Domain.Entities;

public sealed class OrganizationIdentityConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid MicrosoftTenantId { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset? ConnectedAtUtc { get; set; }
    public DateTimeOffset? LastSuccessfulSyncAtUtc { get; set; }
    public DateTimeOffset? NextSyncAtUtc { get; set; }
    public string? DeltaLink { get; set; }
}
