// File summary: Centralizes monitor detail read authorization for CQRS handlers and controller-adjacent flows.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-09 pending Moved monitor detail visibility checks out of controllers for the CQRS/MediatR slice.
// - 2026-06-26 pending Scoped installer monitor detail reads to the installer's assigned company.

using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Application.Monitors;

public interface IMonitorReadAuthorizationService
{
    // Function summary: Evaluates whether a user can read a monitor detail response.
    Task<bool> CanReadAsync(MonitorListItem row, ClaimsPrincipal user, CancellationToken cancellationToken);
}

public sealed class MonitorReadAuthorizationService : IMonitorReadAuthorizationService
{
    private readonly RVTDbContext domainContext;
    private readonly UserManager<ApplicationUser> userManager;

    // Function summary: Initializes monitor read authorization dependencies.
    public MonitorReadAuthorizationService(RVTDbContext domainContext, UserManager<ApplicationUser> userManager)
    {
        this.domainContext = domainContext;
        this.userManager = userManager;
    }

    // Function summary: Evaluates whether a user can read a monitor detail response.
    public async Task<bool> CanReadAsync(MonitorListItem row, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (IsAdmin(user))
        {
            return true;
        }

        if (user.IsInRole(RoleNames.RVTInstaller))
        {
            var installerCompanyId = await CurrentUserCompanyIdAsync(user);
            return row.IsAssigned && row.CompanyId.HasValue && installerCompanyId == row.CompanyId.Value;
        }

        if (!IsCompanyUser(user) || !row.SiteId.HasValue)
        {
            return false;
        }

        return (await VisibleSiteIdsAsync(user, cancellationToken)).Contains(row.SiteId.Value);
    }

    // Function summary: Finds site ids visible to the current company user.
    private async Task<HashSet<Guid>> VisibleSiteIdsAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var currentUserId = CurrentUserId(user);
        if (!currentUserId.HasValue)
        {
            return [];
        }

        var siteIds = await domainContext.SiteUsers
            .AsNoTracking()
            .Where(siteUser => siteUser.UserId == currentUserId.Value && siteUser.EndDate == null)
            .Select(siteUser => siteUser.SiteId)
            .ToListAsync(cancellationToken);
        return siteIds.ToHashSet();
    }

    // Function summary: Evaluates whether the current user has RVT administrator privileges.
    private static bool IsAdmin(ClaimsPrincipal user)
    {
        return user.IsInRole(RoleNames.RVTMasterAdmin) || user.IsInRole(RoleNames.RVTAdmin);
    }

    // Function summary: Evaluates whether the current user is a non-admin company user.
    private static bool IsCompanyUser(ClaimsPrincipal user)
    {
        return user.IsInRole(RoleNames.CompanyUser) && !IsAdmin(user);
    }

    // Function summary: Resolves the authenticated user id from Identity claims.
    private Guid? CurrentUserId(ClaimsPrincipal user)
    {
        return Guid.TryParse(userManager.GetUserId(user) ?? user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : null;
    }

    private async Task<Guid?> CurrentUserCompanyIdAsync(ClaimsPrincipal user)
    {
        var userId = userManager.GetUserId(user) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return (await userManager.FindByIdAsync(userId))?.CompanyId;
    }
}
