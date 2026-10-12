using Deskovia.Application.Interfaces;
using Deskovia.Domain.Entities;
using Deskovia.Domain.Enums;
using Deskovia.Shared.Contracts;

namespace Deskovia.Application.Services;

public sealed class ReservationService(WorkspaceContext context, BillingOptions billing, PrototypeOptions options)
{
    private IWorkspaceStore Store => context.Store;
    private IQueryable<Reservation> Reservations => Store.Query<Reservation>().Where(x => x.OrganizationId == context.OrganizationId);
    private IQueryable<Reservation> Active => Reservations.Where(x => x.Status == ReservationStatus.Reserved || x.Status == ReservationStatus.Confirmed);
    public async Task<List<DateAvailability>> AvailabilityAsync(Guid actor, BookingRequest input, CancellationToken ct) => (await ProcessAsync(actor, input, ct, false)).Dates;
    public async Task<BookingBatchResult> CreateBatchAsync(Guid actor, BookingRequest input, CancellationToken ct) => (await ProcessAsync(actor, input, ct, true)).Batch!;
    private async Task<(List<DateAvailability> Dates, BookingBatchResult? Batch)> ProcessAsync(Guid actor, BookingRequest input, CancellationToken ct, bool save)
    {
        await using var tx = await Store.LockAsync(ct);
        context.Require(actor);
        ExpireDue();
        var result = CheckDates(actor, input);
        if (!save) { await Store.CommitAsync(ct); return (result, null); }
        var conflicts = result.Where(x => !x.Available).ToList();
        if (conflicts.Count > 0)
        {
            // Persist overdue lifecycle changes, but never a partial booking batch.
            await Store.CommitAsync(ct);
            throw new BusinessException(409, "booking_conflict", "The batch was not saved. Refresh the selected dates.", conflicts);
        }
        var desk = Store.Query<Desk>().Single(x => x.Id == input.DeskId);
        var room = Store.Query<Room>().Single(x => x.Id == desk.RoomId);
        var batch = Guid.NewGuid();
        var created = new List<Reservation>();
        foreach (var day in result)
        {
            var r = new Reservation { OrganizationId = context.OrganizationId, OfficeId = room.OfficeId, RoomId = room.Id, DeskId = desk.Id, MemberId = actor,
                BatchId = batch, StartsAtUtc = day.Start!.Value, EndsAtUtc = day.End!.Value, IsFullDay = input.IsFullDay, CreatedAtUtc = context.Now };
            Store.Add(r);
            created.Add(r);
            Event(r, ReservationEventType.Created, actor, null);
            Notify(r, NotificationKind.ReservationCreated, context.Now);
            if (context.Now < r.StartsAtUtc.AddHours(-2)) Notify(r, NotificationKind.ReservationReminder, r.StartsAtUtc.AddHours(-2));
        }
        await Store.CommitAsync(ct);
        return (result, new BookingBatchResult(batch, created.Select(x => View(x, actor)).ToList()));
    }
    private List<DateAvailability> CheckDates(Guid actor, BookingRequest input)
    {
        WorkspaceContext.Check(input.Dates != null && input.Dates.Count is >= 1 and <= 31 && input.Dates.Distinct().Count() == input.Dates.Count,
            "invalid_dates", "Select between 1 and 31 distinct dates.");
        var desk = Store.Query<Desk>().SingleOrDefault(x => x.Id == input.DeskId);
        var room = desk == null ? null : Store.Query<Room>().SingleOrDefault(x => x.Id == desk.RoomId);
        var office = room == null ? null : Store.Query<Office>().SingleOrDefault(x => x.Id == room.OfficeId && x.OrganizationId == context.OrganizationId);
        if (desk == null || room == null || office == null) throw new BusinessException(404, "desk_not_found", "Desk not found.");
        var hours = Store.Query<OfficeWorkingHours>().Where(x => x.OfficeId == office.Id).ToList();
        var subscription = Store.Query<OrganizationSubscription>().SingleOrDefault(x => x.OrganizationId == context.OrganizationId);
        var output = new List<DateAvailability>();
        foreach (var day in input.Dates!.Order())
        {
            try
            {
                WorkspaceContext.Check(!desk.IsArchived && !room.IsArchived && !office.IsArchived, "archived", "This workspace is archived.");
                var interval = BookingRules.Interval(office, hours.SingleOrDefault(x => x.DayOfWeek == day.DayOfWeek), day, input.IsFullDay, input.LocalStart, input.LocalEnd, context.Now);
                var start = interval.Start; var end = interval.End;
                WorkspaceContext.Check(!billing.Enabled || subscription != null && subscription.Status == SubscriptionStatus.Active &&
                    subscription.CurrentPeriodStartsAtUtc <= context.Now && subscription.CurrentPeriodEndsAtUtc > context.Now &&
                    subscription.CurrentPeriodStartsAtUtc <= start && subscription.CurrentPeriodEndsAtUtc >= end, "subscription_required", "The interval must fall within an active paid period.");
                WorkspaceContext.Check(!Store.Query<OfficeClosure>().Any(x => x.OrganizationId == context.OrganizationId && x.OfficeId == office.Id &&
                    (x.RoomId == null || x.RoomId == room.Id) && x.CancelledAtUtc == null && x.StartsAtUtc < end && start < x.EndsAtUtc), "closed", "The office or room is closed.");
                // Exclude tracked lifecycle changes awaiting SaveChanges.
                var overlaps = Active.Where(x => (x.DeskId == desk.Id || x.MemberId == actor) && x.StartsAtUtc < end && start < x.EndsAtUtc).ToList().Where(BookingRules.Active).ToList();
                WorkspaceContext.Check(!overlaps.Any(x => x.DeskId == desk.Id), "desk_occupied", "The desk is occupied.");
                WorkspaceContext.Check(!overlaps.Any(x => x.MemberId == actor), "member_overlap", "You already have an overlapping reservation.");
                output.Add(new(day, true, null, null, start, end));
            }
            catch (BusinessException ex) { output.Add(new(day, false, ex.Code, ex.Message, null, null)); }
        }
        return output;
    }
    public async Task ChangeAsync(Guid actor, Guid id, string action, string? reason, CancellationToken ct)
    {
        await using var tx = await Store.LockAsync(ct);
        var member = context.Require(actor);
        var r = Reservations.SingleOrDefault(x => x.Id == id);
        if (r == null || r.MemberId != actor && member.Role == MemberRole.Employee) throw new BusinessException(404, "reservation_not_found", "Reservation not found.");
        if (action != "cancel" && r.MemberId != actor) throw new BusinessException(403, "own_reservation_required", "Only the reservation owner can perform this action.");
        ExpireDue();
        var now = context.Now;
        if (action == "confirm" && r.Status == ReservationStatus.Confirmed ||
            action == "complete" && r.Status == ReservationStatus.Completed ||
            action == "cancel" && r.Status == ReservationStatus.Cancelled) { await Store.CommitAsync(ct); return; }
        if (action == "confirm" && BookingRules.CanConfirm(r, now))
        {
            r.Status = ReservationStatus.Confirmed; r.ConfirmedAtUtc = now;
            Event(r, ReservationEventType.Confirmed, actor, null); CancelReminders(r);
        }
        else if (action == "complete" && BookingRules.CanComplete(r, now)) Complete(r, actor, now, reason);
        else if (action == "cancel" && BookingRules.Active(r) &&
            (member.Role >= MemberRole.Administrator || BookingRules.CanCancel(r, now)))
        {
            var personal = r.MemberId == actor && BookingRules.CanCancel(r, now);
            if (!personal) WorkspaceContext.Text(reason, 1000);
            Cancel(r, actor, personal ? CancellationReason.Employee : CancellationReason.Administrator, reason);
        }
        else
        {
            await Store.CommitAsync(ct);
            throw new BusinessException(409, "invalid_state_or_time", "The reservation status or allowed time window has changed.");
        }
        await Store.CommitAsync(ct);
    }
    public void ExpireDue()
    {
        var now = context.Now;
        foreach (var r in Active.Where(x => x.Status == ReservationStatus.Reserved && x.StartsAtUtc.AddMinutes(5) <= now ||
            x.Status == ReservationStatus.Confirmed && x.EndsAtUtc <= now).ToList())
        {
            if (!BookingRules.Active(r)) continue;
            if (r.Status == ReservationStatus.Reserved) Cancel(r, null, CancellationReason.NoShow, "The confirmation window has expired.");
            else Complete(r, null, r.EndsAtUtc, "Scheduled completion.");
        }
    }
    public void Cancel(Reservation r, Guid? actor, CancellationReason reason, string? text)
    {
        if (!BookingRules.Active(r)) return;
        r.Status = ReservationStatus.Cancelled; r.CancelledAtUtc = context.Now; r.CancellationReason = reason;
        Event(r, ReservationEventType.Cancelled, actor, reason + ": " + text);
        CancelReminders(r); Notify(r, NotificationKind.ReservationCancelled, context.Now);
    }
    public void Complete(Reservation r, Guid? actor, DateTimeOffset at, string? reason)
    {
        if (r.Status != ReservationStatus.Confirmed) return;
        r.Status = ReservationStatus.Completed; r.CompletedAtUtc = at;
        Event(r, ReservationEventType.Completed, actor, reason); CancelReminders(r);
    }
    public void ResolveDisruption(IEnumerable<Reservation> affected, Guid? actor, CancellationReason reason, string? text, DateTimeOffset? effectiveStart = null)
    {
        foreach (var r in affected.Where(BookingRules.Active))
        {
            if (r.Status == ReservationStatus.Confirmed && r.StartsAtUtc <= context.Now && context.Now < r.EndsAtUtc && (effectiveStart == null || effectiveStart <= context.Now))
                Complete(r, actor, context.Now, reason + ": " + text);
            else Cancel(r, actor, reason, text);
        }
    }
    private void Event(Reservation r, ReservationEventType type, Guid? actor, string? reason)
    {
        if (reason?.Length > 1000) reason = reason[..1000];
        Store.Add(new ReservationEvent { OrganizationId = context.OrganizationId, ReservationId = r.Id, ActorMemberId = actor, EventType = type, Reason = reason, OccurredAtUtc = context.Now });
        context.Audit(actor, type.ToString(), "Reservation", r.Id, reason);
    }
    private void CancelReminders(Reservation r)
    {
        foreach (var item in Store.Query<EmailNotification>().Where(x => x.ReservationId == r.Id && x.Kind == NotificationKind.ReservationReminder &&
            (x.Status == NotificationStatus.Pending || x.Status == NotificationStatus.Processing)).ToList()) item.Status = NotificationStatus.Cancelled;
    }
    private void Notify(Reservation r, NotificationKind kind, DateTimeOffset scheduled)
    {
        var userId = Store.Query<OrganizationMember>().SingleOrDefault(x => x.Id == r.MemberId)?.UserId;
        var user = userId == null ? null : Store.Query<AppUser>().SingleOrDefault(x => x.Id == userId);
        // Newly registered members have no reservations until a subsequent request.
        if (user == null) return;
        var office = Store.Query<Office>().Single(x => x.Id == r.OfficeId);
        var room = Store.Query<Room>().Single(x => x.Id == r.RoomId);
        var desk = Store.Query<Desk>().Single(x => x.Id == r.DeskId);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(office.TimeZoneId);
        var body = $"{office.Name} / {room.Name} / {desk.Label}: {TimeZoneInfo.ConvertTime(r.StartsAtUtc, zone):yyyy-MM-dd HH:mm}–{TimeZoneInfo.ConvertTime(r.EndsAtUtc, zone):HH:mm} ({office.TimeZoneId}). " +
            $"Confirm your arrival from {TimeZoneInfo.ConvertTime(r.StartsAtUtc.AddHours(-1), zone):yyyy-MM-dd HH:mm} until {TimeZoneInfo.ConvertTime(r.StartsAtUtc.AddMinutes(5), zone):yyyy-MM-dd HH:mm} (exclusive). Cancellation reason: {r.CancellationReason}. " +
            $"{options.FrontendOrigin}/reservations?id={r.Id}";
        if (kind == NotificationKind.ReservationCreated && context.Now >= r.StartsAtUtc.AddHours(-2)) body += " This message includes your reminder.";
        Store.Add(new EmailNotification { OrganizationId = context.OrganizationId, MemberId = r.MemberId, ReservationId = r.Id,
            Kind = kind, RecipientEmail = user.Email, DeduplicationKey = $"{r.Id}:{kind}", Subject = $"Deskovia: {kind}", Body = body, ScheduledAtUtc = scheduled });
    }
    public PageResult<ReservationView> History(Guid actor, bool admin, bool history, int page, int size, Guid? officeId = null, Guid? memberId = null, string? status = null)
    {
        context.Require(actor, admin ? MemberRole.Administrator : MemberRole.Employee);
        page = Math.Max(1, page); size = Math.Clamp(size, 1, 100);
        var query = Reservations;
        if (!admin) query = query.Where(x => x.MemberId == actor);
        if (officeId != null) query = query.Where(x => x.OfficeId == officeId);
        if (admin && memberId != null) query = query.Where(x => x.MemberId == memberId);
        if (Enum.TryParse<ReservationStatus>(status, out var state)) query = query.Where(x => x.Status == state);
        else if (!admin) query = history ? query.Where(x => x.Status == ReservationStatus.Cancelled || x.Status == ReservationStatus.Completed) :
            query.Where(x => x.Status == ReservationStatus.Reserved || x.Status == ReservationStatus.Confirmed);
        return new(query.OrderByDescending(x => x.StartsAtUtc).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size).ToList().Select(x => View(x, actor)).ToList(), query.Count(), page, size);
    }
    public ReservationView Get(Guid actor, Guid id)
    {
        var member = context.Require(actor);
        var r = Reservations.SingleOrDefault(x => x.Id == id);
        if (r == null || r.MemberId != actor && member.Role == MemberRole.Employee) throw new BusinessException(404, "reservation_not_found", "Reservation not found.");
        return View(r, actor);
    }
    public ReservationView View(Reservation r, Guid actor)
    {
        var office = Store.Query<Office>().Single(x => x.Id == r.OfficeId);
        var userId = Store.Query<OrganizationMember>().Single(x => x.Id == r.MemberId).UserId;
        var own = r.MemberId == actor;
        return new(r.Id, r.BatchId, r.MemberId, r.OfficeId, r.RoomId, r.DeskId, office.Name, Store.Query<Room>().Single(x => x.Id == r.RoomId).Name,
            Store.Query<Desk>().Single(x => x.Id == r.DeskId).Label, Store.Query<AppUser>().Single(x => x.Id == userId).DisplayName, office.TimeZoneId,
            r.StartsAtUtc, r.EndsAtUtc, r.IsFullDay, r.Status.ToString(), r.CancellationReason?.ToString(), r.CompletedAtUtc,
            own && BookingRules.CanConfirm(r, context.Now), own && BookingRules.CanCancel(r, context.Now), own && BookingRules.CanComplete(r, context.Now));
    }
    public async Task<List<Occupancy>> OccupancyAsync(Guid actor, Guid officeId, DateOnly date, TimeOnly? start, TimeOnly? end, CancellationToken ct)
    {
        await using var tx = await Store.LockAsync(ct);
        context.Require(actor);
        var office = Store.Query<Office>().SingleOrDefault(x => x.Id == officeId && x.OrganizationId == context.OrganizationId && !x.IsArchived)
            ?? throw new BusinessException(404, "office_not_found", "Office not found.");
        var zone = TimeZoneInfo.FindSystemTimeZoneById(office.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(context.Now, zone).DateTime);
        WorkspaceContext.Check(date >= today && date <= today.AddDays(office.BookingHorizonDays), "outside_horizon", "Past occupancy is not available.");
        var from = BookingRules.ToUtc(date, start ?? TimeOnly.MinValue, zone);
        var until = end == null ? BookingRules.ToUtc(date.AddDays(1), TimeOnly.MinValue, zone) : BookingRules.ToUtc(date, end.Value, zone);
        WorkspaceContext.Check(until > from, "invalid_interval", "Invalid interval.");
        ExpireDue();
        var items = Active.Where(x => x.OfficeId == officeId && x.StartsAtUtc < until && from < x.EndsAtUtc).ToList().Where(BookingRules.Active)
            .Select(x => new Occupancy(x.DeskId, Store.Query<AppUser>().Single(u => u.Id == Store.Query<OrganizationMember>().Single(m => m.Id == x.MemberId).UserId).DisplayName, x.StartsAtUtc, x.EndsAtUtc)).ToList();
        await Store.CommitAsync(ct);
        return items;
    }
}
