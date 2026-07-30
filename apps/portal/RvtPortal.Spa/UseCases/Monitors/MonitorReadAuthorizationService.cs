// File summary: Centralizes monitor detail read authorization for CQRS handlers and controller-adjacent flows.
// Major updates:
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-09 pending Moved monitor detail visibility checks out of controllers for the CQRS/MediatR slice.
// - 2026-06-26 pending Scoped installer monitor detail reads to the installer's assigned company.
// - 2026-07-22 pending Enforced inclusive active site-assignment windows for company-user monitor reads.

using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Data;
using RvtPortal.Spa.UseCases.Sites;

namespace RvtPortal.Spa.UseCases.Monitors;

public interface IMonitorReadAuthorizationService
{
    // Function summary: Evaluates whether a user can read a monitor detail response.
    Task<bool> CanReadAsync(MonitorListItem row, ClaimsPrincipal user, CancellationToken cancellationToken);
}

public sealed class MonitorReadAuthorizationService : IMonitorReadAuthorizationService
{
    private readonly RVTDbContext _domainContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TimeProvider _timeProvider;

    // Function summary: Initializes monitor read authorization dependencies.
    public MonitorReadAuthorizationService(
        RVTDbContext domainContext,
        UserManager<ApplicationUser> userManager,
        TimeProvider timeProvider)
    {
        _domainContext = domainContext;
        _userManager = userManager;
        _timeProvider = timeProvider;
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
            Guid? installerCompanyId = await CurrentUserCompanyIdAsync(user);
            return row.IsAssigned &&
                row.CompanyId.HasValue &&
                installerCompanyId.HasValue &&
                row.CompanyId.Value == installerCompanyId.Value;
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
        Guid? currentUserId = CurrentUserId(user);
        if (!currentUserId.HasValue)
        {
            return [];
        }

        List<Guid> siteIds = await _domainContext.SiteUsers
            .AsNoTracking()
            .Where(ActiveSiteAssignment.ForUser(currentUserId.Value, _timeProvider.GetUtcNow().UtcDateTime))
            .Select(siteUser => siteUser.SiteId)
            .ToListAsync(cancellationToken);
        return [.. siteIds];
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
        return Guid.TryParse(_userManager.GetUserId(user) ?? user.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId)
            ? userId
            : null;
    }

    private async Task<Guid?> CurrentUserCompanyIdAsync(ClaimsPrincipal user)
    {
        string? userId = _userManager.GetUserId(user) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return (await _userManager.FindByIdAsync(userId))?.CompanyId;
    }
}
