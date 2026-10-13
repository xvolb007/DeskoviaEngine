using Deskovia.Application.Interfaces;
using Deskovia.Domain.Entities;
using Deskovia.Domain.Enums;
using Deskovia.Shared.Contracts;

namespace Deskovia.Application.Services;

public sealed class OperationsService(WorkspaceContext context, AdministrationService administration, ReservationService reservations,
    PrototypeOptions options, IDirectoryAdapter directory, IEmailSink email)
{
    private IWorkspaceStore Store => context.Store;
    public DirectoryRunView DirectoryStatus(Guid actor)
    {
        context.Require(actor, MemberRole.Owner);
        var run = Store.Query<DirectoryRun>().SingleOrDefault(x => x.OrganizationId == context.OrganizationId);
        return new(run?.LastRunAtUtc, run?.Result ?? "The check has not run yet.");
    }
    public async Task SetDirectoryAsync(Guid actor, Guid id, string? state, CancellationToken ct)
    {
        WorkspaceContext.Check(options.DemoDirectoryEnabled, "demo_disabled", "The directory demonstration is disabled.");
        WorkspaceContext.Check(state == null || state is "Active" or "Disabled" or "Deleted" or "Unavailable", "invalid_directory_state", "Unknown state.");
        await using var tx = await Store.LockAsync(ct);
        context.Require(actor, MemberRole.Owner);
        var member = Store.Query<OrganizationMember>().SingleOrDefault(x => x.Id == id && x.OrganizationId == context.OrganizationId)
            ?? throw new BusinessException(404, "member_not_found", "Member not found.");
        WorkspaceContext.Check(member.Role != MemberRole.Owner, "owner_protected", "An Owner cannot be connected to the account disabling simulation.");
        var item = Store.Query<DemoDirectoryRecord>().SingleOrDefault(x => x.MemberId == id);
        if (state == null) { if (item != null) Store.Remove(item); }
        else if (item == null) Store.Add(new DemoDirectoryRecord { MemberId = id, State = state });
        else item.State = state;
        context.Audit(actor, "DemoDirectoryState", "Member", id, state ?? "Detached");
        await Store.CommitAsync(ct);
    }
    public async Task SyncDirectoryAsync(Guid? actor, bool forced, CancellationToken ct)
    {
        if (!options.DemoDirectoryEnabled) return;
        // Adapter reads happen outside the database transaction.
        var records = Store.Query<DemoDirectoryRecord>().ToList();
        var states = new Dictionary<Guid, (string Configured, string Returned)>();
        foreach (var r in records)
        {
            try { states[r.MemberId] = (r.State, await directory.GetStateAsync(r.State, ct)); }
            catch (Exception) when (!ct.IsCancellationRequested) { states[r.MemberId] = (r.State, "Unavailable"); }
        }
        await using var tx = await Store.LockAsync(ct);
        if (actor != null) context.Require(actor.Value, MemberRole.Owner);
        var run = Store.Query<DirectoryRun>().SingleOrDefault(x => x.OrganizationId == context.OrganizationId);
        if (!Store.Query<Organization>().Any(x => x.Id == context.OrganizationId)) return;
        if (!forced && run?.LastRunAtUtc > context.Now.AddDays(-1)) return;
        if (run == null) { run = new DirectoryRun { OrganizationId = context.OrganizationId, Result = "" }; Store.Add(run); }
        var disabled = 0; var unavailable = 0;
        foreach (var entry in states)
        {
            var current = Store.Query<DemoDirectoryRecord>().SingleOrDefault(x => x.MemberId == entry.Key);
            if (current == null || current.State != entry.Value.Configured) continue;
            if (entry.Value.Returned == "Unavailable") { unavailable++; continue; }
            if (entry.Value.Returned is not ("Disabled" or "Deleted")) continue;
            var member = Store.Query<OrganizationMember>().SingleOrDefault(x => x.Id == entry.Key && x.OrganizationId == context.OrganizationId);
            if (member != null && member.IsActive && member.Role != MemberRole.Owner) { administration.ApplyEnabled(member, false, actor); disabled++; }
        }
        run.LastRunAtUtc = context.Now; run.Result = $"Local demo: disabled {disabled}; unavailable responses {unavailable}; reactivation is manual only.";
        context.Audit(actor, "DemoDirectorySync", "Organization", context.OrganizationId, run.Result);
        await Store.CommitAsync(ct);
    }
    public List<NotificationView> Notifications(Guid actor)
    {
        context.Require(actor, MemberRole.Owner);
        return Store.Query<EmailNotification>().Where(x => x.OrganizationId == context.OrganizationId).OrderByDescending(x => x.ScheduledAtUtc).Take(200).ToList()
            .Select(x => new NotificationView(x.Id, x.RecipientEmail, x.Subject, x.Body, x.Status.ToString(), x.ScheduledAtUtc, x.SentAtUtc, x.AttemptCount, x.LastError)).ToList();
    }
    public async Task TickAsync(CancellationToken ct)
    {
        await using (var tx = await Store.LockAsync(ct))
        {
            reservations.ExpireDue();
            await Store.CommitAsync(ct);
        }
        await SyncDirectoryAsync(null, false, ct);
        // Claim bounded batches. Durable lease permits recovery after process termination.
        var claimed = new List<Guid>();
        await using (var tx = await Store.LockAsync(ct))
        {
            var now = context.Now;
            foreach (var expiredLease in Store.Query<EmailNotification>().Where(x => x.OrganizationId == context.OrganizationId && x.Status == NotificationStatus.Processing && x.AttemptCount >= 5 && x.LockedUntilUtc <= now).ToList())
            {
                expiredLease.Status = NotificationStatus.Failed; expiredLease.LockedUntilUtc = null;
                expiredLease.LastError = "The attempt limit was reached after the processing lease expired.";
            }
            var due = Store.Query<EmailNotification>().Where(x => x.OrganizationId == context.OrganizationId && x.AttemptCount < 5 &&
                x.ScheduledAtUtc <= now && (x.Status == NotificationStatus.Pending || x.Status == NotificationStatus.Processing && x.LockedUntilUtc <= now))
                .OrderBy(x => x.ScheduledAtUtc).Take(25).ToList();
            foreach (var item in due)
            {
                item.Status = NotificationStatus.Processing; item.LockedUntilUtc = now.AddMinutes(2); item.AttemptCount++;
                claimed.Add(item.Id);
            }
            await Store.CommitAsync(ct);
        }
        foreach (var id in claimed)
        {
            var item = Store.Query<EmailNotification>().Single(x => x.Id == id);
            if (item.Status != NotificationStatus.Processing) continue;
            string? error = null;
            try { await email.DeliverAsync(item.Id, item.RecipientEmail, item.Subject, item.Body, ct); }
            catch (Exception) when (!ct.IsCancellationRequested) { error = "The test mailbox is unavailable."; }
            var lease = item.LockedUntilUtc;
            await using var tx = await Store.LockAsync(ct);
            var current = Store.Query<EmailNotification>().Single(x => x.Id == id);
            if (current.Status != NotificationStatus.Processing || current.LockedUntilUtc != lease) continue;
            current.LockedUntilUtc = null; current.LastError = error;
            if (error == null) { current.Status = NotificationStatus.Sent; current.SentAtUtc = context.Now; }
            else { current.Status = current.AttemptCount >= 5 ? NotificationStatus.Failed : NotificationStatus.Pending; current.ScheduledAtUtc = context.Now.AddSeconds(Math.Pow(2, current.AttemptCount) * 30); }
            await Store.CommitAsync(ct);
        }
    }
}
