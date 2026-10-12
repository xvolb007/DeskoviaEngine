using Deskovia.Domain.Entities;
using Deskovia.Domain.Enums;

namespace Deskovia.Application.Services;

public sealed class OrganizationService(WorkspaceContext context)
{
    public async Task EnsureInitializedAsync(CancellationToken ct)
    {
        await using var tx = await context.Store.LockAsync(ct);
        var organization = context.Store.Query<Organization>().SingleOrDefault(x => x.Id == context.OrganizationId);
        if (organization == null)
        {
            organization = new Organization
            {
                Id = context.OrganizationId,
                Name = "ABC",
                IsArchived = false,
                CreatedAtUtc = context.Now
            };
            context.Store.Add(organization);
        }
        // Upgrade an existing prototype without requiring another account or setup command.
        // Never replace an existing Owner or reactivate a disabled account/archived organisation.
        if (!organization.IsArchived &&
            !context.Store.Query<OrganizationMember>().Any(x => x.OrganizationId == context.OrganizationId && x.Role == MemberRole.Owner))
        {
            var firstMember = (
                from member in context.Store.Query<OrganizationMember>()
                join user in context.Store.Query<AppUser>() on member.UserId equals user.Id
                join credential in context.Store.Query<LocalCredential>() on user.Id equals credential.UserId
                where member.OrganizationId == context.OrganizationId && member.IsActive && user.IsEnabled
                orderby member.JoinedAtUtc, member.Id
                select member).FirstOrDefault();
            if (firstMember != null)
            {
                firstMember.Role = MemberRole.Owner;
                context.Audit(null, "InitializeOwner", "Member", firstMember.Id, "First active local account becomes Owner.");
            }
        }
        await context.Store.CommitAsync(ct);
    }
}
