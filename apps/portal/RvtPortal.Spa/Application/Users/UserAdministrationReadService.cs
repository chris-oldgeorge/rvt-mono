// File summary: Provides user detail, option, and site-assignment read models for the admin user API.
// Major updates:
// - 2026-07-09 pending Moved user detail, role/company options, and site-assignment response shaping out of the controller.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Application.Users;

public interface IUserAdministrationReadService
{
    // Function summary: Returns role and company options available to the current admin actor.
    Task<UserAdministrationOptionsModel> OptionsAsync(UserListActor actor, CancellationToken cancellationToken);

    // Function summary: Returns one user detail model, or null when the user is missing.
    Task<UserDetailModel?> GetDetailAsync(string id, UserListActor actor, CancellationToken cancellationToken);

    // Function summary: Returns site-assignment users for one site, or null when the site is missing.
    Task<SiteAssignmentModel?> GetSiteAssignmentsAsync(Guid siteId, UserListActor actor, CancellationToken cancellationToken);
}

public sealed class UserAdministrationOptionsModel
{
    public List<UserOptionModel> AvailableRoles { get; init; } = [];
    public List<UserOptionModel> Companies { get; init; } = [];
}

public sealed class UserOptionModel
{
    public string Value { get; init; } = "";
    public string Label { get; init; } = "";
}

public sealed class UserDetailModel : UserListModel
{
    public List<UserOptionModel> AvailableRoles { get; init; } = [];
    public List<UserOptionModel> Companies { get; init; } = [];
}

public sealed class SiteAssignmentModel
{
    public Guid SiteId { get; init; }
    public string SiteName { get; init; } = "";
    public Guid? CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public List<UserListModel> AvailableUsers { get; init; } = [];
    public List<SiteUserAssignmentModel> AssignedUsers { get; init; } = [];
}

public sealed class SiteUserAssignmentModel : UserListModel
{
    public bool SiteContact { get; init; }
}

public sealed class UserAdministrationReadService : IUserAdministrationReadService
{
    private static readonly string[] RoleOrder =
    [
        RoleNames.RVTMasterAdmin,
        RoleNames.RVTAdmin,
        RoleNames.RVTInstaller,
        RoleNames.CompanyUser
    ];

    private readonly ApplicationDbContext applicationContext;
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes this read service with Identity and domain contexts.
    public UserAdministrationReadService(ApplicationDbContext applicationContext, RVTDbContext domainContext)
    {
        this.applicationContext = applicationContext;
        this.domainContext = domainContext;
    }

    // Function summary: Returns role and company options visible to the current admin actor.
    public async Task<UserAdministrationOptionsModel> OptionsAsync(
        UserListActor actor,
        CancellationToken cancellationToken)
    {
        return new UserAdministrationOptionsModel
        {
            AvailableRoles = await BuildRoleOptionsAsync(actor, cancellationToken),
            Companies = await BuildCompanyOptionsAsync(cancellationToken)
        };
    }

    // Function summary: Returns a full user detail model with options for the edit form.
    public async Task<UserDetailModel?> GetDetailAsync(
        string id,
        UserListActor actor,
        CancellationToken cancellationToken)
    {
        var user = await applicationContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var listItem = (await BuildUserModelsAsync([user], actor, cancellationToken)).Single();
        var options = await OptionsAsync(actor, cancellationToken);
        return new UserDetailModel
        {
            Id = listItem.Id,
            CompanyId = listItem.CompanyId,
            CompanyName = listItem.CompanyName,
            IsDisabled = listItem.IsDisabled,
            Name = listItem.Name,
            Email = listItem.Email,
            PhoneNumber = listItem.PhoneNumber,
            CompanyRole = listItem.CompanyRole,
            Role = listItem.Role,
            SiteCount = listItem.SiteCount,
            EmailConfirmed = listItem.EmailConfirmed,
            CanView = listItem.CanView,
            CanEdit = listItem.CanEdit,
            CanDisable = listItem.CanDisable,
            CanEnable = listItem.CanEnable,
            CanDelete = listItem.CanDelete,
            CanSendConfirmation = listItem.CanSendConfirmation,
            CanSendPasswordReset = listItem.CanSendPasswordReset,
            CanManageNotificationSettings = listItem.CanManageNotificationSettings,
            AvailableRoles = options.AvailableRoles,
            Companies = options.Companies
        };
    }

