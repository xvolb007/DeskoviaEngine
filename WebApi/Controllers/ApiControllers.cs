using System.Security.Claims;
using Deskovia.Application.Services;
using Deskovia.Shared.Contracts;
using Deskovia.WebApi.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Deskovia.WebApi.Controllers;

[ApiController, Authorize]
[ProducesResponseType(typeof(ApiProblem), 400)]
[ProducesResponseType(typeof(ApiProblem), 401)]
[ProducesResponseType(typeof(ApiProblem), 403)]
[ProducesResponseType(typeof(ApiProblem), 404)]
[ProducesResponseType(typeof(ApiProblem), 409)]
[ProducesResponseType(typeof(ApiProblem), 429)]
[ProducesResponseType(typeof(ApiProblem), 500)]
public abstract class WorkspaceController : ControllerBase
{
    protected Guid Actor => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
[Route("api/auth")]
public sealed class AuthController(AuthService auth, IAntiforgery antiforgery, EntraOptions entra) : WorkspaceController
{
    [HttpGet("antiforgery"), AllowAnonymous]
    public AntiforgeryToken Token() => new(antiforgery.GetAndStoreTokens(HttpContext).RequestToken!);
    [HttpPost("register"), AllowAnonymous, EnableRateLimiting("credentials")]
    public async Task<CurrentUser> Register(RegisterRequest input, CancellationToken ct)
    {
        var r = await auth.RegisterAsync(input, ct); await SignIn(r.User, r.Session.Id, r.Session.ExpiresAtUtc); return r.User;
    }
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("credentials")]
    public async Task<CurrentUser> Login(LoginRequest input, CancellationToken ct)
    {
        var r = await auth.LoginAsync(input, ct); await SignIn(r.User, r.Session.Id, r.Session.ExpiresAtUtc); return r.User;
    }
    [HttpGet("options"), AllowAnonymous]
    public SignInOptions Options() => new(entra.Enabled, entra.Configured);
    [HttpPost("microsoft"), AllowAnonymous, EnableRateLimiting("credentials")]
    public IActionResult Microsoft([FromForm] string returnUrl = "/")
    {
        if (!entra.Configured) return Redirect(entra.ErrorUrl("microsoft_not_configured"));
        var properties = new AuthenticationProperties { RedirectUri = entra.ReturnUrl(returnUrl) };
        return Challenge(properties, EntraOptions.Scheme);
    }
    private Task SignIn(CurrentUser u, Guid id, DateTimeOffset expires) => HttpContext.SignInAsync("Cookies", SessionCookieEvents.Principal(u, id),
        new AuthenticationProperties { IsPersistent = false, AllowRefresh = false, ExpiresUtc = expires });
    [HttpGet("me")] public CurrentUser Me() => auth.ValidateSession(Guid.Parse(User.FindFirstValue("session")!)) ?? throw new BusinessException(401, "unauthorized", "Sign in again.");
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(Guid.Parse(User.FindFirstValue("session")!), ct); await HttpContext.SignOutAsync("Cookies"); return NoContent();
    }
}
[Route("api/offices")]
public sealed class OfficesController(AdministrationService admin, ReservationService reservations) : WorkspaceController
{
    [HttpGet] public List<OfficeView> List(bool archived = false) => admin.Offices(Actor, archived);
    [HttpGet("{id:guid}/map")] public OfficeMap Map(Guid id, bool archived = false) => admin.Map(Actor, id, archived);
    [HttpGet("{id:guid}/availability")] public Task<List<Occupancy>> Availability(Guid id, DateOnly date, TimeOnly? start, TimeOnly? end, CancellationToken ct) => reservations.OccupancyAsync(Actor, id, date, start, end, ct);
}
[Route("api/reservations")]
public sealed class ReservationsController(ReservationService reservations) : WorkspaceController
{
    [HttpPost("availability")] public Task<List<DateAvailability>> Availability(BookingRequest input, CancellationToken ct) => reservations.AvailabilityAsync(Actor, input, ct);
    [HttpPost("batch")] public Task<BookingBatchResult> Batch(BookingRequest input, CancellationToken ct) => reservations.CreateBatchAsync(Actor, input, ct);
    [HttpGet("mine")] public PageResult<ReservationView> Mine(bool history = false, int page = 1, int pageSize = 20) => reservations.History(Actor, false, history, page, pageSize);
    [HttpGet("{id:guid}")] public ReservationView Get(Guid id) => reservations.Get(Actor, id);
    [HttpPost("{id:guid}/confirm")] public async Task<IActionResult> Confirm(Guid id, CancellationToken ct) { await reservations.ChangeAsync(Actor, id, "confirm", null, ct); return NoContent(); }
    [HttpPost("{id:guid}/complete")] public async Task<IActionResult> Complete(Guid id, CancellationToken ct) { await reservations.ChangeAsync(Actor, id, "complete", null, ct); return NoContent(); }
    [HttpPost("{id:guid}/cancel")] public async Task<IActionResult> Cancel(Guid id, ReasonRequest input, CancellationToken ct) { await reservations.ChangeAsync(Actor, id, "cancel", input.Reason, ct); return NoContent(); }
}
[Route("api/admin"), Authorize(Roles = "Administrator,Owner")]
public sealed class AdminController(AdministrationService admin, ReservationService reservations) : WorkspaceController
{
    [HttpGet("reservations")] public PageResult<ReservationView> History(int page = 1, int pageSize = 20, Guid? officeId = null, Guid? memberId = null, string? status = null) => reservations.History(Actor, true, true, page, pageSize, officeId, memberId, status);
    [HttpPost("offices")] public Task<Guid> CreateOffice(OfficeInput input, CancellationToken ct) => admin.SaveOfficeAsync(Actor, null, input, ct);
    [HttpPut("offices/{id:guid}")] public Task<Guid> UpdateOffice(Guid id, OfficeInput input, CancellationToken ct) => admin.SaveOfficeAsync(Actor, id, input, ct);
    [HttpPost("rooms")] public Task<Guid> CreateRoom(RoomInput input, CancellationToken ct) => admin.SaveRoomAsync(Actor, null, input, ct);
    [HttpPut("rooms/{id:guid}")] public Task<Guid> UpdateRoom(Guid id, RoomInput input, CancellationToken ct) => admin.SaveRoomAsync(Actor, id, input, ct);
    [HttpPost("desks")] public Task<Guid> CreateDesk(DeskInput input, CancellationToken ct) => admin.SaveDeskAsync(Actor, null, input, ct);
    [HttpPut("desks/{id:guid}")] public Task<Guid> UpdateDesk(Guid id, DeskInput input, CancellationToken ct) => admin.SaveDeskAsync(Actor, id, input, ct);
    [HttpPost("{resource}/{id:guid}/archive")] public async Task<IActionResult> Archive(string resource, Guid id, CancellationToken ct) { await admin.ArchiveAsync(Actor, resource, id, ct); return NoContent(); }
    [HttpPost("closures/preview")] public Task<List<ReservationView>> Preview(ClosureRequest input, CancellationToken ct) => admin.PreviewAsync(Actor, input, ct);
    [HttpPost("closures")] public Task<Guid> Close(ClosureRequest input, CancellationToken ct) => admin.CloseAsync(Actor, input, ct);
    [HttpGet("closures")] public List<ClosureView> Closures() => admin.Closures(Actor);
    [HttpPost("closures/{id:guid}/cancel")] public async Task<IActionResult> CancelClosure(Guid id, CancellationToken ct) { await admin.CancelClosureAsync(Actor, id, ct); return NoContent(); }
    [HttpGet("members")] public List<MemberView> Members() => admin.Members(Actor);
    [HttpPost("members/{id:guid}/disable")] public async Task<IActionResult> Disable(Guid id, CancellationToken ct) { await admin.SetEnabledAsync(Actor, id, false, ct); return NoContent(); }
    [HttpPost("members/{id:guid}/enable")] public async Task<IActionResult> Enable(Guid id, CancellationToken ct) { await admin.SetEnabledAsync(Actor, id, true, ct); return NoContent(); }
}
[Route("api/owner"), Authorize(Roles = "Owner")]
public sealed class OwnerController(AdministrationService admin, OperationsService operations) : WorkspaceController
{
    [HttpPut("members/{id:guid}/role")] public async Task<IActionResult> Role(Guid id, RoleRequest input, CancellationToken ct) { await admin.RoleAsync(Actor, id, input.Role, ct); return NoContent(); }
    [HttpPut("demo-directory/{id:guid}")] public async Task<IActionResult> Directory(Guid id, DirectoryRequest input, CancellationToken ct) { await operations.SetDirectoryAsync(Actor, id, input.State, ct); return NoContent(); }
    [HttpGet("demo-directory")] public DirectoryRunView DirectoryStatus() => operations.DirectoryStatus(Actor);
    [HttpPost("demo-directory/sync")] public async Task<IActionResult> Sync(CancellationToken ct) { await operations.SyncDirectoryAsync(Actor, true, ct); return NoContent(); }
    [HttpGet("notifications")] public List<NotificationView> Notifications() => operations.Notifications(Actor);
    [HttpGet("billing")] public BillingView Billing() => operations.Billing(Actor);
    [HttpPost("billing/simulate")] public Task<PaymentView> Pay(PaymentRequest input, CancellationToken ct) => operations.PayAsync(Actor, input, ct);
}
