using Deskovia.Application.Interfaces;
using Deskovia.Domain.Entities;
using Deskovia.Domain.Enums;
using Deskovia.Shared.Contracts;

namespace Deskovia.Application.Services;

public sealed class AdministrationService(WorkspaceContext context, ReservationService reservations)
{
    private IWorkspaceStore Store => context.Store;
    private Office Office(Guid id) => Store.Query<Office>().SingleOrDefault(x => x.Id == id && x.OrganizationId == context.OrganizationId)
        ?? throw new BusinessException(404, "office_not_found", "Office not found.");
    private Room Room(Guid id)
    {
        var room = Store.Query<Room>().SingleOrDefault(x => x.Id == id) ?? throw new BusinessException(404, "room_not_found", "Room not found.");
        Office(room.OfficeId); return room;
    }
    private Desk Desk(Guid id)
    {
        var desk = Store.Query<Desk>().SingleOrDefault(x => x.Id == id) ?? throw new BusinessException(404, "desk_not_found", "Desk not found.");
        Room(desk.RoomId); return desk;
    }
    public List<OfficeView> Offices(Guid actor, bool archived = false)
    {
        context.Require(actor, archived ? MemberRole.Administrator : MemberRole.Employee);
        return Store.Query<Office>().Where(x => x.OrganizationId == context.OrganizationId && (archived || !x.IsArchived)).OrderBy(x => x.Name).ToList().Select(x =>
            new OfficeView(x.Id, x.Name, x.TimeZoneId, x.BookingHorizonDays, x.IsArchived,
                DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(context.Now, TimeZoneInfo.FindSystemTimeZoneById(x.TimeZoneId)).DateTime),
                Store.Query<OfficeWorkingHours>().Where(h => h.OfficeId == x.Id).ToList().Select(h => new WorkingHoursInput(h.DayOfWeek, h.OpensAt, h.ClosesAt)).ToList(), context.Now)).ToList();
    }
    public OfficeMap Map(Guid actor, Guid officeId, bool archived = false)
    {
        context.Require(actor, archived ? MemberRole.Administrator : MemberRole.Employee);
        var office = Office(officeId);
        if (office.IsArchived && !archived) throw new BusinessException(404, "office_not_found", "The office is archived.");
        var rooms = Store.Query<Room>().Where(x => x.OfficeId == officeId && (archived || !x.IsArchived)).ToList();
        var ids = rooms.Select(x => x.Id).ToList();
        return new(rooms.Select(x => new RoomView(x.Id, x.OfficeId, x.Name, x.PositionX, x.PositionY, x.Width, x.Height, x.IsArchived)).ToList(),
            Store.Query<Desk>().Where(x => ids.Contains(x.RoomId) && (archived || !x.IsArchived)).ToList().Select(x => new DeskView(x.Id, x.RoomId, x.Label, x.PositionX, x.PositionY, x.RotationDegrees, x.IsArchived)).ToList());
    }
    public async Task<Guid> SaveOfficeAsync(Guid actor, Guid? id, OfficeInput input, CancellationToken ct)
    {
        var name = WorkspaceContext.Text(input.Name);
        try { TimeZoneInfo.FindSystemTimeZoneById(input.TimeZoneId); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { throw new BusinessException(400, "invalid_timezone", "Invalid time zone."); }
        WorkspaceContext.Check(input.BookingHorizonDays is >= 1 and <= 365 && input.Hours != null && input.Hours.Count <= 7 &&
            input.Hours.Select(x => x.Day).Distinct().Count() == input.Hours.Count &&
            input.Hours.All(x => Enum.IsDefined(x.Day) && x.ClosesAt > x.OpensAt && x.ClosesAt - x.OpensAt >= TimeSpan.FromMinutes(30) &&
                x.OpensAt.Ticks % (15 * TimeSpan.TicksPerMinute) == 0 && x.ClosesAt.Ticks % (15 * TimeSpan.TicksPerMinute) == 0),
            "invalid_schedule", "Check the booking horizon and schedule (15-minute increments, 30-minute minimum, no overnight shifts).");
        await using var tx = await Store.LockAsync(ct);
        context.Require(actor, MemberRole.Administrator); reservations.ExpireDue();
        var office = id == null ? new Office { OrganizationId = context.OrganizationId, Name = name } : Office(id.Value);
        WorkspaceContext.Check(!office.IsArchived, "archived", "The office is archived.");
        var affected = Store.Query<Reservation>().Where(x => x.OfficeId == office.Id && x.OrganizationId == context.OrganizationId &&
            (x.Status == ReservationStatus.Reserved || x.Status == ReservationStatus.Confirmed)).ToList().Where(BookingRules.Active).Where(r =>
        {
            if (office.TimeZoneId != input.TimeZoneId) return true;
            var zone = TimeZoneInfo.FindSystemTimeZoneById(input.TimeZoneId);
            var from = TimeZoneInfo.ConvertTime(r.StartsAtUtc, zone);
            var end = TimeZoneInfo.ConvertTime(r.EndsAtUtc, zone);
            var h = input.Hours!.SingleOrDefault(x => x.Day == from.DayOfWeek);
            return h == null || from.Date != end.Date || TimeOnly.FromDateTime(from.DateTime) < h.OpensAt ||
                TimeOnly.FromDateTime(end.DateTime) > h.ClosesAt || r.IsFullDay &&
                (TimeOnly.FromDateTime(from.DateTime) != h.OpensAt || TimeOnly.FromDateTime(end.DateTime) != h.ClosesAt);
        }).ToList();
        if (affected.Count > 0) throw new BusinessException(409, "schedule_conflict", "Explicitly cancel the affected reservations first.", affected.Select(x => reservations.View(x, actor)).ToList());
        office.Name = name; office.TimeZoneId = input.TimeZoneId; office.BookingHorizonDays = input.BookingHorizonDays;
        if (id == null) Store.Add(office);
        foreach (var h in Store.Query<OfficeWorkingHours>().Where(x => x.OfficeId == office.Id).ToList()) Store.Remove(h);
        foreach (var h in input.Hours!) Store.Add(new OfficeWorkingHours { OfficeId = office.Id, DayOfWeek = h.Day, OpensAt = h.OpensAt, ClosesAt = h.ClosesAt });
        context.Audit(actor, "SaveOffice", "Office", office.Id);
        await Store.CommitAsync(ct); return office.Id;
    }
    public async Task<Guid> SaveRoomAsync(Guid actor, Guid? id, RoomInput input, CancellationToken ct)
    {
        var name = WorkspaceContext.Text(input.Name);
        WorkspaceContext.Check(input.X >= 0 && input.Y >= 0 && input.Width is >= 20 and <= 10000 && input.Height is >= 20 and <= 10000 &&
            input.X <= 10000 && input.Y <= 10000 && decimal.Round(input.X, 2) == input.X && decimal.Round(input.Y, 2) == input.Y &&
            decimal.Round(input.Width, 2) == input.Width && decimal.Round(input.Height, 2) == input.Height, "invalid_geometry", "Room dimensions must be 20–10000; coordinates must be 0–10000.");
        await using var tx = await Store.LockAsync(ct);
        context.Require(actor, MemberRole.Administrator);
        var office = Office(input.OfficeId);
        WorkspaceContext.Check(!office.IsArchived, "archived", "The office is archived.");
        var room = id == null ? new Room { OfficeId = office.Id, Name = name } : Room(id.Value);
        WorkspaceContext.Check(!room.IsArchived && room.OfficeId == input.OfficeId, "room_move", "Moving rooms between offices is not supported.");
        if (Store.Query<Desk>().Where(x => x.RoomId == room.Id && !x.IsArchived).ToList().Any(x => !BookingRules.DeskFits(x.PositionX, x.PositionY, x.RotationDegrees, input.Width, input.Height)))
            throw new BusinessException(409, "desks_outside_room", "The new dimensions would leave desks outside the room.");
        room.Name = name; room.PositionX = input.X; room.PositionY = input.Y; room.Width = input.Width; room.Height = input.Height;
        if (id == null) Store.Add(room);
        context.Audit(actor, "SaveRoom", "Room", room.Id);
        await Store.CommitAsync(ct); return room.Id;
    }
    public async Task<Guid> SaveDeskAsync(Guid actor, Guid? id, DeskInput input, CancellationToken ct)
    {
        var label = WorkspaceContext.Text(input.Label, 100);
        await using var tx = await Store.LockAsync(ct);
        context.Require(actor, MemberRole.Administrator); reservations.ExpireDue();
        var room = Room(input.RoomId);
        WorkspaceContext.Check(!room.IsArchived && !Office(room.OfficeId).IsArchived, "archived", "The room or office is archived.");
        // A desk is a 12-unit square with rotation about its center.
        WorkspaceContext.Check(BookingRules.DeskFits(input.X, input.Y, input.Rotation, room.Width, room.Height) &&
            decimal.Round(input.X, 2) == input.X && decimal.Round(input.Y, 2) == input.Y && decimal.Round(input.Rotation, 2) == input.Rotation, "invalid_geometry", "The desk must fit inside the room; rotation must be 0–359 degrees.");
        var desk = id == null ? new Desk { RoomId = room.Id, Label = label } : Desk(id.Value);
        WorkspaceContext.Check(!desk.IsArchived, "archived", "The desk is archived.");
        if (desk.RoomId != input.RoomId)
        {
            // Composite keys are referenced by historical reservations; preserve this chain forever.
            if (id != null)
                throw new BusinessException(409, "desk_move_history", "A desk with history cannot be moved; archive it and create a new one.");
        }
        if (Store.Query<Desk>().Any(x => x.RoomId == room.Id && x.Label == label && x.Id != desk.Id))
            throw new BusinessException(409, "duplicate_label", "This desk label is already used in this room.");
        desk.RoomId = room.Id; desk.Label = label; desk.PositionX = input.X; desk.PositionY = input.Y; desk.RotationDegrees = input.Rotation;
        if (id == null) Store.Add(desk);
        context.Audit(actor, "SaveDesk", "Desk", desk.Id);
        await Store.CommitAsync(ct); return desk.Id;
    }
    public async Task ArchiveAsync(Guid actor, string resource, Guid id, CancellationToken ct)
    {
        await using var tx = await Store.LockAsync(ct);
        context.Require(actor, MemberRole.Administrator); reservations.ExpireDue();
        var query = Store.Query<Reservation>().Where(x => x.OrganizationId == context.OrganizationId &&
            (x.Status == ReservationStatus.Reserved || x.Status == ReservationStatus.Confirmed));
        CancellationReason reason;
        if (resource == "offices") { Office(id).IsArchived = true; query = query.Where(x => x.OfficeId == id); reason = CancellationReason.OfficeClosed; }
        else if (resource == "rooms") { Room(id).IsArchived = true; query = query.Where(x => x.RoomId == id); reason = CancellationReason.RoomClosed; }
        else if (resource == "desks") { Desk(id).IsArchived = true; query = query.Where(x => x.DeskId == id); reason = CancellationReason.DeskArchived; }
        else throw new BusinessException(404, "invalid_resource", "Resource not found.");
        reservations.ResolveDisruption(query.ToList(), actor, reason, "Archiving.");
        context.Audit(actor, "Archive", resource, id);
        await Store.CommitAsync(ct);
    }
    private List<Reservation> Affected(ClosureRequest input)
    {
        var office = Office(input.OfficeId);
        WorkspaceContext.Check(!office.IsArchived && input.End > input.Start && input.End > context.Now && input.Start.Offset == TimeSpan.Zero &&
            input.End.Offset == TimeSpan.Zero, "invalid_closure", "A closure requires a UTC interval ending in the future.");
        WorkspaceContext.Text(input.Reason, 1000);
        if (input.RoomId != null) WorkspaceContext.Check(Room(input.RoomId.Value).OfficeId == office.Id, "invalid_room", "The room belongs to another office.");
        return Store.Query<Reservation>().Where(x => x.OrganizationId == context.OrganizationId && x.OfficeId == office.Id &&
            (input.RoomId == null || x.RoomId == input.RoomId) && (x.Status == ReservationStatus.Reserved || x.Status == ReservationStatus.Confirmed) &&
            x.StartsAtUtc < input.End && input.Start < x.EndsAtUtc).ToList().Where(BookingRules.Active).ToList();
    }
    public async Task<List<ReservationView>> PreviewAsync(Guid actor, ClosureRequest input, CancellationToken ct)
    {
        await using var tx = await Store.LockAsync(ct);
        context.Require(actor, MemberRole.Administrator); reservations.ExpireDue();
        var result = Affected(input).Select(x => reservations.View(x, actor)).ToList();
        await Store.CommitAsync(ct); return result;
    }
    public async Task<Guid> CloseAsync(Guid actor, ClosureRequest input, CancellationToken ct)
    {
        await using var tx = await Store.LockAsync(ct);
        context.Require(actor, MemberRole.Administrator); reservations.ExpireDue();
        var affected = Affected(input);
        if (input.AcknowledgedReservationIds == null || affected.Any(x => !input.AcknowledgedReservationIds.Contains(x.Id)))
            throw new BusinessException(409, "closure_preview_changed", "Refresh and confirm the list of affected reservations.", affected.Select(x => reservations.View(x, actor)).ToList());
        var closure = new OfficeClosure { OrganizationId = context.OrganizationId, OfficeId = input.OfficeId, RoomId = input.RoomId,
            StartsAtUtc = input.Start, EndsAtUtc = input.End, Reason = input.Reason.Trim(), CreatedByMemberId = actor, CreatedAtUtc = context.Now };
        Store.Add(closure);
        reservations.ResolveDisruption(affected, actor, input.RoomId == null ? CancellationReason.OfficeClosed : CancellationReason.RoomClosed, input.Reason, input.Start);
        context.Audit(actor, "Close", "OfficeClosure", closure.Id, input.Reason);
        await Store.CommitAsync(ct); return closure.Id;
    }
    public List<ClosureView> Closures(Guid actor)
    {
        context.Require(actor, MemberRole.Administrator);
        return Store.Query<OfficeClosure>().Where(x => x.OrganizationId == context.OrganizationId).OrderByDescending(x => x.StartsAtUtc).Take(200).ToList()
            .Select(x => new ClosureView(x.Id, x.OfficeId, x.RoomId, x.StartsAtUtc, x.EndsAtUtc, x.Reason, x.CancelledAtUtc != null)).ToList();
    }
    public async Task CancelClosureAsync(Guid actor, Guid id, CancellationToken ct)
    {
        await using var tx = await Store.LockAsync(ct);
        context.Require(actor, MemberRole.Administrator);
        var item = Store.Query<OfficeClosure>().SingleOrDefault(x => x.Id == id && x.OrganizationId == context.OrganizationId)
            ?? throw new BusinessException(404, "closure_not_found", "Closure not found.");
        if (item.CancelledAtUtc == null) { item.CancelledAtUtc = context.Now; context.Audit(actor, "CancelClosure", "OfficeClosure", id); }
        await Store.CommitAsync(ct);
    }
    public List<MemberView> Members(Guid actor)
    {
        context.Require(actor, MemberRole.Administrator);
        return Store.Query<OrganizationMember>().Where(x => x.OrganizationId == context.OrganizationId).OrderBy(x => x.JoinedAtUtc).ToList().Select(x =>
        {
            var u = Store.Query<AppUser>().Single(u => u.Id == x.UserId);
            return new MemberView(x.Id, u.DisplayName, u.Email, x.Role.ToString(), x.IsActive && u.IsEnabled, Store.Query<DemoDirectoryRecord>().SingleOrDefault(d => d.MemberId == x.Id)?.State);
        }).ToList();
    }
    public async Task SetEnabledAsync(Guid actor, Guid id, bool enabled, CancellationToken ct)
    {
        await using var tx = await Store.LockAsync(ct);
        var caller = context.Require(actor, MemberRole.Administrator);
        var member = Store.Query<OrganizationMember>().SingleOrDefault(x => x.Id == id && x.OrganizationId == context.OrganizationId)
            ?? throw new BusinessException(404, "member_not_found", "Member not found.");
        if (caller.Role != MemberRole.Owner && member.Role != MemberRole.Employee || member.Role == MemberRole.Owner)
            throw new BusinessException(403, "role_hierarchy", "Managing this role is not allowed. Disabling an Owner is not supported.");
        ApplyEnabled(member, enabled, actor);
        await Store.CommitAsync(ct);
    }
    public void ApplyEnabled(OrganizationMember member, bool enabled, Guid? actor)
    {
        if (member.Role == MemberRole.Owner && !enabled) return;
        var user = Store.Query<AppUser>().Single(x => x.Id == member.UserId);
        if (member.IsActive == enabled && user.IsEnabled == enabled) return;
        member.IsActive = enabled; member.DeactivatedAtUtc = enabled ? null : context.Now;
        user.IsEnabled = enabled; user.DisabledAtUtc = enabled ? null : context.Now;
        if (!enabled)
        {
            foreach (var session in Store.Query<UserSession>().Where(x => x.MemberId == member.Id && x.RevokedAtUtc == null).ToList()) session.RevokedAtUtc = context.Now;
            reservations.ExpireDue();
            reservations.ResolveDisruption(Store.Query<Reservation>().Where(x => x.MemberId == member.Id && x.OrganizationId == context.OrganizationId &&
                (x.Status == ReservationStatus.Reserved || x.Status == ReservationStatus.Confirmed)).ToList(), actor, CancellationReason.AccountDisabled, "Account disabled.");
        }
        context.Audit(actor, enabled ? "EnableMember" : "DisableMember", "Member", member.Id);
    }
    public async Task RoleAsync(Guid actor, Guid id, string role, CancellationToken ct)
    {
        WorkspaceContext.Check(role is "Employee" or "Administrator", "invalid_role", "Allowed roles are Employee and Administrator.");
        await using var tx = await Store.LockAsync(ct);
        context.Require(actor, MemberRole.Owner);
        var member = Store.Query<OrganizationMember>().SingleOrDefault(x => x.Id == id && x.OrganizationId == context.OrganizationId)
            ?? throw new BusinessException(404, "member_not_found", "Member not found.");
        if (member.Role == MemberRole.Owner) throw new BusinessException(403, "owner_protected", "The Owner role is protected.");
        member.Role = Enum.Parse<MemberRole>(role);
        context.Audit(actor, "SetRole", "Member", id, role);
        await Store.CommitAsync(ct);
    }
}
