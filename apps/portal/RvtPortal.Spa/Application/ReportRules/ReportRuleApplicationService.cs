// File summary: Coordinates report-rule business workflows for the SPA API without depending on HTTP transport types.
// Major updates:
// - 2026-07-08 pending Preserved archived-site validation wording after moving report-rule logic to the business layer.
// - 2026-07-08 pending Preserved archived current-site options inside the business-layer detail workflow.
// - 2026-07-05 pending Moved report-rule management, recipient assignment, and generation orchestration out of the controller.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RVT.Entities;
using RVT.BusinessLogic.Application;
using RVT.BusinessLogic.Application.Paging;
using RVT.BusinessLogic.Application.Users;
using RVT.DataAccess.Context;
using RVT.BusinessLogic.Reports;
using RVT.DataAccess.EntityModels.Models;

namespace RvtPortal.Spa.Application.ReportRules;

public interface IReportRuleApplicationService
{
    Task<ApplicationResult<PagedResult<ReportRuleListModel>>> QueryAsync(ReportRuleQuery request, CancellationToken cancellationToken);
    Task<ApplicationResult<ReportRuleOptionsModel>> OptionsAsync(CancellationToken cancellationToken);
    Task<ApplicationResult<ReportRuleDetailModel>> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ApplicationResult<ReportRuleDetailModel>> CreateAsync(PortalUserContext user, ReportRuleMutation request, CancellationToken cancellationToken);
    Task<ApplicationResult<ReportRuleDetailModel>> UpdateAsync(PortalUserContext user, Guid id, ReportRuleMutation request, CancellationToken cancellationToken);
    Task<ApplicationResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<ApplicationResult<ReportUserAssignmentModel>> GetUsersAsync(Guid id, CancellationToken cancellationToken);
    Task<ApplicationResult<ReportUserQueryResult>> QueryUsersAsync(Guid id, PageRequest page, bool assigned, CancellationToken cancellationToken);
    Task<ApplicationResult<ReportUserAssignmentModel>> AddUserAsync(Guid id, Guid userId, CancellationToken cancellationToken);
    Task<ApplicationResult<ReportUserAssignmentModel>> RemoveUserAsync(Guid id, Guid userId, CancellationToken cancellationToken);
    Task<ApplicationResult<ReportGenerationResponseModel>> RequestGenerationAsync(Guid id, ReportGenerationRequestModel request, CancellationToken cancellationToken);
}

public sealed class ReportRuleApplicationService : IReportRuleApplicationService
{
    public const string DefaultSort = "lastGenerated";
    private const int MaxReportNameLength = 128;
    private const string AlertRuleGuidelineContentType = "Alert Rule Guideline";

