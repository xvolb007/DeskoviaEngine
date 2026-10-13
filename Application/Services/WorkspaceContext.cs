using Deskovia.Application.Interfaces;
using Deskovia.Domain.Entities;
using Deskovia.Domain.Enums;
using Deskovia.Shared.Contracts;

namespace Deskovia.Application.Services;

public sealed class BusinessException(int status, string code, string message, object? details = null) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public object? Details { get; } = details;
    public Dictionary<string, string[]>? FieldErrors => Status != 400 ? null : new()
    {
        [Code switch
        {
            "invalid_email" => "Email", "invalid_password" => "Password", "invalid_dates" => "Dates",
            "invalid_timezone" => "TimeZoneId", "invalid_schedule" => "Hours", "invalid_geometry" => "Geometry",
            "invalid_role" => "Role", "invalid_directory_state" => "State", "invalid_closure" => "Interval",
            _ => "Input"
        }] = [Message]
    };
}
public sealed class PrototypeOptions
{
    public Guid OrganizationId { get; set; } = Guid.Parse("abc00000-0000-0000-0000-000000000001");
    public string FrontendOrigin { get; set; } = "https://localhost:7178";
    public bool DemoDirectoryEnabled { get; set; } = true;
}
public sealed class WorkspaceContext(IWorkspaceStore store, TimeProvider clock, PrototypeOptions options)
{
    public IWorkspaceStore Store { get; } = store;
    public DateTimeOffset Now => clock.GetUtcNow();
    public Guid OrganizationId => options.OrganizationId;
    public Guid? SessionId { get; set; }
    public OrganizationMember Require(Guid memberId, MemberRole minimum = MemberRole.Employee)
    {
        if (SessionId != null && !Store.Query<UserSession>().Any(x => x.Id == SessionId && x.MemberId == memberId && x.RevokedAtUtc == null && x.ExpiresAtUtc > Now))
            throw new BusinessException(401, "session_unavailable", "The session is unavailable.");
        var member = Store.Query<OrganizationMember>().SingleOrDefault(x => x.Id == memberId && x.OrganizationId == OrganizationId);
        if (member == null || !member.IsActive || !Store.Query<AppUser>().Any(x => x.Id == member.UserId && x.IsEnabled))
            throw new BusinessException(401, "session_unavailable", "Sign in with an active account.");
        if (member.Role < minimum) throw new BusinessException(403, "forbidden", "You do not have permission to perform this action.");
        return member;
    }
    public static void Check(bool condition, string code, string message)
    {
        if (!condition) throw new BusinessException(400, code, message);
    }
    public static string Text(string? value, int max = 200)
    {
        var result = value?.Trim() ?? "";
        Check(result.Length > 0 && result.Length <= max, "invalid_text", $"Enter text containing 1–{max} characters.");
        return result;
    }
    public void Audit(Guid? actor, string action, string type, Guid id, string? details = null) =>
        Store.Add(new AuditEntry { OrganizationId = OrganizationId, ActorMemberId = actor, Action = action, EntityType = type, EntityId = id, Details = details, OccurredAtUtc = Now });
}
