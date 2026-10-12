using System.Globalization;
using System.Threading.RateLimiting;
using Deskovia.Application.Interfaces;
using Deskovia.Application.Services;
using Deskovia.Infrastructure.Identity;
using Deskovia.Infrastructure.Notifications;
using Deskovia.Infrastructure.Payments;
using Deskovia.Infrastructure.Persistence;
using Deskovia.WebApi.Security;
using Deskovia.WebApi.Workers;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-GB");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-GB");

var builder = WebApplication.CreateBuilder(args);
var connectionString = SqlServerDatabase.ConnectionString(builder.Configuration);
var prototype = builder.Configuration.GetSection("Prototype").Get<PrototypeOptions>() ?? new();
var billing = builder.Configuration.GetSection("Billing").Get<BillingOptions>() ?? new();
if (billing.DemoMonthlyPrice < 0 || billing.DemoMonthlyPrice > 9999999999.99m || decimal.Round(billing.DemoMonthlyPrice, 2) != billing.DemoMonthlyPrice || billing.Currency.Length != 3 || !billing.Currency.All(c => c is >= 'A' and <= 'Z'))
    throw new InvalidOperationException("Billing requires a nonnegative demo price and uppercase currency.");
if (!Uri.TryCreate(prototype.FrontendOrigin, UriKind.Absolute, out var frontend) || frontend.Scheme != "https" || frontend.GetLeftPart(UriPartial.Authority) != prototype.FrontendOrigin)
    throw new InvalidOperationException("Prototype:FrontendOrigin must be an exact HTTPS origin.");
