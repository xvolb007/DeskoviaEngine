using Deskovia.Domain.Enums;

namespace Deskovia.Domain.Entities;

public sealed class PaymentWebhookEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid? PaymentId { get; set; }
    public required string Provider { get; set; }
    public required string ProviderEventId { get; set; }
    public WebhookStatus Status { get; set; } = WebhookStatus.Pending;
    public DateTimeOffset ReceivedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAtUtc { get; set; }
    public string? LastError { get; set; }
}
