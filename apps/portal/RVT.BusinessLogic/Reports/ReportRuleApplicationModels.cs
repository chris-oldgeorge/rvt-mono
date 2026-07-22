// File summary: Defines transport-neutral report-rule application models used by the business layer.
// Major updates:
// - 2026-07-08 pending Added disabled option state needed when archived current sites are shown by edit workflows.
// - 2026-07-05 pending Added report-rule application models for controller-to-business refactoring.
// - 2026-07-05 pending Added report recipient assignment and manual-generation models.

using RVT.BusinessLogic.Application.Paging;
using RVT.Entities;

namespace RVT.BusinessLogic.Reports;

public sealed record ReportRuleQuery(
    Guid? SiteId,
    PageRequest Page);

public sealed record ReportRuleMutation(
    Guid SiteId,
    ReportFrequencyType Frequency,
    DayOfWeek? DayOfWeek,
    int? DayOfMonth,
    string? ReportName);

public sealed record ReportRuleListModel(
    Guid Id,
    Guid SiteId,
    string SiteName,
    ReportFrequencyType Frequency,
    string FrequencyLabel,
    DayOfWeek? DayOfWeek,
    int? DayOfMonth,
    string? ReportName,
    DateTime? LastGenerated,
    bool CanManage);

public sealed record ReportRuleOptionModel(string Value, string Label, bool Disabled = false);

public sealed record ReportAlertRuleGuidelineModel(
    string MonitorType,
    string Title,
    string? Summary,
    string? Body,
    string? ArticleSlug);

public sealed class ReportRuleOptionsModel
{
    public List<ReportRuleOptionModel> Sites { get; init; } = [];
    public List<ReportRuleOptionModel> Frequencies { get; init; } = [];
    public List<ReportRuleOptionModel> DaysOfWeek { get; init; } = [];
    public List<ReportAlertRuleGuidelineModel> AlertRuleGuidelines { get; init; } = [];
}

public sealed class ReportRuleDetailModel
{
    public Guid Id { get; init; }
    public Guid SiteId { get; init; }
    public string SiteName { get; init; } = "";
    public ReportFrequencyType Frequency { get; init; }
    public string FrequencyLabel { get; init; } = "";
    public DayOfWeek? DayOfWeek { get; init; }
    public int? DayOfMonth { get; init; }
    public string? ReportName { get; init; }
    public DateTime? LastGenerated { get; init; }
    public bool CanManage { get; init; }
    public List<ReportRuleOptionModel> Sites { get; init; } = [];
    public List<ReportRuleOptionModel> Frequencies { get; init; } = [];
    public List<ReportRuleOptionModel> DaysOfWeek { get; init; } = [];
    public List<ReportAlertRuleGuidelineModel> AlertRuleGuidelines { get; init; } = [];
    public int AssignedUserCount { get; init; }
}

public sealed record ReportUserListModel(
    string Id,
    Guid? CompanyId,
    string? CompanyName,
    bool IsDisabled,
    string? Name,
    string Email,
    string? PhoneNumber,
    string? CompanyRole,
    string Role,
    int SiteCount,
    bool EmailConfirmed,
    bool CanView,
    bool CanEdit,
    bool CanDisable,
    bool CanEnable,
    bool CanDelete,
    bool CanSendConfirmation,
    bool CanSendPasswordReset,
    bool CanManageNotificationSettings);

public sealed class ReportUserAssignmentModel
{
    public Guid ReportRuleId { get; init; }
    public Guid SiteId { get; init; }
    public string SiteName { get; init; } = "";
    public Guid? CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public List<ReportUserListModel> AvailableUsers { get; init; } = [];
    public List<ReportUserListModel> AssignedUsers { get; init; } = [];
}

public sealed class ReportUserQueryResult
{
    public Guid ReportRuleId { get; init; }
    public Guid SiteId { get; init; }
    public string SiteName { get; init; } = "";
    public Guid? CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public int AssignedUserCount { get; init; }
    public PagedResult<ReportUserListModel> Page { get; init; } = new();
}

public sealed record ReportGenerationRequestModel(
    DateTime? ReportDate,
    bool? SendToRecipients);

public sealed record ReportGenerationResponseModel(
    Guid Id,
    Guid ReportRuleId,
    string Status,
    string Message,
    DateTime RequestedAtUtc);
