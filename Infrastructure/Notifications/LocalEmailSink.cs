using Deskovia.Application.Interfaces;

namespace Deskovia.Infrastructure.Notifications;

// The durable outbox itself is the local mailbox. Nothing is sent to unverified addresses.
public sealed class LocalEmailSink : IEmailSink
{
    public Task DeliverAsync(Guid id, string recipient, string subject, string body, CancellationToken ct) => Task.CompletedTask;
}
