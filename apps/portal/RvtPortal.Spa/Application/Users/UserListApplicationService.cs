// File summary: Provides database-backed user list querying for the admin user API.
// Major updates:
// - 2026-07-09 pending Allowed user detail and assignment read models to reuse the shared user list model fields.
// - 2026-07-08 pending Moved admin user list filtering, sorting, paging, and row shaping out of the controller.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using RVT.BusinessLogic.Application.Paging;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Application.Users;

public interface IUserListApplicationService
{
    // Function summary: Returns a paged admin user list with company names, roles, site counts, and permission flags.
    Task<UserListResult> QueryAsync(UserListQuery request, CancellationToken cancellationToken);
}

public sealed record UserListQuery(
    Guid? CompanyId,
    PageRequest Page,
    UserListActor Actor);

public sealed record UserListActor(
    string? CurrentUserId,
    bool IsMasterAdmin,
    bool IsRvtAdmin);

public sealed class UserListResult
{
    public Guid? CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public PagedResult<UserListModel> Page { get; init; } = new();
}

public class UserListModel
{
    public string Id { get; init; } = "";
    public Guid? CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public bool IsDisabled { get; init; }
    public string? Name { get; init; }
    public string Email { get; init; } = "";
    public string? PhoneNumber { get; init; }
    public string? CompanyRole { get; init; }
    public string Role { get; init; } = "";
    public int SiteCount { get; init; }
    public bool EmailConfirmed { get; init; }
    public bool CanView { get; init; } = true;
    public bool CanEdit { get; init; }
    public bool CanDisable { get; init; }
    public bool CanEnable { get; init; }
    public bool CanDelete { get; init; }
    public bool CanSendConfirmation { get; init; }
    public bool CanSendPasswordReset { get; init; }
    public bool CanManageNotificationSettings { get; init; }
}

public sealed class UserListApplicationService : IUserListApplicationService
{
    public const string DefaultSort = "Email";

    private const string NameSortField = "Name";
    private const string CompanyNameSortField = "CompanyName";
    private const string EmailSortField = "Email";
    private const string PhoneNumberSortField = "PhoneNumber";
    private const string RoleSortField = "Role";
    private const string SiteCountSortField = "NrSites";
    private const string StatusSortField = "IsDisabled";

