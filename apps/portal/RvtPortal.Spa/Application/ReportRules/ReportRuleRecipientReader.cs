// File summary: Builds report-rule recipient assignment read models outside the API controller.
// Major updates:
// - 2026-07-09 pending Moved paged recipient queries onto bounded candidate sets with SQL-side count/search/page.
// - 2026-06-26 pending Extracted report-rule recipient assignment shaping from ReportRulesController.

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Data;
using ReportRule = RVT.DataAccess.EntityModels.Models.ReportRule;

namespace RvtPortal.Spa.Application.ReportRules;

public interface IReportRuleRecipientReader
{
    Task<ReportUserAssignmentResponse?> BuildAssignmentResponseAsync(Guid reportRuleId, CancellationToken cancellationToken);

    Task<QueryReportUsersResponse?> QueryAssignmentUsersAsync(
        Guid reportRuleId,
        QueryReportUsersRequest request,
        bool assigned,
        CancellationToken cancellationToken);
}

public sealed class ReportRuleRecipientReader : IReportRuleRecipientReader
{
    private static readonly string[] AdminRoleNames = [RoleNames.RVTMasterAdmin, RoleNames.RVTAdmin];

    private readonly RVTSearchContext searchContext;
    private readonly RVTDbContext domainContext;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly ApplicationDbContext applicationContext;

    // Function summary: Initializes the report-rule recipient reader.
    public ReportRuleRecipientReader(
        RVTSearchContext searchContext,
        RVTDbContext domainContext,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext applicationContext)
    {
        this.searchContext = searchContext;
        this.domainContext = domainContext;
        this.userManager = userManager;
        this.applicationContext = applicationContext;
    }

