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
    private static readonly string[] _roleOrder =
    [
        RoleNames.RVTMasterAdmin,
        RoleNames.RVTAdmin,
        RoleNames.RVTInstaller,
        RoleNames.CompanyUser
    ];

    private readonly ApplicationDbContext _applicationContext;
    private readonly RVTDbContext _domainContext;

    // Function summary: Initializes this read service with Identity and domain contexts.
    public UserAdministrationReadService(ApplicationDbContext applicationContext, RVTDbContext domainContext)
    {
        _applicationContext = applicationContext;
        _domainContext = domainContext;
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
        ApplicationUser? user = await _applicationContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        UserListModel listItem = (await BuildUserModelsAsync([user], actor, cancellationToken)).Single();
        UserAdministrationOptionsModel options = await OptionsAsync(actor, cancellationToken);
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
        Site? site = await _domainContext.Sites
            .AsNoTracking()
            .Include(item => item.Contracts)
            .SingleOrDefaultAsync(item => item.Id == siteId, cancellationToken);
        if (site is null)
        {
            return null;
        }

        Guid? companyId = site.Contracts?.Select(contract => contract.CompanyId).FirstOrDefault();
        Dictionary<Guid, string> companies = await LoadCompaniesAsync(cancellationToken);
        List<SiteUsers> assigned = await _domainContext.SiteUsers
            .AsNoTracking()
            .Where(siteUser => siteUser.SiteId == siteId)
            .ToListAsync(cancellationToken);
        HashSet<string> assignedUserIds = assigned.Select(item => item.UserId.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        IQueryable<ApplicationUser> candidates = _applicationContext.Users.AsNoTracking();
        if (companyId.HasValue)
        {
            candidates = candidates.Where(user => user.CompanyId == companyId.Value);
        }

        List<UserListModel> candidateItems = await BuildUserModelsAsync(await candidates.ToListAsync(cancellationToken), actor, cancellationToken);

        return new SiteAssignmentModel
        {
            SiteId = site.Id,
            SiteName = site.SiteName,
            CompanyId = companyId == Guid.Empty ? null : companyId,
            CompanyName = companyId.HasValue && companies.TryGetValue(companyId.Value, out string? companyName) ? companyName : null,
            AvailableUsers = [.. candidateItems.Where(user => !assignedUserIds.Contains(user.Id))],
            AssignedUsers = [.. candidateItems
                .Where(user => assignedUserIds.Contains(user.Id))
                .Select(user =>
                {
                    SiteUsers assignment = assigned.Single(item => item.UserId.ToString().Equals(user.Id, StringComparison.OrdinalIgnoreCase));
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
                })]
        };
    }

    // Function summary: Builds list-style user models for detail and site-assignment views.
    private async Task<List<UserListModel>> BuildUserModelsAsync(
        IReadOnlyCollection<ApplicationUser> users,
        UserListActor actor,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, string> companies = await LoadCompaniesAsync(cancellationToken);
        Dictionary<string, string> roleByUser = await LoadRolesAsync(users.Select(user => user.Id), cancellationToken);
        Dictionary<Guid, int> siteCounts = await LoadSiteCountsAsync(users.Select(user => user.Id), cancellationToken);

        return [.. users.Select(user => BuildUserModel(user, roleByUser, companies, siteCounts, actor))];
    }

    // Function summary: Converts one Identity user into the shared admin user model.
    private static UserListModel BuildUserModel(
        ApplicationUser user,
        IReadOnlyDictionary<string, string> roleByUser,
        IReadOnlyDictionary<Guid, string> companies,
        IReadOnlyDictionary<Guid, int> siteCounts,
        UserListActor actor)
    {
        string role = roleByUser.TryGetValue(user.Id, out string? resolvedRole) ? resolvedRole : "";
        Guid parsedId = Guid.TryParse(user.Id, out Guid userId) ? userId : Guid.Empty;
        string? companyName = user.CompanyId.HasValue && companies.TryGetValue(user.CompanyId.Value, out string? name) ? name : null;
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
            SiteCount = parsedId == Guid.Empty || !siteCounts.TryGetValue(parsedId, out int count) ? 0 : count,
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
        List<string?> configuredRoles = await _applicationContext.Roles
            .AsNoTracking()
            .Select(role => role.Name)
            .ToListAsync(cancellationToken);
        return [.. _roleOrder
            .Where(role => configuredRoles.Contains(role))
            .Where(role => CanAssignRole(role, actor))
            .Select(role => new UserOptionModel { Value = role, Label = role })];
    }

    // Function summary: Builds company options for user edit forms.
    private async Task<List<UserOptionModel>> BuildCompanyOptionsAsync(CancellationToken cancellationToken)
    {
        return await _domainContext.Companies
            .AsNoTracking()
            .OrderBy(company => company.CompanyName)
            .Select(company => new UserOptionModel { Value = company.Id.ToString(), Label = company.CompanyName })
            .ToListAsync(cancellationToken);
    }

    // Function summary: Loads company names keyed by company id.
    private async Task<Dictionary<Guid, string>> LoadCompaniesAsync(CancellationToken cancellationToken)
    {
        return await _domainContext.Companies
            .AsNoTracking()
            .ToDictionaryAsync(company => company.Id, company => company.CompanyName, cancellationToken);
    }

    // Function summary: Loads the first configured role for each requested user id.
    private async Task<Dictionary<string, string>> LoadRolesAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken)
    {
        List<string> ids = [.. userIds];
        if (ids.Count == 0)
        {
            return [];
        }

        var roles = await (
            from userRole in _applicationContext.UserRoles.AsNoTracking()
            join role in _applicationContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
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
        List<Guid> parsedIds = [.. userIds
            .Select(id => Guid.TryParse(id, out Guid parsedId) ? parsedId : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)];
        return parsedIds.Count == 0
            ? []
            : await _domainContext.SiteUsers
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
