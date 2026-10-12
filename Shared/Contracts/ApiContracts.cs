namespace Deskovia.Shared.Contracts;

public sealed record RegisterRequest(string Name, string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record SignInOptions(bool MicrosoftEnabled, bool MicrosoftConfigured);
public sealed record CurrentUser(Guid UserId, Guid MemberId, Guid OrganizationId, string Name, string Email, string Role);
public sealed record AntiforgeryToken(string Token);
public sealed record WorkingHoursInput(DayOfWeek Day, TimeOnly OpensAt, TimeOnly ClosesAt);
public sealed record OfficeInput(string Name, string TimeZoneId, int BookingHorizonDays, List<WorkingHoursInput> Hours);
public sealed record RoomInput(Guid OfficeId, string Name, decimal X, decimal Y, decimal Width, decimal Height);
public sealed record DeskInput(Guid RoomId, string Label, decimal X, decimal Y, decimal Rotation);
public sealed record OfficeView(Guid Id, string Name, string TimeZoneId, int BookingHorizonDays, bool IsArchived, DateOnly Today, List<WorkingHoursInput> Hours, DateTimeOffset ServerNow);
public sealed record RoomView(Guid Id, Guid OfficeId, string Name, decimal X, decimal Y, decimal Width, decimal Height, bool IsArchived);
public sealed record DeskView(Guid Id, Guid RoomId, string Label, decimal X, decimal Y, decimal Rotation, bool IsArchived);
public sealed record OfficeMap(List<RoomView> Rooms, List<DeskView> Desks);
public sealed record BookingRequest(Guid DeskId, List<DateOnly> Dates, bool IsFullDay, TimeOnly? LocalStart, TimeOnly? LocalEnd);
public sealed record DateAvailability(DateOnly Date, bool Available, string? Code, string? Reason, DateTimeOffset? Start, DateTimeOffset? End);
public sealed record Occupancy(Guid DeskId, string Name, DateTimeOffset Start, DateTimeOffset End);
public sealed record ReservationView(Guid Id, Guid? BatchId, Guid MemberId, Guid OfficeId, Guid RoomId, Guid DeskId, string Office, string Room, string Desk, string Member, string TimeZoneId, DateTimeOffset Start, DateTimeOffset End, bool IsFullDay, string Status, string? CancellationReason, DateTimeOffset? CompletedAt, bool CanConfirm, bool CanCancel, bool CanComplete);
public sealed record PageResult<T>(List<T> Items, int Total, int Page, int PageSize);
public sealed record ReasonRequest(string Reason);
public sealed record RoleRequest(string Role);
public sealed record ClosureRequest(Guid OfficeId, Guid? RoomId, DateTimeOffset Start, DateTimeOffset End, string Reason, List<Guid>? AcknowledgedReservationIds);
public sealed record ClosureView(Guid Id, Guid OfficeId, Guid? RoomId, DateTimeOffset Start, DateTimeOffset End, string Reason, bool Cancelled);
public sealed record MemberView(Guid Id, string Name, string Email, string Role, bool Active, string? DirectoryState);
public sealed record DirectoryRequest(string? State);
public sealed record DirectoryRunView(DateTimeOffset? LastRun, string Result);
public sealed record NotificationView(Guid Id, string Recipient, string Subject, string Body, string Status, DateTimeOffset ScheduledAt, DateTimeOffset? SentAt, int Attempts, string? Error);
public sealed record BillingView(bool Enabled, string Adapter, decimal DemoPrice, string Currency, string Status, DateTimeOffset? PeriodStart, DateTimeOffset? PeriodEnd);
public sealed record PaymentRequest(string IdempotencyKey, bool Succeed);
public sealed record PaymentView(Guid Id, string Status, decimal Amount, string Currency, DateTimeOffset Start, DateTimeOffset End, bool IsTest);
public sealed record BookingBatchResult(Guid BatchId, List<ReservationView> Reservations);
public sealed record ApiProblem(string Title, int Status, string Code, object? Details = null, Dictionary<string, string[]>? Errors = null)
{
    public string Type { get; init; } = "about:blank";
}