    public static readonly IReadOnlyDictionary<string, string> SortAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = NameSortField,
        [NameSortField] = NameSortField,
        ["companyName"] = CompanyNameSortField,
        [CompanyNameSortField] = CompanyNameSortField,
        ["email"] = EmailSortField,
        [EmailSortField] = EmailSortField,
        ["phoneNumber"] = PhoneNumberSortField,
        [PhoneNumberSortField] = PhoneNumberSortField,
        ["role"] = RoleSortField,
        [RoleSortField] = RoleSortField,
        ["siteCount"] = SiteCountSortField,
        [SiteCountSortField] = SiteCountSortField,
        ["status"] = StatusSortField,
        [StatusSortField] = StatusSortField
    };

    public static readonly IReadOnlySet<string> SortFields = new HashSet<string>(SortAliases.Keys, StringComparer.OrdinalIgnoreCase);

    private readonly ApplicationDbContext applicationContext;
    private readonly RVTDbContext domainContext;

    // Function summary: Initializes this application service with Identity and domain read contexts.
    public UserListApplicationService(ApplicationDbContext applicationContext, RVTDbContext domainContext)
    {
        this.applicationContext = applicationContext;
        this.domainContext = domainContext;
    }

    // Function summary: Returns a paged admin user list with filters applied before materialization where provider boundaries allow.
    public async Task<UserListResult> QueryAsync(UserListQuery request, CancellationToken cancellationToken)
    {
        var companies = await LoadCompaniesAsync(cancellationToken);
        var query = BuildUserRoleQuery(request.CompanyId);
        query = ApplySearch(query, request.Page.SearchText, companies);
        var total = await query.CountAsync(cancellationToken);
        var canonicalSort = CanonicalSort(request.Page.Sort);
        List<UserRoleProjection> pageRows;
        Dictionary<Guid, int> siteCounts;

        if (RequiresProjectedSort(canonicalSort))
        {
            var rows = await query.ToListAsync(cancellationToken);
            siteCounts = await LoadSiteCountsAsync(rows.Select(row => row.Id), cancellationToken);
            var sorted = SortProjectedRows(rows.Select(row => BuildModel(row, companies, siteCounts, request.Actor)), canonicalSort, request.Page.SortDir);
            pageRows = [];
            var pageItems = sorted
                .Skip((request.Page.Page - 1) * request.Page.PageSize)
                .Take(request.Page.PageSize)
                .ToList();
            return BuildResult(request, companies, total, pageItems);
        }

        pageRows = await ApplyDatabaseSort(query, canonicalSort, request.Page.SortDir)
            .Skip((request.Page.Page - 1) * request.Page.PageSize)
            .Take(request.Page.PageSize)
            .ToListAsync(cancellationToken);
        siteCounts = await LoadSiteCountsAsync(pageRows.Select(row => row.Id), cancellationToken);
        return BuildResult(
            request,
            companies,
            total,
            pageRows.Select(row => BuildModel(row, companies, siteCounts, request.Actor)).ToList());
    }

    // Function summary: Builds the Identity user plus role query used for admin list filtering.
    private IQueryable<UserRoleProjection> BuildUserRoleQuery(Guid? companyId)
    {
        var query =
            from user in applicationContext.Users.AsNoTracking()
            join userRole in applicationContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId into userRoles
            from userRole in userRoles.DefaultIfEmpty()
            join role in applicationContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id into roles
            from role in roles.DefaultIfEmpty()
            select new UserRoleProjection
            {
                Id = user.Id,
                CompanyId = user.CompanyId,
                IsDisabled = user.IsDisabled,
                Name = user.Name,
                Email = user.Email ?? "",
                PhoneNumber = user.PhoneNumber,
                CompanyRole = user.CompanyRole,
                Role = role == null ? "" : role.Name ?? "",
                EmailConfirmed = user.EmailConfirmed
            };

        return companyId.HasValue
            ? query.Where(user => user.CompanyId == companyId.Value)
            : query;
    }

    // Function summary: Applies search to Identity fields and company-name matches before materializing users.
    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF query predicate; ToLower() is the only case-insensitive form that translates on Npgsql and runs on the InMemory test provider. See docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF query predicate; see docs/development/portal/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF query predicate; StringComparison does not translate on Npgsql. See docs/development/portal/sonar/globalization-suppressions.md")]
    private static IQueryable<UserRoleProjection> ApplySearch(
        IQueryable<UserRoleProjection> query,
        string? searchText,
        IReadOnlyDictionary<Guid, string> companies)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return query;
        }

        var search = searchText.Trim().ToLower();
        var matchingCompanyIds = companies
            .Where(company => company.Value.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Select(company => company.Key)
            .ToList();
        return query.Where(user =>
            (user.Name != null && user.Name.ToLower().Contains(search)) ||
            user.Email.ToLower().Contains(search) ||
            user.Role.ToLower().Contains(search) ||
            (user.CompanyId.HasValue && matchingCompanyIds.Contains(user.CompanyId.Value)));
    }

    // Function summary: Applies provider-side ordering for fields available in the Identity query.
    private static IQueryable<UserRoleProjection> ApplyDatabaseSort(IQueryable<UserRoleProjection> users, string sort, string sortDir)
    {
        var descending = sortDir == PageSortDirections.Descending;
        return sort switch
        {
            NameSortField => descending ? users.OrderByDescending(user => user.Name) : users.OrderBy(user => user.Name),
            PhoneNumberSortField => descending ? users.OrderByDescending(user => user.PhoneNumber) : users.OrderBy(user => user.PhoneNumber),
            RoleSortField => descending ? users.OrderByDescending(user => user.Role) : users.OrderBy(user => user.Role),
            StatusSortField => descending ? users.OrderByDescending(user => user.IsDisabled) : users.OrderBy(user => user.IsDisabled),
            _ => descending ? users.OrderByDescending(user => user.Email) : users.OrderBy(user => user.Email)
        };
    }

    // Function summary: Applies ordering for fields assembled from domain-context values.
    private static IEnumerable<UserListModel> SortProjectedRows(IEnumerable<UserListModel> users, string sort, string sortDir)
    {
        var descending = sortDir == PageSortDirections.Descending;
        return sort switch
        {
            CompanyNameSortField => descending ? users.OrderByDescending(user => user.CompanyName) : users.OrderBy(user => user.CompanyName),
            SiteCountSortField => descending ? users.OrderByDescending(user => user.SiteCount) : users.OrderBy(user => user.SiteCount),
            _ => users
        };
    }

    // Function summary: Converts one Identity projection into the user list model used by API mappers.
    private static UserListModel BuildModel(
        UserRoleProjection row,
        IReadOnlyDictionary<Guid, string> companies,
        IReadOnlyDictionary<Guid, int> siteCounts,
        UserListActor actor)
    {
        var parsedId = Guid.TryParse(row.Id, out var userId) ? userId : Guid.Empty;
        var companyName = row.CompanyId.HasValue && companies.TryGetValue(row.CompanyId.Value, out var name) ? name : null;
        return new UserListModel
        {
            Id = row.Id,
            CompanyId = row.CompanyId,
            CompanyName = companyName,
            IsDisabled = row.IsDisabled,
            Name = row.Name,
            Email = row.Email,
            PhoneNumber = row.PhoneNumber,
            CompanyRole = row.CompanyRole,
            Role = row.Role,
            SiteCount = parsedId == Guid.Empty || !siteCounts.TryGetValue(parsedId, out var count) ? 0 : count,
            EmailConfirmed = row.EmailConfirmed,
            CanEdit = CanEditUser(row.Role, actor),
            CanDisable = !row.IsDisabled && CanEditUser(row.Role, actor),
            CanEnable = row.IsDisabled && CanEditUser(row.Role, actor),
            CanDelete = CanDeleteUser(row.Role, actor) && !string.Equals(row.Id, actor.CurrentUserId, StringComparison.Ordinal),
            CanSendConfirmation = !row.EmailConfirmed,
            CanSendPasswordReset = row.EmailConfirmed,
            CanManageNotificationSettings = row.Role == RoleNames.CompanyUser
        };
    }

    // Function summary: Builds the final paged result and scoped company metadata.
    private static UserListResult BuildResult(
        UserListQuery request,
        IReadOnlyDictionary<Guid, string> companies,
        int total,
        IReadOnlyList<UserListModel> results)
    {
        return new UserListResult
        {
            CompanyId = request.CompanyId,
            CompanyName = request.CompanyId.HasValue && companies.TryGetValue(request.CompanyId.Value, out var name) ? name : null,
            Page = new PagedResult<UserListModel>
            {
                Results = results,
                Total = total,
                Page = request.Page.Page,
                PageSize = request.Page.PageSize,
                SearchText = request.Page.SearchText ?? "",
                Sort = request.Page.Sort,
                SortDir = request.Page.SortDir
            }
        };
    }

    // Function summary: Loads company names once for user list display, search, and projected sorting.
    private async Task<Dictionary<Guid, string>> LoadCompaniesAsync(CancellationToken cancellationToken)
    {
        return await domainContext.Companies
            .AsNoTracking()
            .ToDictionaryAsync(company => company.Id, company => company.CompanyName, cancellationToken);
    }

    // Function summary: Counts site assignments for the requested user ids.
    private async Task<Dictionary<Guid, int>> LoadSiteCountsAsync(IEnumerable<string> userIds, CancellationToken cancellationToken)
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

    // Function summary: Resolves accepted API sort aliases to canonical sort fields.
    private static string CanonicalSort(string sort)
    {
        return SortAliases.TryGetValue(sort, out var canonicalSort) ? canonicalSort : EmailSortField;
    }

    // Function summary: Identifies sorts that depend on values assembled outside the Identity query.
    private static bool RequiresProjectedSort(string canonicalSort)
    {
        return canonicalSort is CompanyNameSortField or SiteCountSortField;
    }

    // Function summary: Evaluates whether the current actor can edit a user with the supplied role.
    private static bool CanEditUser(string role, UserListActor actor)
    {
        return actor.IsMasterAdmin ||
            (actor.IsRvtAdmin && role is not RoleNames.RVTAdmin and not RoleNames.RVTMasterAdmin);
    }

    // Function summary: Evaluates whether the current actor can delete a user with the supplied role.
    private static bool CanDeleteUser(string role, UserListActor actor)
    {
        return actor.IsMasterAdmin ||
            (actor.IsRvtAdmin && role == RoleNames.CompanyUser);
    }

    private sealed class UserRoleProjection
    {
        public string Id { get; init; } = "";
        public Guid? CompanyId { get; init; }
        public bool IsDisabled { get; init; }
        public string? Name { get; init; }
        public string Email { get; init; } = "";
        public string? PhoneNumber { get; init; }
        public string? CompanyRole { get; init; }
        public string Role { get; init; } = "";
        public bool EmailConfirmed { get; init; }
    }
}
