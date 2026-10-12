using Deskovia.Domain.Enums;

namespace Deskovia.Domain.Entities;

public sealed class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid SubscriptionId { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string Provider { get; set; }
    public string? ProviderPaymentId { get; set; }
    public required string IdempotencyKey { get; set; }
    public bool IsTest { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PaidAtUtc { get; set; }
    public DateTimeOffset PeriodStartsAtUtc { get; set; }
    public DateTimeOffset PeriodEndsAtUtc { get; set; }
    public byte[] Version { get; set; } = [];
}
