using System.Security.Claims;
using Deskovia.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Deskovia.WebApi.Security;

public sealed class EntraEvents(AuthService auth, EntraOptions entra, ILogger<EntraEvents> logger) : OpenIdConnectEvents
{
    public override Task RedirectToIdentityProvider(RedirectContext context)
    {
        SecureHeaders(context.Response);
        context.ProtocolMessage.Prompt = "select_account";
        return Task.CompletedTask;
    }
    public override Task TokenValidated(TokenValidatedContext context)
    {
        if (!Guid.TryParse(context.Principal?.FindFirstValue("tid"), out var tenant) || tenant != entra.TenantId ||
            !Guid.TryParse(context.Principal?.FindFirstValue("oid"), out var oid) || oid == Guid.Empty)
            context.Fail("Microsoft identity does not belong to the configured tenant.");
        return Task.CompletedTask;
    }
    public override async Task TicketReceived(TicketReceivedContext context)
    {
        SecureHeaders(context.Response);
        // TicketReceived runs after nonce, correlation, signature, issuer, audience and lifetime checks.
        try
        {
            var principal = context.Principal!;
            var email = principal.FindFirstValue("email") ?? principal.FindFirstValue("preferred_username");
            var result = await auth.MicrosoftLoginAsync(Guid.Parse(principal.FindFirstValue("tid")!), Guid.Parse(principal.FindFirstValue("oid")!), email,
                principal.FindFirstValue("name"), context.HttpContext.RequestAborted);
            await context.HttpContext.SignInAsync("Cookies", SessionCookieEvents.Principal(result.User, result.Session.Id),
                new AuthenticationProperties { IsPersistent = false, AllowRefresh = false, ExpiresUtc = result.Session.ExpiresAtUtc });
            context.Response.Redirect(context.ReturnUri ?? entra.FrontendOrigin + "/");
        }
        catch (BusinessException e) { context.Response.Redirect(entra.ErrorUrl(e.Code)); }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError("Microsoft account processing failed ({FailureType}).", e.GetType().Name);
            context.Response.Redirect(entra.ErrorUrl("microsoft_signin_failed"));
        }
        context.HandleResponse();
    }
    public override Task RemoteFailure(RemoteFailureContext context)
    {
        SecureHeaders(context.Response);
        // Never expose provider errors, codes, tokens or secrets in frontend URLs.
        logger.LogWarning("Microsoft sign-in was rejected ({FailureType}).", context.Failure?.GetType().Name);
        context.Response.Redirect(entra.ErrorUrl("microsoft_signin_failed"));
        context.HandleResponse();
        return Task.CompletedTask;
    }
    private static void SecureHeaders(HttpResponse response)
    {
        response.Headers.CacheControl = "no-store";
        response.Headers.Pragma = "no-cache";
        response.Headers["Referrer-Policy"] = "no-referrer";
    }
}