var entra = builder.Configuration.GetSection("Entra").Get<EntraOptions>() ?? new();
entra.FrontendOrigin = prototype.FrontendOrigin;
builder.Services.AddSingleton(entra);
builder.Services.AddScoped<EntraEvents>();
builder.Services.AddSingleton(prototype);
builder.Services.AddSingleton(billing);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<DeskoviaDbContext>(o => SqlServerDatabase.Configure(o, connectionString));
builder.Services.AddScoped<IWorkspaceStore, WorkspaceStore>();
builder.Services.AddScoped<WorkspaceContext>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<OrganizationService>();
builder.Services.AddScoped<ReservationService>();
builder.Services.AddScoped<AdministrationService>();
builder.Services.AddScoped<OperationsService>();
builder.Services.AddSingleton<IPasswordService, LocalPasswordService>();
builder.Services.AddSingleton<IDirectoryAdapter, DemoDirectoryAdapter>();
builder.Services.AddSingleton<IPaymentAdapter, LocalPaymentAdapter>();
builder.Services.AddSingleton<IEmailSink, LocalEmailSink>();
builder.Services.AddScoped<SessionCookieEvents>();
var authentication = builder.Services.AddAuthentication("Cookies").AddCookie("Cookies", o =>
{
    o.Cookie.Name = "__Host-Deskovia.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.Path = "/";
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = false;
    o.EventsType = typeof(SessionCookieEvents);
});
if (entra.Configured)
{
    authentication.AddOpenIdConnect(EntraOptions.Scheme, o =>
    {
        o.SignInScheme = "Cookies";
        o.Authority = $"https://login.microsoftonline.com/{entra.TenantId:D}/v2.0";
        o.ClientId = entra.ClientId.ToString();
        o.ClientSecret = entra.ClientSecret;
        o.CallbackPath = "/signin-oidc";
        o.ResponseType = "code";
        // Return through a top-level GET with the existing SameSite=Lax cookie policy.
        o.ResponseMode = "query";
        o.UsePkce = true;
        o.RequireHttpsMetadata = true;
        o.MapInboundClaims = false;
        o.SaveTokens = false;
        o.Scope.Clear();
        o.Scope.Add("openid");
        o.Scope.Add("profile");
        o.Scope.Add("email");
        o.TokenValidationParameters.ValidateIssuer = true;
        o.TokenValidationParameters.ValidIssuer = $"https://login.microsoftonline.com/{entra.TenantId:D}/v2.0";
        o.TokenValidationParameters.ValidateAudience = true;
        o.TokenValidationParameters.ValidAudience = entra.ClientId.ToString();
        o.TokenValidationParameters.ValidateLifetime = true;
        o.EventsType = typeof(EntraEvents);
    });
}
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.Name = "__Host-Deskovia.Antiforgery";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Lax;
});
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(prototype.FrontendOrigin).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("credentials", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.OnRejected = async (c, ct) =>
    {
        c.HttpContext.Response.StatusCode = 429;
        await c.HttpContext.Response.WriteAsJsonAsync(new { title = "Too many attempts.", status = 429, code = "rate_limited" }, ct);
    };
});
builder.Services.AddControllers().ConfigureApiBehaviorOptions(o =>
{
    o.InvalidModelStateResponseFactory = c =>
    {
        var d = new ValidationProblemDetails(c.ModelState) { Status = 400, Title = "Check the request fields." };
        d.Extensions["code"] = "validation_failed"; return new BadRequestObjectResult(d);
    };
});
builder.Services.AddOpenApi();
if (!args.Contains("--verify-host"))
{
    // Initialisation completes before workers and HTTP requests can use ABC.
    builder.Services.AddHostedService<PrototypeInitializer>();
    builder.Services.AddHostedService<PrototypeWorker>();
}
var app = builder.Build();
if (args.Contains("--bootstrap-owner"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AuthService>().BootstrapAsync(
        builder.Configuration["Bootstrap:Name"] ?? "ABC Owner",
        builder.Configuration["Bootstrap:Email"] ?? throw new InvalidOperationException("Set Bootstrap:Email."),
        builder.Configuration["Bootstrap:Password"] ?? throw new InvalidOperationException("Set Bootstrap:Password through secrets."),
        CancellationToken.None);
    Console.WriteLine("ABC/Owner initialized; existing credentials and roles preserved."); return;
}
app.Use(async (c, next) =>
{
    try { await next(); }
    catch (BusinessException e)
    {
        c.Response.StatusCode = e.Status;
        await c.Response.WriteAsJsonAsync(new { type = "about:blank", title = e.Message, status = e.Status, code = e.Code, details = e.Details, errors = e.FieldErrors });
    }
    catch (AntiforgeryValidationException)
    {
        c.Response.StatusCode = 400;
        await c.Response.WriteAsJsonAsync(new { title = "Refresh the security token.", status = 400, code = "antiforgery_failed" });
    }
    catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
    {
        c.Response.StatusCode = 409;
        await c.Response.WriteAsJsonAsync(new { title = "This data already exists.", status = 409, code = "unique_conflict" });
    }
    catch (DbUpdateConcurrencyException)
    {
        c.Response.StatusCode = 409;
        await c.Response.WriteAsJsonAsync(new { title = "The data has changed. Refresh the page.", status = 409, code = "concurrency_conflict" });
    }
    catch (Exception e) when (e is not OperationCanceledException)
    {
        app.Logger.LogError("Request failed ({FailureType}); check configuration/database.", e.GetType().Name);
        c.Response.StatusCode = 500;
        await c.Response.WriteAsJsonAsync(new { title = "The server could not process the request. Check the database connection and applied migration.", status = 500, code = "server_error" });
    }
});
app.UseStatusCodePages(async statusContext =>
{
    var c = statusContext.HttpContext;
    if (c.Request.Path.StartsWithSegments("/api"))
        await c.Response.WriteAsJsonAsync(new { type = "about:blank", title = "The request could not be completed.", status = c.Response.StatusCode, code = "http_" + c.Response.StatusCode });
});
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (c, next) =>
{
    c.Response.Headers.CacheControl = "no-store";
    if (c.Request.Path.StartsWithSegments("/api") && c.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
        await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);
    await next();
});
if (app.Environment.IsDevelopment()) app.MapOpenApi().AllowAnonymous();
app.MapControllers();
app.Run();