    // Function summary: Returns available and assigned company users for one site.
    public async Task<SiteAssignmentModel?> GetSiteAssignmentsAsync(
        Guid siteId,
        UserListActor actor,
        CancellationToken cancellationToken)
    {
        var site = await domainContext.Sites
            .AsNoTracking()
            .Include(item => item.Contracts)
            .SingleOrDefaultAsync(item => item.Id == siteId, cancellationToken);
        if (site is null)
        {
            return null;
        }

        var companyId = site.Contracts?.Select(contract => contract.CompanyId).FirstOrDefault();
        var companies = await LoadCompaniesAsync(cancellationToken);
        var assigned = await domainContext.SiteUsers
            .AsNoTracking()
            .Where(siteUser => siteUser.SiteId == siteId)
            .ToListAsync(cancellationToken);
        var assignedUserIds = assigned.Select(item => item.UserId.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = applicationContext.Users.AsNoTracking();
        if (companyId.HasValue)
        {
            candidates = candidates.Where(user => user.CompanyId == companyId.Value);
        }

        var candidateItems = await BuildUserModelsAsync(await candidates.ToListAsync(cancellationToken), actor, cancellationToken);

        return new SiteAssignmentModel
        {
            SiteId = site.Id,
            SiteName = site.SiteName,
            CompanyId = companyId == Guid.Empty ? null : companyId,
            CompanyName = companyId.HasValue && companies.TryGetValue(companyId.Value, out var companyName) ? companyName : null,
            AvailableUsers = candidateItems.Where(user => !assignedUserIds.Contains(user.Id)).ToList(),
            AssignedUsers = candidateItems
                .Where(user => assignedUserIds.Contains(user.Id))
                .Select(user =>
                {
                    var assignment = assigned.Single(item => item.UserId.ToString().Equals(user.Id, StringComparison.OrdinalIgnoreCase));
                    return new SiteUserAssignmentModel
                    {
                        Id = user.Id,
                        CompanyId = user.CompanyId,
                        CompanyName = user.CompanyName,
                        IsDisabled = user.IsDisabled,
                        Name = user.Name,
                        Email = user.Email,
                        PhoneNumber = user.PhoneNumber,
                        CompanyRole = user.CompanyRole,
                        Role = user.Role,
                        SiteCount = user.SiteCount,
                        EmailConfirmed = user.EmailConfirmed,
                        CanView = user.CanView,
                        CanEdit = user.CanEdit,
                        CanDisable = user.CanDisable,
                        CanEnable = user.CanEnable,
                        CanDelete = user.CanDelete,
                        CanSendConfirmation = user.CanSendConfirmation,
                        CanSendPasswordReset = user.CanSendPasswordReset,
                        CanManageNotificationSettings = user.CanManageNotificationSettings,
                        SiteContact = assignment.SiteContact
                    };
                })
                .ToList()
        };
    }

    // Function summary: Builds list-style user models for detail and site-assignment views.
    private async Task<List<UserListModel>> BuildUserModelsAsync(
        IReadOnlyCollection<ApplicationUser> users,
        UserListActor actor,
        CancellationToken cancellationToken)
    {
        var companies = await LoadCompaniesAsync(cancellationToken);
        var roleByUser = await LoadRolesAsync(users.Select(user => user.Id), cancellationToken);
        var siteCounts = await LoadSiteCountsAsync(users.Select(user => user.Id), cancellationToken);

        return users.Select(user => BuildUserModel(user, roleByUser, companies, siteCounts, actor)).ToList();
    }

    // Function summary: Converts one Identity user into the shared admin user model.
    private static UserListModel BuildUserModel(
        ApplicationUser user,
        IReadOnlyDictionary<string, string> roleByUser,
        IReadOnlyDictionary<Guid, string> companies,
        IReadOnlyDictionary<Guid, int> siteCounts,
        UserListActor actor)
    {
        var role = roleByUser.TryGetValue(user.Id, out var resolvedRole) ? resolvedRole : "";
        var parsedId = Guid.TryParse(user.Id, out var userId) ? userId : Guid.Empty;
        var companyName = user.CompanyId.HasValue && companies.TryGetValue(user.CompanyId.Value, out var name) ? name : null;
        return new UserListModel
        {
            Id = user.Id,
            CompanyId = user.CompanyId,
            CompanyName = companyName,
            IsDisabled = user.IsDisabled,
            Name = user.Name,
            Email = user.Email ?? "",
            PhoneNumber = user.PhoneNumber,
            CompanyRole = user.CompanyRole,
            Role = role,
            SiteCount = parsedId == Guid.Empty || !siteCounts.TryGetValue(parsedId, out var count) ? 0 : count,
            EmailConfirmed = user.EmailConfirmed,
            CanEdit = CanEditUser(role, actor),
            CanDisable = !user.IsDisabled && CanEditUser(role, actor),
            CanEnable = user.IsDisabled && CanEditUser(role, actor),
            CanDelete = CanDeleteUser(role, actor) && !string.Equals(user.Id, actor.CurrentUserId, StringComparison.Ordinal),
            CanSendConfirmation = !user.EmailConfirmed,
            CanSendPasswordReset = user.EmailConfirmed,
            CanManageNotificationSettings = role == RoleNames.CompanyUser
        };
    }

    // Function summary: Builds available role options for the actor.
    private async Task<List<UserOptionModel>> BuildRoleOptionsAsync(
        UserListActor actor,
        CancellationToken cancellationToken)
    {
        var configuredRoles = await applicationContext.Roles
            .AsNoTracking()
            .Select(role => role.Name)
            .ToListAsync(cancellationToken);
        return RoleOrder
            .Where(role => configuredRoles.Contains(role))
            .Where(role => CanAssignRole(role, actor))
            .Select(role => new UserOptionModel { Value = role, Label = role })
            .ToList();
    }

    // Function summary: Builds company options for user edit forms.
    private async Task<List<UserOptionModel>> BuildCompanyOptionsAsync(CancellationToken cancellationToken)
    {
        return await domainContext.Companies
            .AsNoTracking()
            .OrderBy(company => company.CompanyName)
            .Select(company => new UserOptionModel { Value = company.Id.ToString(), Label = company.CompanyName })
            .ToListAsync(cancellationToken);
    }

    // Function summary: Loads company names keyed by company id.
    private async Task<Dictionary<Guid, string>> LoadCompaniesAsync(CancellationToken cancellationToken)
    {
        return await domainContext.Companies
            .AsNoTracking()
            .ToDictionaryAsync(company => company.Id, company => company.CompanyName, cancellationToken);
    }

    // Function summary: Loads the first configured role for each requested user id.
    private async Task<Dictionary<string, string>> LoadRolesAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var roles = await (
            from userRole in applicationContext.UserRoles.AsNoTracking()
            join role in applicationContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where ids.Contains(userRole.UserId)
            select new { userRole.UserId, Role = role.Name ?? "" })
            .ToListAsync(cancellationToken);

        return roles
            .GroupBy(role => role.UserId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Role).FirstOrDefault() ?? "");
    }

    // Function summary: Counts site assignments for the requested user ids.
    private async Task<Dictionary<Guid, int>> LoadSiteCountsAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken)
    {
        var parsedIds = userIds
            .Select(id => Guid.TryParse(id, out var parsedId) ? parsedId : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
        return parsedIds.Count == 0
            ? []
            : await domainContext.SiteUsers
                .AsNoTracking()
                .Where(siteUser => parsedIds.Contains(siteUser.UserId))
                .GroupBy(siteUser => siteUser.UserId)
                .Select(group => new { UserId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(group => group.UserId, group => group.Count, cancellationToken);
    }

    // Function summary: Evaluates whether the current actor can assign a role.
    private static bool CanAssignRole(string role, UserListActor actor)
    {
        return actor.IsMasterAdmin ||
            role is RoleNames.CompanyUser or RoleNames.RVTInstaller;
    }

    // Function summary: Evaluates whether the current actor can edit a user role.
    private static bool CanEditUser(string role, UserListActor actor)
    {
        return actor.IsMasterAdmin ||
            (actor.IsRvtAdmin && role is not RoleNames.RVTAdmin and not RoleNames.RVTMasterAdmin);
    }

    // Function summary: Evaluates whether the current actor can delete a user role.
    private static bool CanDeleteUser(string role, UserListActor actor)
    {
        return actor.IsMasterAdmin ||
            (actor.IsRvtAdmin && role == RoleNames.CompanyUser);
    }
}
