using Deskovia.Domain.Enums;

namespace Deskovia.Domain.Entities;

public sealed class OrganizationSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.PendingPayment;
    public decimal MonthlyPrice { get; set; }
    public required string Currency { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CurrentPeriodStartsAtUtc { get; set; }
    public DateTimeOffset? CurrentPeriodEndsAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public string? ProviderCustomerId { get; set; }
    public string? ProviderSubscriptionId { get; set; }
    public byte[] Version { get; set; } = [];
}
