using Deskovia.Domain.Entities;
using Deskovia.Domain.Enums;

namespace Deskovia.Application.Services;

public static class BookingRules
{
    public static bool DeskFits(decimal x, decimal y, decimal rotation, decimal width, decimal height)
    {
        var radians = (double)rotation * Math.PI / 180;
        var extent = (decimal)(6 * (Math.Abs(Math.Cos(radians)) + Math.Abs(Math.Sin(radians))));
        return rotation is >= 0 and < 360 && x + 6 - extent >= -0.000001m && y + 6 - extent >= -0.000001m &&
            x + 6 + extent <= width + 0.000001m && y + 6 + extent <= height + 0.000001m;
    }
    public static bool Active(Reservation r) => r.Status is ReservationStatus.Reserved or ReservationStatus.Confirmed;
    public static bool CanConfirm(Reservation r, DateTimeOffset now) =>
        r.Status == ReservationStatus.Reserved && now >= r.StartsAtUtc.AddHours(-1) && now < r.StartsAtUtc.AddMinutes(5);
    public static bool CanComplete(Reservation r, DateTimeOffset now) =>
        r.Status == ReservationStatus.Confirmed && now >= r.StartsAtUtc && now < r.EndsAtUtc;
    public static bool CanCancel(Reservation r, DateTimeOffset now) =>
        r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.Confirmed && now < r.StartsAtUtc;
    public static DateTimeOffset ToUtc(DateOnly day, TimeOnly time, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local))
            throw new BusinessException(400, "ambiguous_time", "This time falls within a daylight saving time transition. Choose another time.");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }
    public static (DateTimeOffset Start, DateTimeOffset End) Interval(Office office, OfficeWorkingHours? hours, DateOnly day, bool fullDay, TimeOnly? start, TimeOnly? end, DateTimeOffset now)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(office.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        WorkspaceContext.Check(day >= today && day <= today.AddDays(office.BookingHorizonDays), "outside_horizon", "The date is outside the booking horizon.");
        WorkspaceContext.Check(hours != null, "non_working_day", "The office is closed on this day.");
        var opens = hours!.OpensAt;
        var closes = hours.ClosesAt;
        var from = fullDay ? opens : start;
        var until = fullDay ? closes : end;
        WorkspaceContext.Check(from != null && until != null && from >= opens && until <= closes && until > from, "outside_hours", "The interval must be within office working hours.");
        WorkspaceContext.Check(from!.Value.Minute % 15 == 0 && until!.Value.Minute % 15 == 0 && from.Value.Second == 0 && until!.Value.Second == 0 &&
            from.Value.Ticks % TimeSpan.TicksPerMinute == 0 && until!.Value.Ticks % TimeSpan.TicksPerMinute == 0, "time_step", "Times must use 15-minute increments.");
        var result = (Start: ToUtc(day, from.Value, zone), End: ToUtc(day, until!.Value, zone));
        WorkspaceContext.Check(result.End - result.Start >= TimeSpan.FromMinutes(30), "short_interval", "The minimum duration is 30 minutes.");
        WorkspaceContext.Check(result.Start >= now, "past_time", "Past times cannot be booked; choose the next 15-minute boundary.");
        return result;
    }
}
