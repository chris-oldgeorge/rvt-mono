// File summary: Adapts ASP.NET Identity users into business-layer portal user profiles.
// Major updates:
// - 2026-07-05 pending Added Identity-backed user directory for report-recipient business workflows.

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RvtPortal.Application.Identity;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Api;

public sealed class PortalUserDirectory : IPortalUserDirectory
{
    private readonly UserManager<ApplicationUser> userManager;

    // Function summary: Initializes this adapter with the ASP.NET Identity user manager.
    public PortalUserDirectory(UserManager<ApplicationUser> userManager)
    {
        this.userManager = userManager;
    }

    public async Task<IReadOnlyList<PortalUserProfile>> ListUsersAsync(CancellationToken cancellationToken)
    {
        List<ApplicationUser> users = await userManager.Users.AsNoTracking().ToListAsync(cancellationToken);
        List<PortalUserProfile> profiles = new List<PortalUserProfile>();
        foreach (ApplicationUser? user in users)
        {
            PortalUserProfile? profile = await BuildProfileAsync(user);
            if (profile != null)
            {
                profiles.Add(profile);
            }
        }

        return profiles;
    }

    public async Task<PortalUserProfile?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(userId.ToString());
        return user == null ? null : await BuildProfileAsync(user);
    }

    // Function summary: Converts one Identity user into the business-layer user profile shape.
    private async Task<PortalUserProfile?> BuildProfileAsync(ApplicationUser user)
    {
        if (!Guid.TryParse(user.Id, out Guid parsedId))
        {
            return null;
        }

        IList<string> roles = await userManager.GetRolesAsync(user);
        return new PortalUserProfile(
            parsedId,
            user.Id,
            user.CompanyId,
            user.IsDisabled,
            user.Name,
            user.Email ?? "",
            user.PhoneNumber,
            user.CompanyRole,
            user.EmailConfirmed,
            roles.ToList());
    }
}
