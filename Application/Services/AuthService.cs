using System.Net.Mail;
using Deskovia.Application.Interfaces;
using Deskovia.Domain.Entities;
using Deskovia.Domain.Enums;
using Deskovia.Shared.Contracts;

namespace Deskovia.Application.Services;

public sealed class AuthService(WorkspaceContext context, IPasswordService passwords)
{
    private IWorkspaceStore Store => context.Store;
    public static string NormalizeEmail(string email)
    {
        email = email.Trim();
        WorkspaceContext.Check(email.Length <= 320 && MailAddress.TryCreate(email, out var parsed) && parsed.Address == email, "invalid_email", "Invalid email address.");
        return email.ToUpperInvariant();
    }
    private static void ValidatePassword(string password) => WorkspaceContext.Check(password.Length is >= 12 and <= 128, "invalid_password", "Password must contain 12–128 characters.");
    public async Task<(CurrentUser User, UserSession Session)> RegisterAsync(RegisterRequest input, CancellationToken ct)
    {
        var email = NormalizeEmail(input.Email);
        var name = WorkspaceContext.Text(input.Name, 100);
        ValidatePassword(input.Password);
        var hash = passwords.Hash(input.Password);
        await using var tx = await Store.LockAsync(ct);
        if (!Store.Query<Organization>().Any(x => x.Id == context.OrganizationId && !x.IsArchived))
            throw new BusinessException(409, "bootstrap_required", "ABC is unavailable. Restart the API and check the organisation status.");
        if (Store.Query<AppUser>().Any(x => x.NormalizedEmail == email))
            throw new BusinessException(409, "email_exists", "This email address is already registered.");
        var user = new AppUser { Email = input.Email.Trim(), NormalizedEmail = email, DisplayName = name, CreatedAtUtc = context.Now };
        // The transaction lock makes the first-member decision atomic across API instances.
        var role = Store.Query<OrganizationMember>().Any(x => x.OrganizationId == context.OrganizationId)
            ? MemberRole.Employee : MemberRole.Owner;
        var member = new OrganizationMember { UserId = user.Id, OrganizationId = context.OrganizationId, Role = role, JoinedAtUtc = context.Now };
        Store.Add(user);
        Store.Add(member);
        Store.Add(new LocalCredential { UserId = user.Id, PasswordHash = hash, PasswordChangedAtUtc = context.Now });
        var session = NewSession(member);
        context.Audit(member.Id, "Register", "Member", member.Id);
        await Store.CommitAsync(ct);
        return (View(user, member), session);
    }
    public async Task<(CurrentUser User, UserSession Session)> LoginAsync(LoginRequest input, CancellationToken ct)
    {
        var email = NormalizeEmail(input.Email);
        await using var tx = await Store.LockAsync(ct);
        var user = Store.Query<AppUser>().SingleOrDefault(x => x.NormalizedEmail == email);
        var member = user == null ? null : Store.Query<OrganizationMember>().SingleOrDefault(x => x.UserId == user.Id && x.OrganizationId == context.OrganizationId);
        var credential = user == null ? null : Store.Query<LocalCredential>().SingleOrDefault(x => x.UserId == user.Id);
        if (user == null || member == null || credential == null || !user.IsEnabled || !member.IsActive ||
            !passwords.Verify(credential.PasswordHash, input.Password, out _))
            throw new BusinessException(401, "invalid_credentials", "Invalid credentials or unavailable account.");
        passwords.Verify(credential.PasswordHash, input.Password, out var rehash);
        if (rehash) credential.PasswordHash = passwords.Hash(input.Password);
        user.LastSignInAtUtc = context.Now;
        var session = NewSession(member);
        await Store.CommitAsync(ct);
        return (View(user, member), session);
    }
    // Called only after the server has validated Microsoft's OIDC response.
    public async Task<(CurrentUser User, UserSession Session)> MicrosoftLoginAsync(Guid tenantId, Guid objectId, string? email, string? name, CancellationToken ct)
    {
        WorkspaceContext.Check(tenantId != Guid.Empty && objectId != Guid.Empty, "invalid_microsoft_identity", "Microsoft identity is incomplete.");
        await using var tx = await Store.LockAsync(ct);
        if (!Store.Query<Organization>().Any(x => x.Id == context.OrganizationId && !x.IsArchived))
            throw new BusinessException(409, "bootstrap_required", "ABC is unavailable.");
        var user = Store.Query<AppUser>().SingleOrDefault(x => x.MicrosoftTenantId == tenantId && x.MicrosoftObjectId == objectId);
        if (user == null)
        {
            if (string.IsNullOrWhiteSpace(email)) throw new BusinessException(400, "microsoft_email_unavailable", "Microsoft did not provide an email address.");
            var normalized = NormalizeEmail(email);
            if (Store.Query<AppUser>().Any(x => x.NormalizedEmail == normalized))
                throw new BusinessException(409, "email_exists", "An account with this email already exists. Use its existing sign-in method.");
            user = new AppUser { Email = email.Trim(), NormalizedEmail = normalized, DisplayName = WorkspaceContext.Text(string.IsNullOrWhiteSpace(name) ? email : name, 100), MicrosoftTenantId = tenantId, MicrosoftObjectId = objectId, CreatedAtUtc = context.Now };
            Store.Add(user);
            var first = !Store.Query<OrganizationMember>().Any(x => x.OrganizationId == context.OrganizationId);
            var created = new OrganizationMember { UserId = user.Id, OrganizationId = context.OrganizationId, Role = first ? MemberRole.Owner : MemberRole.Employee, JoinedAtUtc = context.Now };
            Store.Add(created);
            // Querying an EF DbSet does not include newly added entities before SaveChanges.
            user.LastSignInAtUtc = context.Now;
            var createdSession = NewSession(created);
            context.Audit(created.Id, "MicrosoftRegister", "Member", created.Id);
            await Store.CommitAsync(ct);
            return (View(user, created), createdSession);
        }
        var member = Store.Query<OrganizationMember>().SingleOrDefault(x => x.UserId == user.Id && x.OrganizationId == context.OrganizationId);
        if (!user.IsEnabled || member == null || !member.IsActive)
            throw new BusinessException(401, "account_unavailable", "This account is disabled or unavailable.");
        user.LastSignInAtUtc = context.Now;
        var session = NewSession(member);
        context.Audit(member.Id, "MicrosoftSignIn", "Member", member.Id);
        await Store.CommitAsync(ct);
        return (View(user, member), session);
    }
    private UserSession NewSession(OrganizationMember member)
    {
        var session = new UserSession { MemberId = member.Id, CreatedAtUtc = context.Now, ExpiresAtUtc = context.Now.AddHours(8) };
        Store.Add(session);
        return session;
    }
    public CurrentUser? ValidateSession(Guid sessionId)
    {
        var session = Store.Query<UserSession>().SingleOrDefault(x => x.Id == sessionId);
        if (session == null || session.RevokedAtUtc != null || session.ExpiresAtUtc <= context.Now) return null;
        var member = Store.Query<OrganizationMember>().SingleOrDefault(x => x.Id == session.MemberId && x.OrganizationId == context.OrganizationId && x.IsActive);
        if (member == null) return null;
        var user = Store.Query<AppUser>().SingleOrDefault(x => x.Id == member.UserId && x.IsEnabled);
        return user == null ? null : View(user, member);
    }
    private static CurrentUser View(AppUser u, OrganizationMember m) => new(u.Id, m.Id, m.OrganizationId, u.DisplayName, u.Email, m.Role.ToString());
    public async Task LogoutAsync(Guid sessionId, CancellationToken ct)
    {
        await using var tx = await Store.LockAsync(ct);
        var session = Store.Query<UserSession>().SingleOrDefault(x => x.Id == sessionId);
        if (session != null && session.RevokedAtUtc == null) session.RevokedAtUtc = context.Now;
        await Store.CommitAsync(ct);
    }
    public async Task BootstrapAsync(string name, string email, string password, CancellationToken ct)
    {
        var normalized = NormalizeEmail(email);
        ValidatePassword(password);
        name = WorkspaceContext.Text(name, 100);
        var hash = passwords.Hash(password);
        await using var tx = await Store.LockAsync(ct);
        var organization = Store.Query<Organization>().SingleOrDefault(x => x.Id == context.OrganizationId);
        if (organization == null) Store.Add(new Organization { Id = context.OrganizationId, Name = "ABC", CreatedAtUtc = context.Now });
        var existing = Store.Query<AppUser>().SingleOrDefault(x => x.NormalizedEmail == normalized);
        if (existing != null)
        {
            if (!Store.Query<OrganizationMember>().Any(x => x.UserId == existing.Id && x.OrganizationId == context.OrganizationId && x.Role == MemberRole.Owner && x.IsActive))
                throw new BusinessException(409, "bootstrap_email_in_use", "This email belongs to an existing account; automatic promotion is not allowed.");
        }
        else
        {
            if (Store.Query<OrganizationMember>().Any(x => x.OrganizationId == context.OrganizationId && x.Role == MemberRole.Owner && x.IsActive))
                throw new BusinessException(409, "owner_exists", "ABC already has an Owner. Automatic ownership transfer is not allowed.");
            var user = new AppUser { DisplayName = name, Email = email.Trim(), NormalizedEmail = normalized, CreatedAtUtc = context.Now };
            Store.Add(user);
            Store.Add(new LocalCredential { UserId = user.Id, PasswordHash = hash, PasswordChangedAtUtc = context.Now });
            Store.Add(new OrganizationMember { UserId = user.Id, OrganizationId = context.OrganizationId, Role = MemberRole.Owner, JoinedAtUtc = context.Now });
        }
        await Store.CommitAsync(ct);
    }
}
