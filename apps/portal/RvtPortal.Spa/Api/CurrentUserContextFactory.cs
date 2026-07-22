// File summary: Builds business-layer portal user context from ASP.NET Core identity state.
// Major updates:
// - 2026-07-05 pending Added current-user adapter for controller-to-business refactoring.

using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using RVT.BusinessLogic.Application;
using RVT.Entities;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Api;

public interface ICurrentUserContextFactory
{
    // Function summary: Creates a transport-neutral current-user context for business-layer workflows.
    Task<PortalUserContext> CreateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}

public sealed class CurrentUserContextFactory : ICurrentUserContextFactory
{
    private readonly UserManager<ApplicationUser> userManager;

    // Function summary: Initializes this type with ASP.NET Identity dependencies.
    public CurrentUserContextFactory(UserManager<ApplicationUser> userManager)
    {
        this.userManager = userManager;
    }

    public async Task<PortalUserContext> CreateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (principal.Identity?.IsAuthenticated != true)
        {
            return new PortalUserContext(null, null, null, false, false, false);
        }

        var user = await userManager.GetUserAsync(principal);
        var userId = Guid.TryParse(user?.Id, out var parsedUserId) ? parsedUserId : (Guid?)null;
        IReadOnlyCollection<string> roles = user == null
            ? []
            : (await userManager.GetRolesAsync(user)).ToArray();
        var isAdmin = roles.Contains(RoleNames.RVTMasterAdmin, StringComparer.Ordinal) ||
            roles.Contains(RoleNames.RVTAdmin, StringComparer.Ordinal);
        var isInstaller = roles.Contains(RoleNames.RVTInstaller, StringComparer.Ordinal);
        var isCompanyUser = roles.Contains(RoleNames.CompanyUser, StringComparer.Ordinal);

        return new PortalUserContext(
            userId,
            user?.UserName ?? principal.Identity?.Name,
            user?.CompanyId,
            isAdmin,
            isInstaller,
            isCompanyUser);
    }
}
