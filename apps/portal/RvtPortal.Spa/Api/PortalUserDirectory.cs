// File summary: Adapts ASP.NET Identity users into business-layer portal user profiles.
// Major updates:
// - 2026-07-05 pending Added Identity-backed user directory for report-recipient business workflows.

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RVT.BusinessLogic.Application.Users;
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
        var users = await userManager.Users.AsNoTracking().ToListAsync(cancellationToken);
        var profiles = new List<PortalUserProfile>();
        foreach (var user in users)
        {
            var profile = await BuildProfileAsync(user);
            if (profile != null)
            {
                profiles.Add(profile);
            }
        }

        return profiles;
    }

    public async Task<PortalUserProfile?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user == null ? null : await BuildProfileAsync(user);
    }

    // Function summary: Converts one Identity user into the business-layer user profile shape.
    private async Task<PortalUserProfile?> BuildProfileAsync(ApplicationUser user)
    {
        if (!Guid.TryParse(user.Id, out var parsedId))
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
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
