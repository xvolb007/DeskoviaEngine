using System.Security.Claims;
using Deskovia.Application.Services;
using Deskovia.Shared.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Deskovia.WebApi.Security;

public sealed class SessionCookieEvents(AuthService auth, WorkspaceContext workspace) : CookieAuthenticationEvents
{
    public static ClaimsPrincipal Principal(CurrentUser user, Guid sessionId) => new(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.NameIdentifier, user.MemberId.ToString()), new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role), new Claim("session", sessionId.ToString()) }, "Cookies"));
    public override Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var valid = Guid.TryParse(context.Principal?.FindFirstValue("session"), out var id) ? auth.ValidateSession(id) : null;
        if (valid == null) context.RejectPrincipal();
        else { context.ReplacePrincipal(Principal(valid, id)); workspace.SessionId = id; }
        return Task.CompletedTask;
    }
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) => Error(context, 401, "unauthorized");
    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) => Error(context, 403, "forbidden");
    private static Task Error(RedirectContext<CookieAuthenticationOptions> c, int status, string code)
    {
        c.Response.StatusCode = status;
        return c.Response.WriteAsJsonAsync(new { type = "about:blank", title = code, status, code });
    }
}
