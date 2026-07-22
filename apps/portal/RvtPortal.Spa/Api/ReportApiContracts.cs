// File summary: Exposes API endpoints used by the React portal for report api contracts workflows.
// Major updates:
// - 2026-07-09 pending Removed empty DTO constructors left by generated comments.
// - 2026-06-24 pending Added report generation request and paged report-recipient assignment contracts.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;

namespace RvtPortal.Spa.Api;

public sealed class QueryReportsRequest : PagedQueryRequest
{
}

public class QueryReportsResponse : PagedResponse<ReportListItem>
{
}

public class ReportListItem
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public string SiteName { get; set; } = "";
    public DateTime ReportDate { get; set; }
    public DateTime ReportFrom { get; set; }
    public DateTime ReportTo { get; set; }
    public string ReportLink { get; set; } = "";
    public Guid ReportRuleId { get; set; }
    public int Frequency { get; set; }
    public string FrequencyLabel { get; set; } = "";
    public string? ReportName { get; set; }
    public string? Contracts { get; set; }
}

public class QueryReportRulesRequest : PagedQueryRequest
{
    public Guid? SiteId { get; set; }
}

public class QueryReportRulesResponse : PagedResponse<ReportRuleListItem>
{
}

public class ReportRuleListItem
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public string SiteName { get; set; } = "";
    public ReportFrequencyType Frequency { get; set; }
    public string FrequencyLabel { get; set; } = "";
    public DayOfWeek? DayOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public string? ReportName { get; set; }
    public DateTime? LastGenerated { get; set; }
    public bool CanManage { get; set; }
}

public class ReportRuleDetailResponse : ReportRuleListItem
{
    public List<OptionItem> Sites { get; set; } = [];
    public List<OptionItem> Frequencies { get; set; } = [];
    public List<OptionItem> DaysOfWeek { get; set; } = [];
    public List<ReportAlertRuleGuidelineItem> AlertRuleGuidelines { get; set; } = [];
    public int AssignedUserCount { get; set; }
}

public class ReportRuleOptionsResponse
{
    public List<OptionItem> Sites { get; set; } = [];
    public List<OptionItem> Frequencies { get; set; } = [];
    public List<OptionItem> DaysOfWeek { get; set; } = [];
    public List<ReportAlertRuleGuidelineItem> AlertRuleGuidelines { get; set; } = [];
}

public class ReportRuleMutationRequest
{
    public required Guid SiteId { get; set; }
    public required ReportFrequencyType Frequency { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public string? ReportName { get; set; }
}

public class ReportUserAssignmentResponse
{
    public Guid ReportRuleId { get; set; }
    public Guid SiteId { get; set; }
    public string SiteName { get; set; } = "";
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public List<UserListItem> AvailableUsers { get; set; } = [];
    public List<UserListItem> AssignedUsers { get; set; } = [];
}

public class ReportUserMutationRequest
{
    public required Guid UserId { get; set; }
}

public class QueryReportUsersRequest : PagedQueryRequest
{
}

public class QueryReportUsersResponse : PagedResponse<UserListItem>
{
    public Guid ReportRuleId { get; set; }
    public Guid SiteId { get; set; }
    public string SiteName { get; set; } = "";
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public int AssignedUserCount { get; set; }
}

public class ReportUserAssignmentSummaryResponse
{
    public Guid ReportRuleId { get; set; }
    public Guid SiteId { get; set; }
    public string SiteName { get; set; } = "";
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public int AssignedUserCount { get; set; }
}

public class ReportGenerationRequest
{
    public DateTime? ReportDate { get; set; }
    public bool? SendToRecipients { get; set; } = true;
}

public class ReportGenerationRequestResponse
{
    public Guid Id { get; set; }
    public Guid ReportRuleId { get; set; }
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime RequestedAtUtc { get; set; }
}

public class ReportAlertRuleGuidelineItem
{
    public string MonitorType { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Summary { get; set; }
    public string? Body { get; set; }
    public string? ArticleSlug { get; set; }
}