    // Function summary: Loads each user's roles in one join instead of a UserManager round trip per user.
    private async Task<Dictionary<string, List<string>>> LoadRolesByUserAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var pairs = await applicationContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userIds.Contains(userRole.UserId))
            .Join(
                applicationContext.Roles.AsNoTracking(),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, RoleName = role.Name })
            .ToListAsync(cancellationToken);

        return pairs
            .GroupBy(pair => pair.UserId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(pair => pair.RoleName ?? string.Empty).ToList(),
                StringComparer.Ordinal);
    }

    // Function summary: Builds the report-rule assignment summary used by assignment screens.
    public async Task<ReportUserAssignmentResponse?> BuildAssignmentResponseAsync(Guid reportRuleId, CancellationToken cancellationToken)
    {
        var rule = await searchContext.ReportRules
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == reportRuleId && !item.Deleted, cancellationToken);
        if (rule == null)
        {
            return null;
        }

        var site = await domainContext.Sites
            .AsNoTracking()
            .Include(item => item.Contracts)
            .ThenInclude(contract => contract.Company)
            .SingleOrDefaultAsync(item => item.Id == rule.SiteId, cancellationToken);
        if (site == null)
        {
            return null;
        }

        var company = site.Contracts?
            .Select(contract => contract.Company)
            .FirstOrDefault(item => item != null);
        var assignments = await searchContext.ReportUsers
            .AsNoTracking()
            .Where(item => item.ReportRuleId == reportRuleId)
            .ToListAsync(cancellationToken);
        var assignedUserIds = assignments.Select(item => item.UserId).ToHashSet();
        var candidates = await BuildReportCandidateUsersAsync(rule, assignedUserIds, cancellationToken);
        var candidateItems = await BuildUserItemsAsync(candidates, cancellationToken);

        return new ReportUserAssignmentResponse
        {
            ReportRuleId = reportRuleId,
            SiteId = site.Id,
            SiteName = site.SiteName,
            CompanyId = company?.Id,
            CompanyName = company?.CompanyName,
            AvailableUsers = candidateItems.Where(user => !assignedUserIds.Contains(Guid.Parse(user.Id))).ToList(),
            AssignedUsers = candidateItems.Where(user => assignedUserIds.Contains(Guid.Parse(user.Id))).ToList()
        };
    }

    // Function summary: Builds a paged assigned or available report-recipient response.
    public async Task<QueryReportUsersResponse?> QueryAssignmentUsersAsync(
        Guid reportRuleId,
        QueryReportUsersRequest request,
        bool assigned,
        CancellationToken cancellationToken)
    {
        var assignmentContext = await LoadAssignmentContextAsync(reportRuleId, cancellationToken);
        if (assignmentContext == null)
        {
            return null;
        }

        var assignedUserIds = await LoadAssignedUserIdsAsync(reportRuleId, cancellationToken);
        var visibleUserIds = await LoadVisibleUserIdsAsync(assignmentContext.SiteId, assignedUserIds, cancellationToken);
        var requestedSort = string.IsNullOrWhiteSpace(request.Sort) ? "email" : request.Sort.Trim();
        var assignedUserCount = await BuildCandidateUserQuery(visibleUserIds, assignedUserIds, true).CountAsync(cancellationToken);
        var users = BuildCandidateUserQuery(visibleUserIds, assignedUserIds, assigned);
        var query = ApplyUserSearch(users, request.SearchText);
        query = ApplyUserQuerySort(query, requestedSort, request.GetNormalizedSortDir());
        var page = request.GetNormalizedPage();
        var pageSize = request.GetNormalizedPageSize();
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var results = await BuildUserItemsFromRowsAsync(rows, cancellationToken);

        return new QueryReportUsersResponse
        {
            ReportRuleId = reportRuleId,
            SiteId = assignmentContext.SiteId,
            SiteName = assignmentContext.SiteName,
            CompanyId = assignmentContext.CompanyId,
            CompanyName = assignmentContext.CompanyName,
            AssignedUserCount = assignedUserCount,
            Results = results,
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize),
            HasPreviousPage = page > 1 && total > 0,
            HasNextPage = page * pageSize < total,
            SearchText = request.SearchText ?? "",
            Sort = requestedSort,
            SortDir = request.GetNormalizedSortDir()
        };
    }

    // Function summary: Loads the site and company context required by report-recipient assignment queries.
    private async Task<ReportAssignmentContext?> LoadAssignmentContextAsync(Guid reportRuleId, CancellationToken cancellationToken)
    {
        var rule = await searchContext.ReportRules
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == reportRuleId && !item.Deleted, cancellationToken);
        if (rule == null)
        {
            return null;
        }

        var site = await domainContext.Sites
            .AsNoTracking()
            .Include(item => item.Contracts)
            .ThenInclude(contract => contract.Company)
            .SingleOrDefaultAsync(item => item.Id == rule.SiteId, cancellationToken);
        if (site == null)
        {
            return null;
        }

        var company = site.Contracts?
            .Select(contract => contract.Company)
            .FirstOrDefault(item => item != null);
        return new ReportAssignmentContext(site.Id, site.SiteName, company?.Id, company?.CompanyName);
    }

    // Function summary: Loads assigned report-recipient IDs as strings so Identity queries can filter without client-side user scans.
    private async Task<IReadOnlyList<string>> LoadAssignedUserIdsAsync(Guid reportRuleId, CancellationToken cancellationToken)
    {
        var assignedUserIds = await searchContext.ReportUsers
            .AsNoTracking()
            .Where(item => item.ReportRuleId == reportRuleId)
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken);
        return assignedUserIds.Select(id => id.ToString()).ToList();
    }

    // Function summary: Loads active site and assigned user IDs that are visible to a report-recipient picker.
    private async Task<IReadOnlyList<string>> LoadVisibleUserIdsAsync(
        Guid siteId,
        IReadOnlyCollection<string> assignedUserIds,
        CancellationToken cancellationToken)
    {
        var activeSiteUserIds = await domainContext.SiteUsers
            .AsNoTracking()
            .Where(siteUser => siteUser.SiteId == siteId && siteUser.EndDate == null)
            .Select(siteUser => siteUser.UserId)
            .ToListAsync(cancellationToken);
        return activeSiteUserIds
            .Select(id => id.ToString())
            .Concat(assignedUserIds)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    // Function summary: Builds the bounded user-search query for assigned or available report recipients.
    private IQueryable<UserSearch> BuildCandidateUserQuery(
        IReadOnlyCollection<string> visibleUserIds,
        IReadOnlyCollection<string> assignedUserIds,
        bool assigned)
    {
        var users = searchContext.UserSearches.AsNoTracking();
        return assigned
            ? users.Where(user => assignedUserIds.Contains(user.Id))
            : users.Where(user =>
                !assignedUserIds.Contains(user.Id) &&
                (visibleUserIds.Contains(user.Id) ||
                    user.Role == RoleNames.RVTMasterAdmin ||
                    user.Role == RoleNames.RVTAdmin));
    }

    // Function summary: Applies recipient search filters that can run before paging.
    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF query predicate; ToLower() is the only case-insensitive form that translates on Npgsql and runs on the InMemory test provider. See docs/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF query predicate; see docs/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF query predicate; StringComparison does not translate on Npgsql. See docs/sonar/globalization-suppressions.md")]
    private static IQueryable<UserSearch> ApplyUserSearch(IQueryable<UserSearch> users, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return users;
        }

        var search = searchText.Trim().ToLower();
        return users.Where(user =>
            (user.Email != null && user.Email.ToLower().Contains(search)) ||
            (user.Name != null && user.Name.ToLower().Contains(search)) ||
            (user.CompanyRole != null && user.CompanyRole.ToLower().Contains(search)) ||
            (user.Role != null && user.Role.ToLower().Contains(search)) ||
            (user.CompanyName != null && user.CompanyName.ToLower().Contains(search)));
    }

    // Function summary: Applies stable SQL-side sorting for recipient queries before paging.
    private static IOrderedQueryable<UserSearch> ApplyUserQuerySort(
        IQueryable<UserSearch> users,
        string sort,
        string direction)
    {
        var descending = string.Equals(direction, SortDirections.Descending, StringComparison.OrdinalIgnoreCase);
        return sort.ToLowerInvariant() switch
        {
            "name" => descending
                ? users.OrderByDescending(user => user.Name).ThenByDescending(user => user.Email).ThenByDescending(user => user.Id)
                : users.OrderBy(user => user.Name).ThenBy(user => user.Email).ThenBy(user => user.Id),
            "role" => descending
                ? users.OrderByDescending(user => user.Role).ThenByDescending(user => user.Email).ThenByDescending(user => user.Id)
                : users.OrderBy(user => user.Role).ThenBy(user => user.Email).ThenBy(user => user.Id),
            "companyname" => descending
                ? users.OrderByDescending(user => user.CompanyName).ThenByDescending(user => user.Email).ThenByDescending(user => user.Id)
                : users.OrderBy(user => user.CompanyName).ThenBy(user => user.Email).ThenBy(user => user.Id),
            _ => descending
                ? users.OrderByDescending(user => user.Email).ThenByDescending(user => user.Id)
                : users.OrderBy(user => user.Email).ThenBy(user => user.Id)
        };
    }

    // Function summary: Hydrates paged recipient rows with company names and active site counts for API display models.
    private async Task<List<UserListItem>> BuildUserItemsFromRowsAsync(
        IReadOnlyList<UserSearch> users,
        CancellationToken cancellationToken)
    {
        var userIds = users
            .Select(user => Guid.TryParse(user.Id, out var parsedId) ? parsedId : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
        var siteCounts = userIds.Count == 0
            ? []
            : await domainContext.SiteUsers
                .AsNoTracking()
                .Where(siteUser => userIds.Contains(siteUser.UserId) && siteUser.EndDate == null)
                .GroupBy(siteUser => siteUser.UserId)
                .Select(group => new { UserId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.UserId, item => item.Count, cancellationToken);

        return users
            .Select(user => BuildUserItem(user, siteCounts))
            .ToList();
    }

    // Function summary: Converts a paged recipient query row into the API user list contract.
    private static UserListItem BuildUserItem(
        UserSearch user,
        IReadOnlyDictionary<Guid, int> siteCounts)
    {
        var parsedId = Guid.TryParse(user.Id, out var userId) ? userId : Guid.Empty;
        return new UserListItem
        {
            Id = user.Id,
            CompanyId = user.CompanyId,
            CompanyName = user.CompanyName,
            IsDisabled = user.IsDisabled,
            Name = user.Name,
            Email = user.Email ?? "",
            PhoneNumber = user.PhoneNumber,
            CompanyRole = user.CompanyRole,
            Role = user.Role ?? "",
            SiteCount = parsedId == Guid.Empty || !siteCounts.TryGetValue(parsedId, out var siteCount) ? 0 : siteCount,
            EmailConfirmed = user.EmailConfirmed,
            CanView = true,
            CanEdit = false,
            CanDisable = false,
            CanEnable = false,
            CanDelete = false,
            CanSendConfirmation = false,
            CanSendPasswordReset = false,
            CanManageNotificationSettings = user.Role == RoleNames.CompanyUser
        };
    }

    private async Task<List<ApplicationUser>> BuildReportCandidateUsersAsync(
        ReportRule rule,
        IReadOnlySet<Guid> assignedUserIds,
        CancellationToken cancellationToken)
    {
        var activeSiteUserIds = await domainContext.SiteUsers
            .AsNoTracking()
            .Where(siteUser => siteUser.SiteId == rule.SiteId && siteUser.EndDate == null)
            .Select(siteUser => siteUser.UserId)
            .ToListAsync(cancellationToken);
        var visibleUserIds = activeSiteUserIds.Concat(assignedUserIds).ToHashSet();

        // Candidates are the site-visible/assigned users plus every admin. Both sets are resolved in SQL, so
        // the whole users table is no longer materialized and no per-user role round trip is issued.
        var visibleUserIdStrings = visibleUserIds
            .Select(id => id.ToString())
            .ToList();
        var adminUserIds = await applicationContext.UserRoles
            .AsNoTracking()
            .Join(
                applicationContext.Roles.AsNoTracking().Where(role => AdminRoleNames.Contains(role.Name!)),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, _) => userRole.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var candidateIds = visibleUserIdStrings
            .Concat(adminUserIds)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (candidateIds.Count == 0)
        {
            return [];
        }

        var candidates = await userManager.Users
            .AsNoTracking()
            .Where(user => candidateIds.Contains(user.Id))
            .ToListAsync(cancellationToken);

        return candidates
            .OrderBy(user => user.Email)
            .ToList();
    }

    private async Task<List<UserListItem>> BuildUserItemsAsync(
        IReadOnlyList<ApplicationUser> users,
        CancellationToken cancellationToken)
    {
        var companyIds = users
            .Where(user => user.CompanyId.HasValue)
            .Select(user => user.CompanyId!.Value)
            .Distinct()
            .ToList();
        var companies = companyIds.Count == 0
            ? []
            : await domainContext.Companies
                .AsNoTracking()
                .Where(company => companyIds.Contains(company.Id))
                .ToDictionaryAsync(company => company.Id, company => company.CompanyName, cancellationToken);
        var userIds = users
            .Select(user => Guid.TryParse(user.Id, out var parsedId) ? parsedId : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
        var siteCounts = userIds.Count == 0
            ? []
            : await domainContext.SiteUsers
                .AsNoTracking()
                .Where(siteUser => userIds.Contains(siteUser.UserId) && siteUser.EndDate == null)
                .GroupBy(siteUser => siteUser.UserId)
                .Select(group => new { UserId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.UserId, item => item.Count, cancellationToken);

        // One join for every user's roles, instead of a UserManager round trip inside the loop below.
        var rolesByUser = await LoadRolesByUserAsync(
            users.Select(user => user.Id).ToList(),
            cancellationToken);

        var items = new List<UserListItem>();
        foreach (var user in users)
        {
            var role = rolesByUser.TryGetValue(user.Id, out var userRoles)
                ? userRoles.FirstOrDefault() ?? ""
                : "";
            var parsedId = Guid.TryParse(user.Id, out var userId) ? userId : Guid.Empty;
            items.Add(new UserListItem
            {
                Id = user.Id,
                CompanyId = user.CompanyId,
                CompanyName = user.CompanyId.HasValue && companies.TryGetValue(user.CompanyId.Value, out var companyName) ? companyName : null,
                IsDisabled = user.IsDisabled,
                Name = user.Name,
                Email = user.Email ?? "",
                PhoneNumber = user.PhoneNumber,
                CompanyRole = user.CompanyRole,
                Role = role,
                SiteCount = parsedId == Guid.Empty || !siteCounts.TryGetValue(parsedId, out var siteCount) ? 0 : siteCount,
                EmailConfirmed = user.EmailConfirmed,
                CanView = true,
                CanEdit = false,
                CanDisable = false,
                CanEnable = false,
                CanDelete = false,
                CanSendConfirmation = false,
                CanSendPasswordReset = false,
                CanManageNotificationSettings = role == RoleNames.CompanyUser
            });
        }

        return items;
    }

    private static bool Contains(string? value, string search)
    {
        return value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static IEnumerable<UserListItem> ApplyUserSort(IEnumerable<UserListItem> users, string sort, string direction)
    {
        var descending = string.Equals(direction, SortDirections.Descending, StringComparison.OrdinalIgnoreCase);
        return sort.ToLowerInvariant() switch
        {
            "name" => descending ? users.OrderByDescending(user => user.Name) : users.OrderBy(user => user.Name),
            "role" => descending ? users.OrderByDescending(user => user.Role) : users.OrderBy(user => user.Role),
            "companyname" => descending ? users.OrderByDescending(user => user.CompanyName) : users.OrderBy(user => user.CompanyName),
            _ => descending ? users.OrderByDescending(user => user.Email) : users.OrderBy(user => user.Email)
        };
    }

    private sealed record ReportAssignmentContext(Guid SiteId, string SiteName, Guid? CompanyId, string? CompanyName);
}