    public static readonly IReadOnlySet<string> SortFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "reportName",
        "lastGenerated",
        "frequency",
        "siteName",
        "dayOfWeek",
        "dayOfMonth"
    };

    private static readonly ReportFrequencyType[] SupportedFrequencies =
    [
        ReportFrequencyType.Daily,
        ReportFrequencyType.Weekly,
        ReportFrequencyType.Monthly,
        ReportFrequencyType.WeeklyAndMonthly
    ];

    private static readonly DayOfWeek[] SupportedDays =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday
    ];

    private readonly RVTSearchContext searchContext;
    private readonly RVTDbContext domainContext;
    private readonly IPortalUserDirectory userDirectory;
    private readonly IReportGenerationGateway reportGenerationGateway;

    // Function summary: Initializes this type with the data contexts needed for report-rule workflows.
    public ReportRuleApplicationService(
        RVTSearchContext searchContext,
        RVTDbContext domainContext,
        IPortalUserDirectory userDirectory,
        IReportGenerationGateway reportGenerationGateway)
    {
        this.searchContext = searchContext;
        this.domainContext = domainContext;
        this.userDirectory = userDirectory;
        this.reportGenerationGateway = reportGenerationGateway;
    }

    public async Task<ApplicationResult<PagedResult<ReportRuleListModel>>> QueryAsync(ReportRuleQuery request, CancellationToken cancellationToken)
    {
        if (searchContext.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
        {
            return ApplicationResult<PagedResult<ReportRuleListModel>>.Success(await QueryRulesFromTableAsync(request, cancellationToken));
        }

        var query = searchContext.ReportRuleSearches.AsNoTracking();
        if (request.SiteId.HasValue)
        {
            query = query.Where(rule => rule.SiteId == request.SiteId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Page.SearchText))
        {
            query = ApplySearch(query, request.Page.SearchText);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await ApplySort(query, request.Page.Sort, request.Page.SortDir)
            .Skip((request.Page.Page - 1) * request.Page.PageSize)
            .Take(request.Page.PageSize)
            .ToListAsync(cancellationToken);

        return ApplicationResult<PagedResult<ReportRuleListModel>>.Success(new PagedResult<ReportRuleListModel>
        {
            Results = rows.Select(BuildRuleItem).ToList(),
            Total = total,
            Page = request.Page.Page,
            PageSize = request.Page.PageSize,
            SearchText = request.Page.SearchText ?? "",
            Sort = request.Page.Sort,
            SortDir = request.Page.SortDir
        });
    }

    public async Task<ApplicationResult<ReportRuleOptionsModel>> OptionsAsync(CancellationToken cancellationToken)
    {
        return ApplicationResult<ReportRuleOptionsModel>.Success(await BuildOptionsAsync(cancellationToken));
    }

    public async Task<ApplicationResult<ReportRuleDetailModel>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var rule = await searchContext.ReportRules
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        return rule == null
            ? ApplicationResult<ReportRuleDetailModel>.NotFound($"Report rule '{id}' was not found.")
            : ApplicationResult<ReportRuleDetailModel>.Success(await BuildRuleDetailAsync(rule, cancellationToken));
    }

    public async Task<ApplicationResult<ReportRuleDetailModel>> CreateAsync(PortalUserContext user, ReportRuleMutation request, CancellationToken cancellationToken)
    {
        var validationErrors = await ValidateRuleRequestAsync(request, cancellationToken);
        if (validationErrors.Count > 0)
        {
            return ApplicationResult<ReportRuleDetailModel>.Validation(validationErrors.ToArray());
        }

        var rule = new ReportRule
        {
            SiteId = request.SiteId,
            UserId = user.UserId ?? Guid.Empty,
            Frequency = request.Frequency,
            DayOfWeek = request.DayOfWeek,
            DayOfMonth = request.DayOfMonth,
            ReportName = EmptyToNull(request.ReportName),
            Deleted = false
        };
        searchContext.ReportRules.Add(rule);
        await searchContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<ReportRuleDetailModel>.Success(await BuildRuleDetailAsync(rule, cancellationToken));
    }

    public async Task<ApplicationResult<ReportRuleDetailModel>> UpdateAsync(PortalUserContext user, Guid id, ReportRuleMutation request, CancellationToken cancellationToken)
    {
        var rule = await searchContext.ReportRules.SingleOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (rule == null)
        {
            return ApplicationResult<ReportRuleDetailModel>.NotFound($"Report rule '{id}' was not found.");
        }

        var validationErrors = await ValidateRuleRequestAsync(request, cancellationToken);
        if (validationErrors.Count > 0)
        {
            return ApplicationResult<ReportRuleDetailModel>.Validation(validationErrors.ToArray());
        }

        rule.SiteId = request.SiteId;
        rule.UserId = user.UserId ?? Guid.Empty;
        rule.Frequency = request.Frequency;
        rule.DayOfWeek = request.DayOfWeek;
        rule.DayOfMonth = request.DayOfMonth;
        rule.ReportName = EmptyToNull(request.ReportName);
        rule.Deleted = false;
        await searchContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<ReportRuleDetailModel>.Success(await BuildRuleDetailAsync(rule, cancellationToken));
    }

    public async Task<ApplicationResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var rule = await searchContext.ReportRules.SingleOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (rule == null)
        {
            return ApplicationResult<Guid>.NotFound($"Report rule '{id}' was not found.");
        }

        rule.Deleted = true;
        await searchContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult<Guid>.Success(id);
    }

    public async Task<ApplicationResult<ReportUserAssignmentModel>> GetUsersAsync(Guid id, CancellationToken cancellationToken)
    {
        var response = await BuildAssignmentResponseAsync(id, cancellationToken);
        return response == null
            ? ApplicationResult<ReportUserAssignmentModel>.NotFound($"Report rule '{id}' was not found.")
            : ApplicationResult<ReportUserAssignmentModel>.Success(response);
    }

    public async Task<ApplicationResult<ReportUserQueryResult>> QueryUsersAsync(
        Guid id,
        PageRequest page,
        bool assigned,
        CancellationToken cancellationToken)
    {
        var assignments = await BuildAssignmentResponseAsync(id, cancellationToken);
        if (assignments == null)
        {
            return ApplicationResult<ReportUserQueryResult>.NotFound($"Report rule '{id}' was not found.");
        }

        var users = assigned ? assignments.AssignedUsers : assignments.AvailableUsers;
        if (!string.IsNullOrWhiteSpace(page.SearchText))
        {
            var search = page.SearchText.Trim();
            users = users
                .Where(user =>
                    Contains(user.Email, search) ||
                    Contains(user.Name, search) ||
                    Contains(user.Role, search) ||
                    Contains(user.CompanyName, search))
                .ToList();
        }

        var sortedUsers = ApplyUserSort(users, page.Sort, page.SortDir).ToList();
        var total = sortedUsers.Count;
        return ApplicationResult<ReportUserQueryResult>.Success(new ReportUserQueryResult
        {
            ReportRuleId = id,
            SiteId = assignments.SiteId,
            SiteName = assignments.SiteName,
            CompanyId = assignments.CompanyId,
            CompanyName = assignments.CompanyName,
            AssignedUserCount = assignments.AssignedUsers.Count,
            Page = new PagedResult<ReportUserListModel>
            {
                Results = sortedUsers.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToList(),
                Total = total,
                Page = page.Page,
                PageSize = page.PageSize,
                SearchText = page.SearchText ?? "",
                Sort = page.Sort,
                SortDir = page.SortDir
            }
        });
    }

    public async Task<ApplicationResult<ReportUserAssignmentModel>> AddUserAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var rule = await searchContext.ReportRules.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (rule == null)
        {
            return ApplicationResult<ReportUserAssignmentModel>.NotFound($"Report rule '{id}' was not found.");
        }

        var errors = await ValidateReportUserAsync(rule, userId, cancellationToken);
        if (errors.Count > 0)
        {
            return ApplicationResult<ReportUserAssignmentModel>.Validation(errors.ToArray());
        }

        var exists = await searchContext.ReportUsers.AnyAsync(item => item.ReportRuleId == id && item.UserId == userId, cancellationToken);
        if (!exists)
        {
            searchContext.ReportUsers.Add(new ReportUser
            {
                ReportRuleId = id,
                UserId = userId
            });
            await searchContext.SaveChangesAsync(cancellationToken);
        }

        return ApplicationResult<ReportUserAssignmentModel>.Success((await BuildAssignmentResponseAsync(id, cancellationToken))!);
    }

    public async Task<ApplicationResult<ReportUserAssignmentModel>> RemoveUserAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var ruleExists = await searchContext.ReportRules.AsNoTracking().AnyAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (!ruleExists)
        {
            return ApplicationResult<ReportUserAssignmentModel>.NotFound($"Report rule '{id}' was not found.");
        }

        var assignments = await searchContext.ReportUsers
            .Where(item => item.ReportRuleId == id && item.UserId == userId)
            .ToListAsync(cancellationToken);
        if (assignments.Count > 0)
        {
            searchContext.ReportUsers.RemoveRange(assignments);
            await searchContext.SaveChangesAsync(cancellationToken);
        }

        return ApplicationResult<ReportUserAssignmentModel>.Success((await BuildAssignmentResponseAsync(id, cancellationToken))!);
    }

    public async Task<ApplicationResult<ReportGenerationResponseModel>> RequestGenerationAsync(
        Guid id,
        ReportGenerationRequestModel request,
        CancellationToken cancellationToken)
    {
        var exists = await searchContext.ReportRules
            .AsNoTracking()
            .AnyAsync(rule => rule.Id == id && !rule.Deleted, cancellationToken);
        return exists
            ? await reportGenerationGateway.RequestGenerationAsync(id, request, cancellationToken)
            : ApplicationResult<ReportGenerationResponseModel>.NotFound($"Report rule '{id}' was not found.");
    }

    // Function summary: Queries persisted report rules for test providers that cannot seed keyless search views.
    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF query predicate; ToLower() is the only case-insensitive form that translates on Npgsql and runs on the InMemory test provider. See docs/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF query predicate; see docs/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF query predicate; StringComparison does not translate on Npgsql. See docs/sonar/globalization-suppressions.md")]
    private async Task<PagedResult<ReportRuleListModel>> QueryRulesFromTableAsync(ReportRuleQuery request, CancellationToken cancellationToken)
    {
        var query = searchContext.ReportRules
            .AsNoTracking()
            .Where(rule => !rule.Deleted);

        if (request.SiteId.HasValue)
        {
            query = query.Where(rule => rule.SiteId == request.SiteId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Page.SearchText))
        {
            var search = request.Page.SearchText.Trim().ToLower();
            var matchingSiteIds = await domainContext.Sites
                .AsNoTracking()
                .Where(site => site.SiteName.ToLower().Contains(search))
                .Select(site => site.Id)
                .ToListAsync(cancellationToken);
            var frequencyMatches = MatchingFrequencies(search).ToArray();
            query = query.Where(rule =>
                (rule.ReportName != null && rule.ReportName.ToLower().Contains(search)) ||
                matchingSiteIds.Contains(rule.SiteId) ||
                frequencyMatches.Contains(rule.Frequency));
        }

        var total = await query.CountAsync(cancellationToken);
        var pageRows = await ApplySort(query, request.Page.Sort, request.Page.SortDir)
            .Skip((request.Page.Page - 1) * request.Page.PageSize)
            .Take(request.Page.PageSize)
            .ToListAsync(cancellationToken);
        var siteNames = await BuildSiteNamesAsync(pageRows.Select(rule => rule.SiteId), cancellationToken);

        return new PagedResult<ReportRuleListModel>
        {
            Results = pageRows.Select(rule => BuildRuleItem(rule, siteNames)).ToList(),
            Total = total,
            Page = request.Page.Page,
            PageSize = request.Page.PageSize,
            SearchText = request.Page.SearchText ?? "",
            Sort = request.Page.Sort,
            SortDir = request.Page.SortDir
        };
    }

    // Function summary: Builds report-rule detail data and option lists.
    private async Task<ReportRuleDetailModel> BuildRuleDetailAsync(ReportRule rule, CancellationToken cancellationToken)
    {
        var siteNames = await BuildSiteNamesAsync([rule.SiteId], cancellationToken);
        var item = BuildRuleItem(rule, siteNames);
        var options = await BuildOptionsAsync(cancellationToken);
        EnsureCurrentSiteOption(options.Sites, item);
        return new ReportRuleDetailModel
        {
            Id = item.Id,
            SiteId = item.SiteId,
            SiteName = item.SiteName,
            Frequency = item.Frequency,
            FrequencyLabel = item.FrequencyLabel,
            DayOfWeek = item.DayOfWeek,
            DayOfMonth = item.DayOfMonth,
            ReportName = item.ReportName,
            LastGenerated = item.LastGenerated,
            CanManage = item.CanManage,
            Sites = options.Sites,
            Frequencies = options.Frequencies,
            DaysOfWeek = options.DaysOfWeek,
            AlertRuleGuidelines = options.AlertRuleGuidelines,
            AssignedUserCount = await searchContext.ReportUsers.CountAsync(user => user.ReportRuleId == rule.Id, cancellationToken)
        };
    }

    // Function summary: Keeps an archived current site selectable in edit forms while preventing new selection.
    private static void EnsureCurrentSiteOption(List<ReportRuleOptionModel> sites, ReportRuleListModel item)
    {
        var selectedSiteId = item.SiteId.ToString();
        if (sites.Any(site => string.Equals(site.Value, selectedSiteId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        sites.Add(new ReportRuleOptionModel(selectedSiteId, item.SiteName, Disabled: true));
    }

    // Function summary: Builds the options used by report-rule editors and wizards.
    private async Task<ReportRuleOptionsModel> BuildOptionsAsync(CancellationToken cancellationToken)
    {
        var sites = await domainContext.Sites
            .AsNoTracking()
            .Where(site => !site.Archived)
            .OrderBy(site => site.SiteName)
            .Select(site => new ReportRuleOptionModel(site.Id.ToString(), site.SiteName))
            .ToListAsync(cancellationToken);

        return new ReportRuleOptionsModel
        {
            Sites = sites,
            Frequencies = SupportedFrequencies
                .Select(frequency => new ReportRuleOptionModel(((int)frequency).ToString(CultureInfo.InvariantCulture), FrequencyLabel(frequency)))
                .ToList(),
            DaysOfWeek = SupportedDays
                .Select(day => new ReportRuleOptionModel(((int)day).ToString(CultureInfo.InvariantCulture), day.ToString()))
                .ToList(),
            AlertRuleGuidelines = await BuildAlertRuleGuidelinesAsync(cancellationToken)
        };
    }

    // Function summary: Loads published Help CMS report-guideline content for alert-rule setup guidance.
    private async Task<List<ReportAlertRuleGuidelineModel>> BuildAlertRuleGuidelinesAsync(CancellationToken cancellationToken)
    {
        return await domainContext.HelpArticles
            .AsNoTracking()
            .Include(article => article.Section)
            .Where(article => article.IsPublished && article.ContentType == AlertRuleGuidelineContentType)
            .OrderBy(article => article.Section!.SortOrder)
            .ThenBy(article => article.SortOrder)
            .Select(article => new ReportAlertRuleGuidelineModel(
                article.Section == null ? "General" : article.Section.Title,
                article.Title,
                article.Summary,
                article.Body,
                article.Slug))
            .ToListAsync(cancellationToken);
    }

    // Function summary: Validates report-rule mutations before persistence.
    private async Task<List<ApplicationError>> ValidateRuleRequestAsync(ReportRuleMutation request, CancellationToken cancellationToken)
    {
        var errors = new List<ApplicationError>();
        if (!SupportedFrequencies.Contains(request.Frequency))
        {
            errors.Add(new ApplicationError(nameof(ReportRuleMutation.Frequency), "Frequency is not valid for scheduled report rules."));
        }

        var site = await domainContext.Sites
            .AsNoTracking()
            .Where(item => item.Id == request.SiteId)
            .Select(item => new { item.Archived })
            .SingleOrDefaultAsync(cancellationToken);
        if (site == null)
        {
            errors.Add(new ApplicationError(nameof(ReportRuleMutation.SiteId), "Site was not found."));
        }
        else if (site.Archived)
        {
            errors.Add(new ApplicationError(nameof(ReportRuleMutation.SiteId), "Archived sites cannot be assigned to report rules."));
        }

        if (request.Frequency is ReportFrequencyType.Weekly or ReportFrequencyType.WeeklyAndMonthly && !request.DayOfWeek.HasValue)
        {
            errors.Add(new ApplicationError(nameof(ReportRuleMutation.DayOfWeek), "Day of week is required for weekly reports."));
        }
        else if (request.DayOfWeek.HasValue && !SupportedDays.Contains(request.DayOfWeek.Value))
        {
            errors.Add(new ApplicationError(nameof(ReportRuleMutation.DayOfWeek), "Day of week is not valid."));
        }

        if (request.Frequency is ReportFrequencyType.Monthly or ReportFrequencyType.WeeklyAndMonthly &&
            (!request.DayOfMonth.HasValue || request.DayOfMonth < 1 || request.DayOfMonth > 31))
        {
            errors.Add(new ApplicationError(nameof(ReportRuleMutation.DayOfMonth), "Day of month must be between 1 and 31."));
        }

        if (request.ReportName?.Length > MaxReportNameLength)
        {
            errors.Add(new ApplicationError(nameof(ReportRuleMutation.ReportName), $"Report name must be {MaxReportNameLength} characters or fewer."));
        }

        return errors;
    }

    // Function summary: Builds the full assignment model used by report-recipient summary and grids.
    private async Task<ReportUserAssignmentModel?> BuildAssignmentResponseAsync(Guid reportRuleId, CancellationToken cancellationToken)
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

        return new ReportUserAssignmentModel
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

    // Function summary: Builds the role-aware report recipient candidate list for a report rule.
    private async Task<List<PortalUserProfile>> BuildReportCandidateUsersAsync(
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
        var users = await userDirectory.ListUsersAsync(cancellationToken);
        var candidates = new List<PortalUserProfile>();

        foreach (var user in users)
        {
            if (user.IsInRole(PortalRoleNames.RVTMasterAdmin) ||
                user.IsInRole(PortalRoleNames.RVTAdmin) ||
                visibleUserIds.Contains(user.UserId))
            {
                candidates.Add(user);
            }
        }

        return candidates
            .OrderBy(user => user.Email)
            .ToList();
    }

    // Function summary: Builds user list rows with company names and active site counts.
    private async Task<List<ReportUserListModel>> BuildUserItemsAsync(
        IReadOnlyList<PortalUserProfile> users,
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
        var userIds = users.Select(user => user.UserId).ToList();
        var siteCounts = userIds.Count == 0
            ? []
            : await domainContext.SiteUsers
                .AsNoTracking()
                .Where(siteUser => userIds.Contains(siteUser.UserId) && siteUser.EndDate == null)
                .GroupBy(siteUser => siteUser.UserId)
                .Select(group => new { UserId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.UserId, item => item.Count, cancellationToken);

        var items = new List<ReportUserListModel>();
        foreach (var user in users)
        {
            var role = user.PrimaryRole;
            items.Add(new ReportUserListModel(
                user.UserIdText,
                user.CompanyId,
                user.CompanyId.HasValue && companies.TryGetValue(user.CompanyId.Value, out var companyName) ? companyName : null,
                user.IsDisabled,
                user.Name,
                user.Email,
                user.PhoneNumber,
                user.CompanyRole,
                role,
                siteCounts.TryGetValue(user.UserId, out var siteCount) ? siteCount : 0,
                user.EmailConfirmed,
                true,
                false,
                false,
                false,
                false,
                false,
                false,
                role == PortalRoleNames.CompanyUser));
        }

        return items;
    }

    // Function summary: Validates whether a user is eligible to receive a report rule.
    private async Task<List<ApplicationError>> ValidateReportUserAsync(
        ReportRule rule,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var errors = new List<ApplicationError>();
        var user = await userDirectory.FindByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            errors.Add(new ApplicationError("UserId", "User was not found."));
            return errors;
        }

        if (user.IsInRole(PortalRoleNames.RVTMasterAdmin) ||
            user.IsInRole(PortalRoleNames.RVTAdmin))
        {
            return errors;
        }

        if (await domainContext.SiteUsers.AsNoTracking().AnyAsync(siteUser =>
            siteUser.SiteId == rule.SiteId &&
            siteUser.UserId == userId &&
            siteUser.EndDate == null,
            cancellationToken))
        {
            return errors;
        }

        errors.Add(new ApplicationError("UserId", "User is not assigned to the report site."));
        return errors;
    }

    // Function summary: Builds a report-rule list model from persisted rule data.
    private static ReportRuleListModel BuildRuleItem(ReportRule rule, Dictionary<Guid, string> siteNames)
    {
        return new ReportRuleListModel(
            rule.Id,
            rule.SiteId,
            siteNames.TryGetValue(rule.SiteId, out var siteName) ? siteName : "Unknown site",
            rule.Frequency,
            FrequencyLabel(rule.Frequency),
            rule.DayOfWeek,
            rule.DayOfMonth,
            rule.ReportName,
            rule.LastGenerated,
            true);
    }

    // Function summary: Builds a report-rule list model from the search view.
    private static ReportRuleListModel BuildRuleItem(ReportRuleSearch rule)
    {
        return new ReportRuleListModel(
            rule.Id,
            rule.SiteId,
            rule.SiteName,
            rule.Frequency,
            FrequencyLabel(rule.Frequency),
            rule.DayOfWeek,
            rule.DayOfMonth,
            rule.ReportName,
            rule.LastGenerated,
            true);
    }

    // Function summary: Builds site display names keyed by id.
    private async Task<Dictionary<Guid, string>> BuildSiteNamesAsync(IEnumerable<Guid> siteIds, CancellationToken cancellationToken)
    {
        var ids = siteIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await domainContext.Sites
            .AsNoTracking()
            .Where(site => ids.Contains(site.Id))
            .ToDictionaryAsync(site => site.Id, site => site.SiteName, cancellationToken);
    }

    // Function summary: Applies database-side report-rule text and frequency filtering.
    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF query predicate; ToLower() is the only case-insensitive form that translates on Npgsql and runs on the InMemory test provider. See docs/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF query predicate; see docs/sonar/globalization-suppressions.md")]
    [SuppressMessage("Globalization", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF query predicate; StringComparison does not translate on Npgsql. See docs/sonar/globalization-suppressions.md")]
    private static IQueryable<ReportRuleSearch> ApplySearch(IQueryable<ReportRuleSearch> rules, string searchText)
    {
        var search = searchText.Trim().ToLower();
        var frequencyMatches = MatchingFrequencies(search).ToArray();
        return rules.Where(rule =>
            (rule.ReportName != null && rule.ReportName.ToLower().Contains(search)) ||
            rule.SiteName.ToLower().Contains(search) ||
            frequencyMatches.Contains(rule.Frequency));
    }

    // Function summary: Applies database-side sorting for report-rule search rows.
    private static IOrderedQueryable<ReportRuleSearch> ApplySort(IQueryable<ReportRuleSearch> rules, string sort, string direction)
    {
        var descending = string.Equals(direction, PageSortDirections.Descending, StringComparison.OrdinalIgnoreCase);
        return sort.ToLowerInvariant() switch
        {
            "reportname" => descending ? rules.OrderByDescending(rule => rule.ReportName) : rules.OrderBy(rule => rule.ReportName),
            "frequency" => descending ? rules.OrderByDescending(rule => rule.Frequency) : rules.OrderBy(rule => rule.Frequency),
            "sitename" => descending ? rules.OrderByDescending(rule => rule.SiteName) : rules.OrderBy(rule => rule.SiteName),
            "dayofweek" => descending ? rules.OrderByDescending(rule => rule.DayOfWeek) : rules.OrderBy(rule => rule.DayOfWeek),
            "dayofmonth" => descending ? rules.OrderByDescending(rule => rule.DayOfMonth) : rules.OrderBy(rule => rule.DayOfMonth),
            _ => descending ? rules.OrderByDescending(rule => rule.LastGenerated) : rules.OrderBy(rule => rule.LastGenerated)
        };
    }

    // Function summary: Applies database-side sorting for persisted report rules.
    private static IOrderedQueryable<ReportRule> ApplySort(IQueryable<ReportRule> rules, string sort, string direction)
    {
        var descending = string.Equals(direction, PageSortDirections.Descending, StringComparison.OrdinalIgnoreCase);
        return sort.ToLowerInvariant() switch
        {
            "reportname" => descending ? rules.OrderByDescending(rule => rule.ReportName) : rules.OrderBy(rule => rule.ReportName),
            "frequency" => descending ? rules.OrderByDescending(rule => rule.Frequency) : rules.OrderBy(rule => rule.Frequency),
            "dayofweek" => descending ? rules.OrderByDescending(rule => rule.DayOfWeek) : rules.OrderBy(rule => rule.DayOfWeek),
            "dayofmonth" => descending ? rules.OrderByDescending(rule => rule.DayOfMonth) : rules.OrderBy(rule => rule.DayOfMonth),
            "sitename" => descending ? rules.OrderByDescending(rule => rule.SiteId) : rules.OrderBy(rule => rule.SiteId),
            _ => descending ? rules.OrderByDescending(rule => rule.LastGenerated) : rules.OrderBy(rule => rule.LastGenerated)
        };
    }

    // Function summary: Applies report-recipient text matching used by available/assigned grids.
    private static bool Contains(string? value, string search)
    {
        return value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
    }

    // Function summary: Applies report-recipient grid sorting.
    private static IEnumerable<ReportUserListModel> ApplyUserSort(IEnumerable<ReportUserListModel> users, string sort, string direction)
    {
        var descending = string.Equals(direction, PageSortDirections.Descending, StringComparison.OrdinalIgnoreCase);
        return sort.ToLowerInvariant() switch
        {
            "name" => descending ? users.OrderByDescending(user => user.Name) : users.OrderBy(user => user.Name),
            "role" => descending ? users.OrderByDescending(user => user.Role) : users.OrderBy(user => user.Role),
            "companyname" => descending ? users.OrderByDescending(user => user.CompanyName) : users.OrderBy(user => user.CompanyName),
            _ => descending ? users.OrderByDescending(user => user.Email) : users.OrderBy(user => user.Email)
        };
    }

    // Function summary: Matches report-rule frequency labels that participate in text search.
    private static IEnumerable<ReportFrequencyType> MatchingFrequencies(string search)
    {
        return SupportedFrequencies
            .Where(frequency => FrequencyLabel(frequency).Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    // Function summary: Builds the display label for report frequencies.
    private static string FrequencyLabel(ReportFrequencyType frequency)
    {
        return frequency == ReportFrequencyType.WeeklyAndMonthly ? "Weekly and Monthly" : frequency.ToString();
    }

    // Function summary: Normalizes optional string input into persisted nullable values.
    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
