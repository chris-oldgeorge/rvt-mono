// File summary: Maps report-rule business-layer models to existing frontend-facing API contracts.
// Major updates:
// - 2026-07-08 pending Added disabled option propagation for adapter routing and kept manual-generation API response mapping.
// - 2026-07-05 pending Added report-rule API mapper for controller-to-business refactoring.
// - 2026-07-05 pending Added report-recipient and manual-generation mapping.

using RVT.BusinessLogic.Application.Paging;
using RVT.BusinessLogic.Reports;

namespace RvtPortal.Spa.Api.Mappers;

public static class ReportRuleApiMapper
{
    // Function summary: Maps paged report-rule application results to the existing API response contract.
    public static QueryReportRulesResponse ToQueryResponse(PagedResult<ReportRuleListModel> result)
    {
        return new QueryReportRulesResponse
        {
            Results = result.Results.Select(ToListItem).ToList(),
            Total = result.Total,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalPages = result.TotalPages,
            HasPreviousPage = result.HasPreviousPage,
            HasNextPage = result.HasNextPage,
            SearchText = result.SearchText,
            Sort = result.Sort,
            SortDir = result.SortDir
        };
    }

    // Function summary: Maps report-rule options to the existing API response contract.
    public static ReportRuleOptionsResponse ToOptionsResponse(ReportRuleOptionsModel model)
    {
        return new ReportRuleOptionsResponse
        {
            Sites = model.Sites.Select(ToOptionItem).ToList(),
            Frequencies = model.Frequencies.Select(ToOptionItem).ToList(),
            DaysOfWeek = model.DaysOfWeek.Select(ToOptionItem).ToList(),
            AlertRuleGuidelines = model.AlertRuleGuidelines.Select(ToGuidelineItem).ToList()
        };
    }

    // Function summary: Maps report-rule detail to the existing API response contract.
    public static ReportRuleDetailResponse ToDetailResponse(ReportRuleDetailModel model)
    {
        return new ReportRuleDetailResponse
        {
            Id = model.Id,
            SiteId = model.SiteId,
            SiteName = model.SiteName,
            Frequency = model.Frequency,
            FrequencyLabel = model.FrequencyLabel,
            DayOfWeek = model.DayOfWeek,
            DayOfMonth = model.DayOfMonth,
            ReportName = model.ReportName,
            LastGenerated = model.LastGenerated,
            CanManage = model.CanManage,
            Sites = model.Sites.Select(ToOptionItem).ToList(),
            Frequencies = model.Frequencies.Select(ToOptionItem).ToList(),
            DaysOfWeek = model.DaysOfWeek.Select(ToOptionItem).ToList(),
            AlertRuleGuidelines = model.AlertRuleGuidelines.Select(ToGuidelineItem).ToList(),
            AssignedUserCount = model.AssignedUserCount
        };
    }

    // Function summary: Maps report-rule mutation request DTOs to transport-neutral business input.
    public static ReportRuleMutation ToMutation(ReportRuleMutationRequest request)
    {
        return new ReportRuleMutation(
            request.SiteId,
            request.Frequency,
            request.DayOfWeek,
            request.DayOfMonth,
            request.ReportName);
    }

    // Function summary: Maps report-recipient assignment details to the existing API response contract.
    public static ReportUserAssignmentResponse ToAssignmentResponse(ReportUserAssignmentModel model)
    {
        return new ReportUserAssignmentResponse
        {
            ReportRuleId = model.ReportRuleId,
            SiteId = model.SiteId,
            SiteName = model.SiteName,
            CompanyId = model.CompanyId,
            CompanyName = model.CompanyName,
            AvailableUsers = model.AvailableUsers.Select(ToUserListItem).ToList(),
            AssignedUsers = model.AssignedUsers.Select(ToUserListItem).ToList()
        };
    }

    // Function summary: Maps paged report-recipient results to the existing API response contract.
    public static QueryReportUsersResponse ToQueryUsersResponse(ReportUserQueryResult model)
    {
        return new QueryReportUsersResponse
        {
            ReportRuleId = model.ReportRuleId,
            SiteId = model.SiteId,
            SiteName = model.SiteName,
            CompanyId = model.CompanyId,
            CompanyName = model.CompanyName,
            AssignedUserCount = model.AssignedUserCount,
            Results = model.Page.Results.Select(ToUserListItem).ToList(),
            Total = model.Page.Total,
            Page = model.Page.Page,
            PageSize = model.Page.PageSize,
            TotalPages = model.Page.TotalPages,
            HasPreviousPage = model.Page.HasPreviousPage,
            HasNextPage = model.Page.HasNextPage,
            SearchText = model.Page.SearchText,
            Sort = model.Page.Sort,
            SortDir = model.Page.SortDir
        };
    }

    // Function summary: Maps a manual generation request DTO to transport-neutral business input.
    public static ReportGenerationRequestModel ToGenerationRequest(ReportGenerationRequest? request)
    {
        return new ReportGenerationRequestModel(request?.ReportDate, request?.SendToRecipients);
    }

    // Function summary: Maps a manual generation response model to the existing API response contract.
    public static ReportGenerationRequestResponse ToGenerationResponse(ReportGenerationResponseModel model)
    {
        return new ReportGenerationRequestResponse
        {
            Id = model.Id,
            ReportRuleId = model.ReportRuleId,
            Status = model.Status,
            Message = model.Message,
            RequestedAtUtc = model.RequestedAtUtc
        };
    }

    private static ReportRuleListItem ToListItem(ReportRuleListModel model)
    {
        return new ReportRuleListItem
        {
            Id = model.Id,
            SiteId = model.SiteId,
            SiteName = model.SiteName,
            Frequency = model.Frequency,
            FrequencyLabel = model.FrequencyLabel,
            DayOfWeek = model.DayOfWeek,
            DayOfMonth = model.DayOfMonth,
            ReportName = model.ReportName,
            LastGenerated = model.LastGenerated,
            CanManage = model.CanManage
        };
    }

    private static OptionItem ToOptionItem(ReportRuleOptionModel model)
    {
        return new OptionItem
        {
            Value = model.Value,
            Label = model.Label,
            Disabled = model.Disabled
        };
    }

    private static ReportAlertRuleGuidelineItem ToGuidelineItem(ReportAlertRuleGuidelineModel model)
    {
        return new ReportAlertRuleGuidelineItem
        {
            MonitorType = model.MonitorType,
            Title = model.Title,
            Summary = model.Summary,
            Body = model.Body,
            ArticleSlug = model.ArticleSlug
        };
    }

    private static UserListItem ToUserListItem(ReportUserListModel model)
    {
        return new UserListItem
        {
            Id = model.Id,
            CompanyId = model.CompanyId,
            CompanyName = model.CompanyName,
            IsDisabled = model.IsDisabled,
            Name = model.Name,
            Email = model.Email,
            PhoneNumber = model.PhoneNumber,
            CompanyRole = model.CompanyRole,
            Role = model.Role,
            SiteCount = model.SiteCount,
            EmailConfirmed = model.EmailConfirmed,
            CanView = model.CanView,
            CanEdit = model.CanEdit,
            CanDisable = model.CanDisable,
            CanEnable = model.CanEnable,
            CanDelete = model.CanDelete,
            CanSendConfirmation = model.CanSendConfirmation,
            CanSendPasswordReset = model.CanSendPasswordReset,
            CanManageNotificationSettings = model.CanManageNotificationSettings
        };
    }
}
